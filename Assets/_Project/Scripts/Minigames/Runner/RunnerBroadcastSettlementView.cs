using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StreamOn.Minigames.Runner
{
    [Serializable]
    public sealed class RunnerSettlementDisplayData
    {
        public string gameTitle;
        public int score;
        public int rawGameScore;
        public int broadcastScore;
        public int previousBestScore;
        public bool isNewRecord;
        public int experienceGained;
        public int levelAfter;
        public bool broadcastCompleted = true;
        public int targetScore;
        public int enemiesDefeated;
        public int hitsTaken;
        public int subscriberDelta;
        public int subscribersAfter;
        public int mentalLevel;
        public long cashAfter;
        public RunnerBroadcastResult broadcastResult;
    }

    public sealed class RunnerBroadcastSettlementView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text gameResultText;
        [SerializeField] private TMP_Text audienceText;
        [SerializeField] private TMP_Text ratingText;
        [SerializeField] private TMP_Text growthText;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text continueLabel;
        [SerializeField, Min(0f)] private float rowRevealDelay = 0.22f;

        private Action _onContinue;
        private Coroutine _reveal;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            continueButton?.onClick.AddListener(Continue);
            HideImmediate();
        }

        public void Show(RunnerSettlementDisplayData data, Action onContinue, string buttonLabel = "다음 날")
        {
            if (data == null) return;
            _onContinue = onContinue;
            if (_reveal != null) StopCoroutine(_reveal);
            gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            _reveal = StartCoroutine(Reveal(data, buttonLabel));
        }

        private IEnumerator Reveal(RunnerSettlementDisplayData data, string buttonLabel)
        {
            int finalScore = data.broadcastScore > 0 ? data.broadcastScore : data.score;
            bool succeeded = data.broadcastCompleted;
            titleText.text = succeeded ? $"{data.gameTitle} 방송 완료!" : $"{data.gameTitle} 방송 종료";
            titleText.color = succeeded ? new Color(0.40f, 0.90f, 0.82f) : new Color(1f, 0.58f, 0.42f);
            SetRow(gameResultText, false); SetRow(audienceText, false); SetRow(ratingText, false); SetRow(growthText, false);
            continueButton.gameObject.SetActive(false);

            string record = data.isNewRecord ? "  ·  신기록!" : string.Empty;
            gameResultText.text = $"게임 점수  {data.rawGameScore:N0}\n방송 보정  →  최종 {finalScore:N0}{record}\n이전 최고 {data.previousBestScore:N0}    적 처치 {data.enemiesDefeated:N0}    피격 {data.hitsTaken:N0}";
            SetRow(gameResultText, true); yield return new WaitForSecondsRealtime(rowRevealDelay);
            RunnerBroadcastResult result = data.broadcastResult;
            if (result != null)
            {
                audienceText.text = $"총 방문 {result.totalVisitors:N0}    평균 {result.averageViewers:0.0}    최고 {result.peakViewers:N0}\n종료 시청자 {result.endingViewers:N0}";
                ratingText.text = $"플레이 {result.gameplayRating:0.0}    생존 {result.survivalRating:0.0}    진행 {result.hostingRating:0.0}\n최종 방송 평점  {result.finalRating:0.0} / 5.0";
                growthText.text = $"팔로워 {(data.subscriberDelta >= 0 ? "+" : string.Empty)}{data.subscriberDelta:N0}    후원 +{result.donationWon:N0}원\n현재 팔로워 {data.subscribersAfter:N0}    보유금 {data.cashAfter:N0}원\n방송인 EXP +{data.experienceGained:N0}    Lv.{data.levelAfter}";
            }
            else
            {
                audienceText.text = "시청자 통계를 불러오지 못했습니다.";
                ratingText.text = "방송 평가 없음";
                growthText.text = $"팔로워 {(data.subscriberDelta >= 0 ? "+" : string.Empty)}{data.subscriberDelta:N0}\n현재 팔로워 {data.subscribersAfter:N0}    보유금 {data.cashAfter:N0}원";
            }
            SetRow(audienceText, true); yield return new WaitForSecondsRealtime(rowRevealDelay);
            SetRow(ratingText, true); yield return new WaitForSecondsRealtime(rowRevealDelay);
            SetRow(growthText, true); yield return new WaitForSecondsRealtime(rowRevealDelay);
            continueLabel.text = buttonLabel;
            continueButton.gameObject.SetActive(true);
            _reveal = null;
        }

        private void Continue()
        {
            canvasGroup.interactable = false;
            Action callback = _onContinue;
            _onContinue = null;
            callback?.Invoke();
        }

        private static void SetRow(TMP_Text row, bool visible) { if (row != null) row.gameObject.SetActive(visible); }

        private void HideImmediate()
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public static class RunnerBroadcastSettlementService
    {
        public static RunnerSettlementDisplayData ApplyTileResult(RunnerCampaignSettings settings,
            RunnerCampaignSaveData save, RunnerBroadcastResult result, int rawScore, int score, int hitsTaken)
        {
            int target = settings.TargetScoreForDay(save.day);
            bool succeeded = score >= target;
            int subscriberDelta = result != null ? result.netFollowerChange : 0;
            save.subscribers = Mathf.Max(0, save.subscribers + subscriberDelta);
            int previousBest = save.bestTileArenaGameScore;
            save.bestBroadcastScore = Mathf.Max(save.bestBroadcastScore, score);
            save.bestTileArenaBroadcastScore = Mathf.Max(save.bestTileArenaBroadcastScore, score);
            save.bestTileArenaGameScore = Mathf.Max(save.bestTileArenaGameScore, rawScore);
            if (result != null)
            {
                save.lifetimeDonations += result.donationWon;
                save.cash += result.donationWon;
            }
            save.broadcastPending = false;
            save.awaitingAdvance = true;
            save.campaignFailed = false;
            int experience = settings.broadcastCompletionExperience
                + Mathf.RoundToInt((result != null ? result.finalRating : 0f) * settings.broadcastRatingExperiencePerPoint)
                + (rawScore > previousBest ? settings.newRecordExperience : 0);
            experience = BroadcasterProgression.AddBroadcastExperience(settings, save, experience);
            save.hiredManagerTier = 0;
            save.managerUsesRemaining = 0;
            save.broadcastSessionExperienceEarned = 0;
            if (save.records == null) save.records = new System.Collections.Generic.List<RunnerCampaignDayRecord>();
            save.records.Add(new RunnerCampaignDayRecord
            {
                day = save.day, selectedAction = save.selectedAction, score = score, targetScore = target,
                succeeded = succeeded, hitsTaken = hitsTaken, subscriberDelta = subscriberDelta, mentalDelta = 0f,
                subscribersAfter = save.subscribers, mentalAfter = save.mentalLevel,
                broadcastRating = result != null ? result.finalRating : 0f,
                peakViewers = result != null ? result.peakViewers : 0,
                averageViewers = result != null ? result.averageViewers : 0f,
                totalVisitors = result != null ? result.totalVisitors : 0,
                donationWon = result != null ? result.donationWon : 0
            });
            int excess = save.records.Count - settings.maximumStoredDayRecords;
            if (excess > 0) save.records.RemoveRange(0, excess);
            RunnerCampaignSaveStore.Save(settings, save, true);
            return new RunnerSettlementDisplayData
            {
                gameTitle = "타일 아레나", score = score, rawGameScore = rawScore, broadcastScore = score,
                broadcastCompleted = true,
                previousBestScore = previousBest, isNewRecord = rawScore > previousBest,
                experienceGained = experience, levelAfter = save.broadcasterLevel, targetScore = target, hitsTaken = hitsTaken,
                subscriberDelta = subscriberDelta, subscribersAfter = save.subscribers,
                mentalLevel = save.mentalLevel, cashAfter = save.cash, broadcastResult = result
            };
        }
    }
}
