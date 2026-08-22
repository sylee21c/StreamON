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

        private void Update()
        {
            if (!BroadcastActive || _finished || Time.timeScale <= 0f) { RefreshHud(); return; }
            RunnerBroadcastSessionStore.Tick(Time.deltaTime);
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
            RunnerBroadcastSessionStore.ApplyPenalty(settings.gameOverTimePenaltySeconds);
            RemainingSeconds = RunnerBroadcastSessionStore.RemainingSeconds;
            RefreshHud();
            if (RemainingSeconds <= 0f) FinishBroadcast();
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
            RunnerBroadcastSessionStore.BeginOrResume(settings, _save);
            DurationSeconds = RunnerBroadcastSessionStore.DurationSeconds;
            RemainingSeconds = RunnerBroadcastSessionStore.RemainingSeconds;
            ElapsedSeconds = RunnerBroadcastSessionStore.ElapsedSeconds;
            AttemptsPlayed = 0;
            BestAttemptScore = 0;
            TotalHitsTaken = 0;
            BroadcastActive = true;
            ScoreMultiplier = 1f + Mathf.Max(0, _save.pcLevel - 1) * settings.scoreBonusPerPcUpgrade;
            _finished = false;
            audience?.BeginBroadcastSession();
            RefreshHud();
        }

        private void FinishBroadcast()
        {
            if (!BroadcastActive || _finished || _save == null) return;
            _finished = true;
            BroadcastActive = false;
            RunnerBroadcastSessionStore.End(settings, _save);
            int score = Mathf.Max(BestAttemptScore, gameController != null ? gameController.Score : 0);
            int target = settings.TargetScoreForDay(_save.day);
            RunnerBroadcastResult result = audience?.FinishBroadcast(score, target, TotalHitsTaken,
                Mathf.Max(1f, ElapsedSeconds), DurationSeconds);
            FindFirstObjectByType<RunnerChatController>()?.BeginBroadcastEndingChat();
            StartCoroutine(FinishBroadcastPresentation(result, score));
        }

        private IEnumerator FinishBroadcastPresentation(RunnerBroadcastResult result, int score)
        {
            float exitDuration = audience != null ? Mathf.Max(0.5f, audience.ViewerExitDuration) : 3.2f;
            if (audience != null) yield return audience.DrainViewersToZero(exitDuration);
            RunnerSettlementDisplayData display = RunnerBroadcastSettlementService.ApplyTileResult(settings,
                _save, result, score, TotalHitsTaken);
            if (settlementView != null) settlementView.Show(display, ReturnToRoom, "다음 날");
            else ReturnToRoom();
        }

        private void ReturnToRoom()
        {
            if (!string.IsNullOrWhiteSpace(settings.roomSceneName)) SceneManager.LoadScene(settings.roomSceneName);
        }

        public bool TrySuspendForGameSwitch()
        {
            RunnerBroadcastSessionStore.SaveProgress(settings);
            BroadcastActive = false;
            return true;
        }

        private void RefreshHud()
        {
            if (remainingTimeText != null)
            {
                int seconds = Mathf.CeilToInt(RemainingSeconds);
                remainingTimeText.text = WaitingForFinalGameOver ? "방송 종료 대기 · 현재 판까지" : $"방송 {seconds / 60:00}:{seconds % 60:00}";
            }
            if (attemptText != null) attemptText.text = $"도전 {Mathf.Max(1, AttemptsPlayed)}회";
        }
    }
}
