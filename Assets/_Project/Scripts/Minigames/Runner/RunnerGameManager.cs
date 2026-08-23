using System;
using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    public enum RunnerGameState { Ready, Playing, GameOver }
    public enum RunnerRunEndReason { None, PlayerDefeated, TimeLimitCompleted }

    public sealed class RunnerGameManager : MonoBehaviour, IBroadcastGameSuspendHandler
    {
        [Header("Scene References")]
        [SerializeField] private RunnerPlayerController player;
        [SerializeField] private RunnerObstacleSpawner spawner;
        [SerializeField] private RunnerGroundLooper groundLooper;
        [SerializeField] private RunnerChatController chat;
        [SerializeField] private RunnerHUD hud;
        [SerializeField] private RunnerCampaignController campaign;
        [SerializeField] private RunnerBroadcastAudienceController audience;

        [Header("Run Balance")]
        [SerializeField] private float startingSpeed = 8f;
        [SerializeField] private float maximumSpeed = 20f;
        [SerializeField] private float speedGainPerSecond = 0.25f;
        [SerializeField] private float scorePerSecond = 10f;
        [SerializeField, Min(0)] private int obstacleClearScore = 25;
        [SerializeField, Min(0)] private int enemyDefeatScore = 75;

        [Header("Broadcast Retry Rules")]
        [SerializeField, Min(0f)] private float gameOverTimePenaltySeconds = 8f;
        [SerializeField, Min(0f)] private float deathAnimationDisplaySeconds = 1.4f;

        [Header("Campaign Difficulty")]
        [SerializeField, Min(1)] private int daysToMaximumDifficulty = 15;
        [SerializeField, Min(0f)] private float maximumStartingSpeedBonus = 3f;
        [SerializeField, Min(0f)] private float maximumSpeedLimitBonus = 4f;

        public RunnerGameState State { get; private set; } = RunnerGameState.Ready;
        public float WorldSpeed { get; private set; }
        public int Score { get; private set; }
        public int HighScore { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public int EnemiesDefeated { get; private set; }
        public int HitsTaken { get; private set; }
        public float BroadcastDurationSeconds { get; private set; } = 90f;
        public float BroadcastSecondsRemaining { get; private set; }
        public float BroadcastElapsedSeconds { get; private set; }
        public bool BroadcastActive => _broadcastActive;
        public bool IsFinalAttempt => _broadcastActive && BroadcastSecondsRemaining <= 0f;
        public bool CanRestartAttempt => _broadcastActive && State == RunnerGameState.GameOver
            && BroadcastSecondsRemaining > 0f;
        public int AttemptsPlayed { get; private set; }
        public RunnerRunEndReason LastEndReason { get; private set; }
        public RunnerBroadcastResult BroadcastResult { get; private set; }
        public int FinalRawGameScore { get; private set; }
        public int FinalBroadcastScore { get; private set; }
        public RunnerCampaignSettings CampaignSettings => campaign != null ? campaign.Settings : null;
        public event Action<float> SpeedChanged;

        private float _rawScore;
        private float _runStartingSpeed;
        private float _runMaximumSpeed;
        private float _runScoreMultiplier = 1f;
        private Coroutine _gameOverPresentationRoutine;
        private Coroutine _broadcastFinishRoutine;
        private bool _broadcastActive;
        private int _bestAttemptScore;
        private int _lastReportedAttemptScore;

        private void Awake()
        {
            _runStartingSpeed = startingSpeed;
            _runMaximumSpeed = maximumSpeed;
            if (campaign == null) campaign = GetComponent<RunnerCampaignController>();
            if (audience == null) audience = GetComponent<RunnerBroadcastAudienceController>();
            if (audience == null) audience = gameObject.AddComponent<RunnerBroadcastAudienceController>();
        }

        private void Start()
        {
            RunnerBroadcastSessionStore.TimeBonusGranted += HandleTimeBonusGranted;
            HighScore = RunnerUserSettingsStore.Load().runnerHighScore;
            if (campaign != null) campaign.Initialize(this);
            else BeginRun();
        }

        private void OnDestroy() => RunnerBroadcastSessionStore.TimeBonusGranted -= HandleTimeBonusGranted;

        private void Update()
        {
            if (_broadcastActive)
            {
                if (CampaignSettings != null && RunnerBroadcastSessionStore.IsActive)
                {
                    RunnerBroadcastSessionStore.Tick(Time.unscaledDeltaTime);
                    BroadcastElapsedSeconds = RunnerBroadcastSessionStore.ElapsedSeconds;
                    BroadcastSecondsRemaining = RunnerBroadcastSessionStore.RemainingSeconds;
                }
                else
                {
                    BroadcastElapsedSeconds += Time.unscaledDeltaTime;
                    BroadcastSecondsRemaining = Mathf.Max(0f, BroadcastSecondsRemaining - Time.unscaledDeltaTime);
                }
                if (State == RunnerGameState.GameOver)
                {
                    hud.SetScore(Score, HighScore, WorldSpeed, BroadcastSecondsRemaining);
                    hud.SetRetryAvailable(CanRestartAttempt, BroadcastSecondsRemaining);
                    if (BroadcastSecondsRemaining <= 0f) FinishBroadcast();
                }
            }
            if (State != RunnerGameState.Playing) return;
            WorldSpeed = Mathf.Min(_runMaximumSpeed, WorldSpeed + speedGainPerSecond * Time.deltaTime);
            ElapsedSeconds += Time.deltaTime;
            _rawScore += scorePerSecond * _runScoreMultiplier * Time.deltaTime * (WorldSpeed / _runStartingSpeed);
            Score = Mathf.FloorToInt(_rawScore);
            ReportRawScoreDelta();
            hud.SetScore(Score, HighScore, WorldSpeed, BroadcastSecondsRemaining);
            SpeedChanged?.Invoke(WorldSpeed);
        }

        public void BeginRun()
        {
            if (CampaignSettings != null && RunnerCampaignSaveStore.TryLoad(CampaignSettings, out RunnerCampaignSaveData sessionSave))
            {
                if (!RunnerBroadcastSessionStore.BeginOrResume(CampaignSettings, sessionSave, BroadcastGameId.Runner))
                {
                    Debug.LogError("이미 다른 게임 방송이 진행 중이라 러너 방송을 시작할 수 없습니다.", this);
                    return;
                }
                BroadcastDurationSeconds = RunnerBroadcastSessionStore.DurationSeconds;
                BroadcastSecondsRemaining = RunnerBroadcastSessionStore.RemainingSeconds;
                BroadcastElapsedSeconds = RunnerBroadcastSessionStore.ElapsedSeconds;
            }
            _broadcastActive = true;
            AttemptsPlayed = 0;
            _bestAttemptScore = 0;
            ElapsedSeconds = 0f;
            EnemiesDefeated = 0;
            HitsTaken = 0;
            LastEndReason = RunnerRunEndReason.None;
            BroadcastResult = null;
            FinalRawGameScore = 0;
            FinalBroadcastScore = 0;
            if (!RunnerBroadcastSessionStore.IsActive)
            {
                BroadcastElapsedSeconds = 0f;
                BroadcastSecondsRemaining = BroadcastDurationSeconds;
            }
            if (campaign != null && campaign.IsActive)
                audience.BeginBroadcast(campaign.Followers, campaign.GameSkill, campaign.TalkingSkill, campaign.MentalLevel);
            chat.ResetChat();
            BeginAttempt(false);
        }

        private void BeginAttempt(bool isRetry)
        {
            if (_gameOverPresentationRoutine != null)
            {
                StopCoroutine(_gameOverPresentationRoutine);
                _gameOverPresentationRoutine = null;
            }
            State = RunnerGameState.Playing;
            AttemptsPlayed++;
            WorldSpeed = _runStartingSpeed;
            _rawScore = 0f;
            Score = 0;
            _lastReportedAttemptScore = 0;
            LastEndReason = RunnerRunEndReason.None;
            groundLooper?.ResetTiles();
            spawner.ResetRun();
            player.ResetPlayer();
            hud.ShowGameOver(false);
            hud.SetScore(0, HighScore, WorldSpeed, BroadcastSecondsRemaining);
            hud.SetHealth(player.CurrentHealth, player.MaxHealth);
            hud.SetRetryAvailable(true, BroadcastSecondsRemaining);
            if (isRetry) chat.ResumeRunChat();
            else chat.React(RunnerChatEvent.RunStarted);
        }

        public void PrepareCampaignDay()
        {
            State = RunnerGameState.Ready;
            WorldSpeed = 0f;
            _rawScore = 0f;
            Score = 0;
            ElapsedSeconds = 0f;
            EnemiesDefeated = 0;
            HitsTaken = 0;
            LastEndReason = RunnerRunEndReason.None;
            _broadcastActive = false;
            AttemptsPlayed = 0;
            _bestAttemptScore = 0;
            BroadcastElapsedSeconds = 0f;
            BroadcastSecondsRemaining = BroadcastDurationSeconds;
            groundLooper?.ResetTiles();
            spawner.ResetRun();
            player.ResetPlayer();
            hud.ShowGameOver(false);
            hud.SetScore(0, HighScore, 0f);
            hud.SetHealth(player.CurrentHealth, player.MaxHealth);
            chat.ResetChat();
        }

        public void ConfigureCampaignRun(int day, int gameSkill, int healthStat, float broadcastDurationSeconds,
            float gameOverPenaltySeconds = 8f, int pcLevel = 1, int microphoneLevel = 1, int interiorLevel = 1)
        {
            int dayIndex = Mathf.Max(0, day - 1);
            float difficulty = Mathf.Clamp01(dayIndex / (float)Mathf.Max(1, daysToMaximumDifficulty));
            _runStartingSpeed = startingSpeed + maximumStartingSpeedBonus * difficulty;
            _runMaximumSpeed = maximumSpeed + maximumSpeedLimitBonus * difficulty;
            _runScoreMultiplier = 1f;
            BroadcastDurationSeconds = Mathf.Max(1f, broadcastDurationSeconds);
            BroadcastSecondsRemaining = BroadcastDurationSeconds;
            gameOverTimePenaltySeconds = Mathf.Max(0f, gameOverPenaltySeconds);
            player.ConfigureForSkill(1, 1);
            spawner.ConfigureDifficulty(difficulty);
            audience.Configure(campaign != null ? campaign.GrowthSettings : null, this, chat);
            audience.ConfigureEquipment(microphoneLevel, interiorLevel);
        }

        public void OnObstacleCleared(RunnerObstacleType obstacleType)
        {
            if (State != RunnerGameState.Playing) return;
            _rawScore += obstacleClearScore;
            Score = Mathf.FloorToInt(_rawScore);
            ReportRawScoreDelta();
            audience?.OnObstacleCleared();
            chat.React(obstacleType == RunnerObstacleType.Roll
                ? RunnerChatEvent.PlayerRolled
                : RunnerChatEvent.ObstacleCleared);
        }

        public void OnPlayerHit()
        {
            if (State != RunnerGameState.Playing) return;
            HitsTaken++;
            audience?.OnPlayerHit(player.CurrentHealth <= 1);
            chat.React(player.CurrentHealth <= 1 ? RunnerChatEvent.LowHealth : RunnerChatEvent.PlayerHit);
            hud.SetHealth(player.CurrentHealth, player.MaxHealth);
            if (player.CurrentHealth <= 0) EndRun(RunnerRunEndReason.PlayerDefeated);
        }

        public void OnPlayerJumped() => chat.React(RunnerChatEvent.PlayerJumped);

        public void OnAttackMissed()
        {
            audience?.OnAttackMissed();
            chat.React(RunnerChatEvent.AttackMissed);
        }

        public void OnEnemyDefeated()
        {
            if (State != RunnerGameState.Playing) return;
            EnemiesDefeated++;
            _rawScore += enemyDefeatScore;
            Score = Mathf.FloorToInt(_rawScore);
            ReportRawScoreDelta();
            audience?.OnEnemyDefeated();
            chat.React(RunnerChatEvent.EnemyDefeated);
        }

        public void EndRun() => EndRun(RunnerRunEndReason.PlayerDefeated);

        public void EndRun(RunnerRunEndReason reason)
        {
            if (State == RunnerGameState.GameOver) return;
            State = RunnerGameState.GameOver;
            LastEndReason = reason;
            _bestAttemptScore = Mathf.Max(_bestAttemptScore, Score);
            bool isNewHighScore = Score > HighScore;
            if (isNewHighScore)
            {
                HighScore = Score;
                RunnerUserSettingsData userSettings = RunnerUserSettingsStore.Load();
                userSettings.runnerHighScore = HighScore;
                RunnerUserSettingsStore.Save(userSettings);
                audience?.OnNewHighScore();
            }
            if (reason == RunnerRunEndReason.PlayerDefeated)
            {
                audience?.OnAttemptDefeated();
                if (RunnerBroadcastSessionStore.IsActive)
                {
                    float penalty = gameOverTimePenaltySeconds;
                    if (CampaignSettings != null && RunnerCampaignSaveStore.TryLoad(CampaignSettings, out RunnerCampaignSaveData penaltySave))
                        penalty = CampaignSettings.StaminaRule(penaltySave.staminaRank)?.gameOverTimeLoss ?? penalty;
                    RunnerBroadcastSessionStore.ApplyPenalty(penalty);
                    BroadcastSecondsRemaining = RunnerBroadcastSessionStore.RemainingSeconds;
                }
                else BroadcastSecondsRemaining = Mathf.Max(0f, BroadcastSecondsRemaining - gameOverTimePenaltySeconds);
            }
            else if (reason == RunnerRunEndReason.TimeLimitCompleted)
                BroadcastSecondsRemaining = 0f;
            chat.BeginRunEndedChat(isNewHighScore, false);
            hud.SetScore(Score, HighScore, WorldSpeed, Mathf.Max(0f, BroadcastSecondsRemaining));
            if (reason == RunnerRunEndReason.PlayerDefeated && deathAnimationDisplaySeconds > 0f)
            {
                hud.ShowGameOver(false);
                _gameOverPresentationRoutine = StartCoroutine(ShowGameOverAfterDeathAnimation());
            }
            else
            {
                hud.ShowGameOver(true);
                hud.SetRetryAvailable(CanRestartAttempt, BroadcastSecondsRemaining);
            }
            if (BroadcastSecondsRemaining <= 0f) FinishBroadcast();
        }

        private System.Collections.IEnumerator ShowGameOverAfterDeathAnimation()
        {
            yield return new WaitForSecondsRealtime(deathAnimationDisplaySeconds);
            _gameOverPresentationRoutine = null;
            if (State != RunnerGameState.GameOver || !_broadcastActive
                || LastEndReason != RunnerRunEndReason.PlayerDefeated) yield break;
            hud.ShowGameOver(true);
            hud.SetRetryAvailable(CanRestartAttempt, BroadcastSecondsRemaining);
        }

        private void FinishBroadcast()
        {
            if (!_broadcastActive) return;
            _broadcastActive = false;
            FinalRawGameScore = RunnerBroadcastSessionStore.RawScore;
            FinalBroadcastScore = RunnerBroadcastSessionStore.BroadcastScore;
            State = RunnerGameState.GameOver;
            LastEndReason = RunnerRunEndReason.TimeLimitCompleted;
            Score = Mathf.Max(Score, FinalRawGameScore);
            _rawScore = Score;
            bool isNewHighScore = Score > HighScore;
            if (isNewHighScore)
            {
                HighScore = Score;
                RunnerUserSettingsData userSettings = RunnerUserSettingsStore.Load();
                userSettings.runnerHighScore = HighScore;
                RunnerUserSettingsStore.Save(userSettings);
                audience?.OnNewHighScore();
            }
            if (campaign != null && campaign.IsActive)
                BroadcastResult = audience?.FinishBroadcast(FinalBroadcastScore, campaign.CurrentTargetScore, HitsTaken, EnemiesDefeated,
                    BroadcastElapsedSeconds, BroadcastDurationSeconds, true);
            chat.BeginRunEndedChat(isNewHighScore, true);
            hud.SetScore(Score, HighScore, WorldSpeed, 0f);
            hud.SetRetryAvailable(false, 0f);
            hud.ShowGameOver(false);
            if (_broadcastFinishRoutine != null) StopCoroutine(_broadcastFinishRoutine);
            _broadcastFinishRoutine = StartCoroutine(FinishBroadcastPresentation());
        }

        private System.Collections.IEnumerator FinishBroadcastPresentation()
        {
            float exitDuration = campaign != null && campaign.GrowthSettings != null
                ? campaign.GrowthSettings.viewerExitDuration : 3.2f;
            if (audience != null) yield return audience.DrainViewersToZero(exitDuration);
            _broadcastFinishRoutine = null;
            if (campaign != null && campaign.IsActive) campaign.HandleRunEnded();
            else hud.ShowGameOver(true);
        }

        public void RestartRun()
        {
            if (State != RunnerGameState.GameOver) return;
            if (CanRestartAttempt) BeginAttempt(true);
            else if (!_broadcastActive && (campaign == null || !campaign.IsActive)) BeginRun();
        }

        public bool TrySuspendForGameSwitch()
        {
            return !_broadcastActive;
        }

        private void ReportRawScoreDelta()
        {
            int delta = Score - _lastReportedAttemptScore;
            if (delta <= 0) return;
            _lastReportedAttemptScore = Score;
            RunnerBroadcastSessionStore.AddRawPoints(delta, audience != null ? audience.Hype : 50f);
        }

        private void HandleTimeBonusGranted(float seconds, int score) => hud?.ShowTimeBonus(seconds);

        public void NotifyChat(RunnerChatEvent chatEvent) => chat?.React(chatEvent);

        public RunnerChatSnapshot CreateChatSnapshot(string events)
        {
            RunnerChatSnapshot snapshot = new RunnerChatSnapshot
            {
                gameTitle = "러너",
                gameState = State.ToString(),
                events = events,
                score = Score,
                highScore = HighScore,
                speed = WorldSpeed,
                health = player != null ? player.CurrentHealth : 0,
                maxHealth = player != null ? player.MaxHealth : 0,
                enemiesDefeated = EnemiesDefeated,
                hitsTaken = HitsTaken,
                elapsedSeconds = ElapsedSeconds,
                broadcastSecondsRemaining = BroadcastSecondsRemaining,
                broadcastDurationSeconds = BroadcastDurationSeconds,
                runEndReason = LastEndReason.ToString()
            };
            if (campaign != null && campaign.IsActive)
            {
                snapshot.campaignDay = campaign.Day;
                snapshot.campaignMaximumDays = campaign.MaximumDays;
                snapshot.campaignEndless = campaign.IsEndless;
                snapshot.subscribers = campaign.Subscribers;
                snapshot.mental = campaign.MentalLevel;
                snapshot.gameSkill = campaign.GameSkill;
                snapshot.talkingSkill = campaign.TalkingSkill;
                snapshot.healthStat = campaign.HealthStat;
                snapshot.targetScore = campaign.CurrentTargetScore;
                snapshot.campaignBestScore = campaign.BestBroadcastScore;
                snapshot.selectedDayAction = campaign.LastSelectedAction;
            }
            if (audience != null)
            {
                snapshot.currentViewers = audience.CurrentViewers;
                snapshot.chattingViewers = audience.ChattingViewers;
                snapshot.peakViewers = audience.PeakViewers;
                snapshot.totalVisitors = audience.TotalVisitors;
                snapshot.broadcastHype = audience.Hype;
                snapshot.broadcastRating = BroadcastResult != null ? BroadcastResult.finalRating : 0f;
                snapshot.donationWon = BroadcastResult != null ? BroadcastResult.donationWon : 0;
            }
            return snapshot;
        }
    }
}
