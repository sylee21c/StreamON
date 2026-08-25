using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StreamOn.Minigames.TileArena
{
    using StreamOn.Minigames.Runner;

    public sealed class TileArenaBroadcastSessionController : MonoBehaviour, IBroadcastGameSuspendHandler
    {
        [Header("Scene References")]
        [SerializeField] private TileArenaController gameController;
        [SerializeField] private TileArenaChatAdapter audience;
        [SerializeField] private RunnerCampaignSettings settings;
        [SerializeField] private RunnerBroadcastSettlementView settlementView;
        [SerializeField] private TMP_Text remainingTimeText;
        [SerializeField] private TMP_Text attemptText;
        [SerializeField] private TMP_Text timeBonusText;
        [SerializeField, Min(0f)] private float timeBonusVisibleSeconds = 2f;

        public bool BroadcastActive { get; private set; }
        public float DurationSeconds { get; private set; }
        public float RemainingSeconds { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public int AttemptsPlayed { get; private set; }
        public int BestAttemptScore { get; private set; }
        public int TotalHitsTaken { get; private set; }
        public bool WaitingForFinalGameOver => BroadcastActive && RemainingSeconds <= 0f && gameController != null && gameController.IsRunning;
        public float ScoreMultiplier { get; private set; } = 1f;

        private RunnerCampaignSaveData _save;
        private bool _finished;

        private void Awake()
        {
            if (gameController == null) gameController = FindFirstObjectByType<TileArenaController>();
            if (audience == null) audience = FindFirstObjectByType<TileArenaChatAdapter>();
            if (settlementView == null) settlementView = FindFirstObjectByType<RunnerBroadcastSettlementView>();
        }

        private void OnEnable() => RunnerBroadcastSessionStore.TimeBonusGranted += HandleTimeBonus;
        private void OnDisable() => RunnerBroadcastSessionStore.TimeBonusGranted -= HandleTimeBonus;

        private void Update()
        {
            if (!BroadcastActive || _finished || Time.timeScale <= 0f) { RefreshHud(); return; }
            RunnerBroadcastSessionStore.Tick(Time.unscaledDeltaTime);
            ElapsedSeconds = RunnerBroadcastSessionStore.ElapsedSeconds;
            RemainingSeconds = RunnerBroadcastSessionStore.RemainingSeconds;
            RefreshHud();
            if (RemainingSeconds <= 0f && gameController != null && !gameController.IsRunning) FinishBroadcast();
        }

        public bool TryStartAttempt()
        {
            if (_finished) return false;
            if (!BroadcastActive) BeginBroadcast();
            if (!BroadcastActive || RemainingSeconds <= 0f) return false;
            AttemptsPlayed++;
            RefreshHud();
            return true;
        }

        public void OnAttemptGameOver(int score, int hitsTaken)
        {
            if (!BroadcastActive || _finished) return;
            BestAttemptScore = Mathf.Max(BestAttemptScore, score);
            TotalHitsTaken += Mathf.Max(0, hitsTaken);
            float penalty = settings.GameRule(BroadcastGameId.TileArena).baseGameOverTimeLoss;
            RunnerBroadcastSessionStore.ApplyPenalty(penalty);
            RemainingSeconds = RunnerBroadcastSessionStore.RemainingSeconds;
            RefreshHud();
            // There are no additional attempts after game over; immediately enter the
            // common broadcast-ending presentation and settlement flow.
            FinishBroadcast();
        }

        private void BeginBroadcast()
        {
            if (settings == null)
            {
                Debug.LogError("TILE ARENA broadcast session has no campaign settings assigned.", this);
                return;
            }
            if (!RunnerCampaignSaveStore.TryLoad(settings, out _save))
            {
                // Keep the minigame directly testable from its own scene as well as through the room flow.
                _save = RunnerCampaignSaveStore.CreateNew(settings);
                RunnerCampaignSaveStore.Save(settings, _save, true);
            }
            if (!RunnerBroadcastSessionStore.BeginOrResume(settings, _save, BroadcastGameId.TileArena))
            {
                Debug.LogError("이미 다른 게임 방송이 진행 중이라 타일 아레나를 시작할 수 없습니다.", this);
                return;
            }
            DurationSeconds = RunnerBroadcastSessionStore.DurationSeconds;
            RemainingSeconds = RunnerBroadcastSessionStore.RemainingSeconds;
            ElapsedSeconds = RunnerBroadcastSessionStore.ElapsedSeconds;
            AttemptsPlayed = 0;
            BestAttemptScore = 0;
            TotalHitsTaken = 0;
            BroadcastActive = true;
            ScoreMultiplier = 1f;
            _finished = false;
            audience?.BeginBroadcastSession();
            RefreshHud();
        }

        private void FinishBroadcast()
        {
            if (!BroadcastActive || _finished || _save == null) return;
            _finished = true;
            BroadcastActive = false;
            int rawScore = RunnerBroadcastSessionStore.RawScore;
            int score = RunnerBroadcastSessionStore.BroadcastScore;
            int ratingTarget = settings.RatingTargetScore(BroadcastGameId.TileArena);
            RunnerBroadcastResult result = audience?.FinishBroadcast(score, ratingTarget, TotalHitsTaken,
                Mathf.Max(1f, ElapsedSeconds), DurationSeconds);
            FindFirstObjectByType<RunnerChatController>()?.BeginBroadcastEndingChat();
            StartCoroutine(FinishBroadcastPresentation(result, rawScore, score));
        }

        private IEnumerator FinishBroadcastPresentation(RunnerBroadcastResult result, int rawScore, int score)
        {
            // Commit and present the result in the game-over frame. Viewer drain is only
            // background presentation and must not delay settlement interaction.
            if (RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData stagedSave)) _save = stagedSave;
            RunnerSettlementDisplayData display = RunnerBroadcastSettlementService.ApplyTileResult(settings,
                _save, result, rawScore, score, TotalHitsTaken);
            RunnerBroadcastSessionStore.Complete(settings, _save);
            RunnerBroadcastHeatGauge.Hide();
            if (settlementView != null) settlementView.Show(display, ReturnToRoom, "다음 날");
            else ReturnToRoom();

            float exitDuration = audience != null ? Mathf.Max(0.5f, audience.ViewerExitDuration) : 3.2f;
            if (audience != null) yield return audience.DrainViewersToZero(exitDuration);
        }

        private void ReturnToRoom()
        {
            if (!string.IsNullOrWhiteSpace(settings.roomSceneName)) SceneManager.LoadScene(settings.roomSceneName);
        }

        public bool TrySuspendForGameSwitch()
        {
            return !BroadcastActive;
        }

        public void OnRawPointsEarned(int points)
        {
            if (!BroadcastActive || points <= 0) return;
            RunnerBroadcastSessionStore.AddRawPoints(points, audience != null ? audience.Hype : 50f);
        }

        private void HandleTimeBonus(float seconds, int score)
        {
            if (timeBonusText == null || !BroadcastActive) return;
            StopCoroutine(nameof(HideTimeBonus));
            timeBonusText.gameObject.SetActive(true);
            timeBonusText.text = $"방송 호조! 방송 시간 +{seconds:0.#}초";
            StartCoroutine(nameof(HideTimeBonus));
        }

        private IEnumerator HideTimeBonus()
        {
            yield return new WaitForSecondsRealtime(timeBonusVisibleSeconds);
            if (timeBonusText != null) timeBonusText.gameObject.SetActive(false);
        }

        private void RefreshHud()
        {
            if (remainingTimeText != null)
            {
                int seconds = Mathf.CeilToInt(RemainingSeconds);
                remainingTimeText.text = WaitingForFinalGameOver ? "방송 종료 대기 / 현재 판까지" : $"방송 {seconds / 60:00}:{seconds % 60:00}";
            }
            if (attemptText != null) attemptText.text = $"도전 {Mathf.Max(1, AttemptsPlayed)}회";
        }
    }
}
