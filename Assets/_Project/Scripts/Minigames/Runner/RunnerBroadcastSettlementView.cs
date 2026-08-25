using System;
using System.Collections;
using System.Collections.Generic;
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
        public int experienceBefore;
        public int experienceAfter;
        public int levelBefore;
        public int levelAfter;
        public int finalScore;
        public bool broadcastCompleted = true;
        public int targetScore;
        public int enemiesDefeated;
        public int hitsTaken;
        public int subscriberDelta;
        public int subscribersAfter;
        public int mentalLevel;
        public long cashAfter;
        public long managerSalary;
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
        [SerializeField] private Image experienceFill;
        [SerializeField] private Image ratingFill;
        [SerializeField] private RunnerCampaignSettings campaignSettings;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text continueLabel;
        [SerializeField, Min(0f)] private float sectionRevealDelay = 0.24f;
        [SerializeField, Min(0f)] private float labelRevealDelay = 0.10f;
        [SerializeField, Min(0.05f)] private float numberCountDuration = 0.48f;
        [SerializeField, Min(0.05f)] private float gaugeFillDuration = 0.9f;

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
            bool succeeded = data.broadcastCompleted;
            titleText.text = succeeded ? $"{data.gameTitle} 방송 완료!" : $"{data.gameTitle} 방송 종료";
            titleText.color = succeeded ? new Color(0.40f, 0.90f, 0.82f) : new Color(1f, 0.58f, 0.42f);
            SetSection(gameResultText, false);
            SetSection(audienceText, false);
            SetSection(growthText, false);
            SetSection(ratingText, false);
            SetGauge(experienceFill, 0f);
            SetGauge(ratingFill, 0f);
            continueButton.gameObject.SetActive(false);
            yield return Wait(sectionRevealDelay);

            RunnerBroadcastResult result = data.broadcastResult;
            int finalScore = data.finalScore != 0 ? data.finalScore
                : data.broadcastScore != 0 ? data.broadcastScore : data.score;

            SetSection(gameResultText, true);
            List<string> gameLines = new List<string>();
            yield return CountLine(gameResultText, gameLines, "게임 점수", data.rawGameScore, value => value.ToString("N0"));
            float effectiveHeatMultiplier = data.rawGameScore > 0
                ? data.broadcastScore / (float)data.rawGameScore
                : 1f;
            yield return CountFloatLine(gameResultText, gameLines, "방송 보정", effectiveHeatMultiplier,
                value => $"x{value:0.00}");
            yield return CountLine(gameResultText, gameLines, "최종 점수", finalScore, value => value.ToString("N0"),
                data.isNewRecord ? "  <color=#FFE74A>신기록!</color>" : string.Empty);
            yield return Wait(sectionRevealDelay);

            SetSection(audienceText, true);
            List<string> audienceLines = new List<string>();
            yield return CountLine(audienceText, audienceLines, "최고 시청자 수", result != null ? result.peakViewers : 0,
                value => value.ToString("N0"));
            yield return CountLine(audienceText, audienceLines, "총 시청자 수", result != null ? result.totalVisitors : 0,
                value => value.ToString("N0"));
            yield return Wait(sectionRevealDelay);

            SetSection(growthText, true);
            List<string> growthLines = new List<string>();
            yield return CountLine(growthText, growthLines, "팔로워", data.subscriberDelta, FormatSigned);
            yield return CountDualLine(growthText, growthLines,
                "후원", result != null ? result.donationWon : 0, value => $"+{Math.Abs(value):N0}원",
                "매니저 일급", data.managerSalary, value => $"-{Math.Abs(value):N0}원");
            yield return CountLine(growthText, growthLines, "EXP", data.experienceGained,
                value => $"+{Math.Abs(value):N0}");
            yield return AnimateExperience(growthText, growthLines, data);
            yield return Wait(sectionRevealDelay);

            SetSection(ratingText, true);
            yield return AnimateRating(result != null ? result.finalRating : 0f);
            yield return Wait(sectionRevealDelay);

            continueLabel.text = buttonLabel;
            continueButton.gameObject.SetActive(true);
            _reveal = null;
        }

        private IEnumerator CountLine(TMP_Text text, List<string> completed, string label, long target,
            Func<long, string> formatter, string finalSuffix = "")
        {
            text.text = Compose(completed, label);
            yield return Wait(labelRevealDelay);
            yield return Count(numberCountDuration, progress =>
            {
                long value = (long)Math.Round(target * progress);
                text.text = Compose(completed, $"{label}  {formatter(value)}");
            });
            completed.Add($"{label}  {formatter(target)}{finalSuffix}");
            text.text = Compose(completed);
        }

        private IEnumerator CountFloatLine(TMP_Text text, List<string> completed, string label, float target,
            Func<float, string> formatter)
        {
            text.text = Compose(completed, label);
            yield return Wait(labelRevealDelay);
            yield return Count(numberCountDuration, progress =>
                text.text = Compose(completed, $"{label}  {formatter(target * progress)}"));
            completed.Add($"{label}  {formatter(target)}");
            text.text = Compose(completed);
        }

        private IEnumerator CountDualLine(TMP_Text text, List<string> completed,
            string firstLabel, long firstTarget, Func<long, string> firstFormatter,
            string secondLabel, long secondTarget, Func<long, string> secondFormatter)
        {
            text.text = Compose(completed, $"{firstLabel}                         {secondLabel}");
            yield return Wait(labelRevealDelay);
            yield return Count(numberCountDuration, progress =>
            {
                long first = (long)Math.Round(firstTarget * progress);
                long second = (long)Math.Round(secondTarget * progress);
                text.text = Compose(completed,
                    $"{firstLabel}  {firstFormatter(first)}             {secondLabel}  {secondFormatter(second)}");
            });
            completed.Add($"{firstLabel}  {firstFormatter(firstTarget)}             {secondLabel}  {secondFormatter(secondTarget)}");
            text.text = Compose(completed);
        }

        private IEnumerator AnimateExperience(TMP_Text text, List<string> completed, RunnerSettlementDisplayData data)
        {
            int level = Mathf.Max(1, data.levelBefore > 0 ? data.levelBefore : data.levelAfter);
            int experience = Mathf.Max(0, data.experienceBefore);
            int targetLevel = Mathf.Max(level, data.levelAfter);
            int remaining = Mathf.Max(0, data.experienceGained);
            string levelLine = LevelLabel(level);
            text.text = Compose(completed, levelLine);
            SetGauge(experienceFill, ExperienceProgress(level, experience));
            yield return Wait(labelRevealDelay);

            float totalDuration = Mathf.Max(0.05f, gaugeFillDuration);
            int totalExperience = Mathf.Max(1, remaining);
            while (remaining > 0 && !IsMaximumLevel(level))
            {
                int required = ExperienceRequired(level);
                int step = Mathf.Min(remaining, Mathf.Max(0, required - experience));
                if (step <= 0)
                {
                    level++;
                    experience = 0;
                    continue;
                }
                float start = experience / (float)required;
                float end = (experience + step) / (float)required;
                float duration = totalDuration * step / totalExperience;
                int displayedLevel = level;
                yield return Count(duration, progress =>
                {
                    SetGauge(experienceFill, Mathf.Lerp(start, end, progress));
                    text.text = Compose(completed, LevelLabel(displayedLevel));
                });
                experience += step;
                remaining -= step;
                if (experience >= required && !IsMaximumLevel(level))
                {
                    level++;
                    experience = 0;
                    SetGauge(experienceFill, IsMaximumLevel(level) ? 1f : 0f);
                }
            }

            level = Mathf.Max(level, targetLevel);
            experience = Mathf.Max(0, data.experienceAfter);
            levelLine = LevelLabel(level);
            SetGauge(experienceFill, IsMaximumLevel(level) ? 1f : ExperienceProgress(level, experience));
            completed.Add(levelLine);
            text.text = Compose(completed);
        }

        private IEnumerator AnimateRating(float rating)
        {
            rating = Mathf.Clamp(rating, 0f, 5f);
            ratingText.text = "최종 방송 평점";
            yield return Wait(labelRevealDelay);
            yield return Count(gaugeFillDuration, progress =>
            {
                float current = rating * progress;
                SetGauge(ratingFill, current / 5f);
                ratingText.text = $"최종 방송 평점                                      {current:0.0} / 5.0";
            });
            ratingText.text = $"최종 방송 평점                                      {rating:0.0} / 5.0";
            SetGauge(ratingFill, rating / 5f);
        }

        private IEnumerator Count(float duration, Action<float> update)
        {
            duration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                update(Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            update(1f);
        }

        private int ExperienceRequired(int level)
        {
            BroadcasterLevelRule rule = campaignSettings?.broadcasterLevels?.Find(candidate => candidate != null && candidate.level == level);
            return Mathf.Max(1, rule != null ? rule.experienceToNextLevel : 100);
        }

        private float ExperienceProgress(int level, int experience) => Mathf.Clamp01(experience / (float)ExperienceRequired(level));
        private bool IsMaximumLevel(int level) => campaignSettings != null && level >= campaignSettings.maximumBroadcasterLevel;
        private string LevelLabel(int level) => IsMaximumLevel(level) ? "Lvl. MAX" : $"Lvl. {Mathf.Max(1, level)}";
        private static string FormatSigned(long value) => value > 0 ? $"+{value:N0}" : value.ToString("N0");
        private static string Compose(List<string> completed, string current = null) =>
            string.Join("\n", string.IsNullOrEmpty(current) ? completed : new List<string>(completed) { current });
        private static WaitForSecondsRealtime Wait(float seconds) => new WaitForSecondsRealtime(Mathf.Max(0f, seconds));

        private static void SetGauge(Image fill, float progress)
        {
            if (fill == null) return;
            RectTransform rect = fill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void Continue()
        {
            canvasGroup.interactable = false;
            Action callback = _onContinue;
            _onContinue = null;
            callback?.Invoke();
        }

        private static void SetSection(TMP_Text row, bool visible)
        {
            if (row == null) return;
            Transform section = row.transform.parent;
            if (section != null) section.gameObject.SetActive(visible);
            else row.gameObject.SetActive(visible);
        }

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
            long managerSalary = BroadcasterProgression.ApplyManagerSalary(settings, save);
            save.broadcastPending = false;
            save.awaitingAdvance = true;
            save.campaignFailed = false;
            int experience = settings.broadcastCompletionExperience
                + Mathf.RoundToInt((result != null ? result.finalRating : 0f) * settings.broadcastRatingExperiencePerPoint)
                + (rawScore > previousBest ? settings.newRecordExperience : 0);
            BroadcasterProgression.AddBroadcastExperience(settings, save, experience);
            experience = save.broadcastSessionExperienceEarned;
            int experienceAfter = save.broadcasterExperience;
            BroadcasterProgression.ExperienceStateBeforeGain(settings, save.broadcasterLevel, experienceAfter,
                experience, out int levelBefore, out int experienceBefore);
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
                gameTitle = "타일 아레나", score = score, rawGameScore = rawScore, broadcastScore = score, finalScore = score,
                broadcastCompleted = true,
                previousBestScore = previousBest, isNewRecord = rawScore > previousBest,
                experienceGained = experience, experienceBefore = experienceBefore, experienceAfter = experienceAfter,
                levelBefore = levelBefore, levelAfter = save.broadcasterLevel, targetScore = target, hitsTaken = hitsTaken,
                subscriberDelta = subscriberDelta, subscribersAfter = save.subscribers,
                mentalLevel = save.mentalLevel, cashAfter = save.cash, managerSalary = managerSalary,
                broadcastResult = result
            };
        }
    }
}
