using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    [Serializable]
    public sealed class RunnerBroadcastResult
    {
        public int startingFollowers;
        public int endingViewers;
        public int peakViewers;
        public int totalVisitors;
        public float averageViewers;
        public int chattingViewers;
        public float gameplayRating;
        public float survivalRating;
        public float safetyRating;
        public float combatRating;
        public float hostingRating;
        public float finalRating;
        public float followConversionRate;
        public int followersGained;
        public int followersLost;
        public int netFollowerChange;
        public int donationWon;
    }

    public sealed class RunnerBroadcastAudienceController : MonoBehaviour
    {
        public int CurrentViewers { get; private set; }
        public int ChattingViewers { get; private set; }
        public int PeakViewers { get; private set; }
        public int TotalVisitors { get; private set; }
        public float Hype { get; private set; }
        public int LiveDonationWon { get; private set; }
        public RunnerBroadcastResult LastResult { get; private set; }
        public int TalkingSkill => _talkingSkill;
        public bool CanShowWitInteraction => _running && _gameManager != null && _gameManager.BroadcastActive;

        private RunnerBroadcastGrowthSettings _settings;
        private RunnerGameManager _gameManager;
        private RunnerChatController _chat;
        private int _followers;
        private int _gameSkill;
        private int _talkingSkill;
        private float _nextViewerUpdate;
        private float _viewerSeconds;
        private bool _running;
        private float _nextDonationAt;
        private RunnerDonationPopupController _donationPopup;
        private RunnerWitInteractionController _witInteraction;
        private float _witHostingBonus;
        private float _witFollowBonus;

        public void Configure(RunnerBroadcastGrowthSettings settings, RunnerGameManager gameManager, RunnerChatController chat)
        {
            _settings = settings;
            _gameManager = gameManager;
            _chat = chat;
            if (_donationPopup == null) _donationPopup = FindFirstObjectByType<RunnerDonationPopupController>();
            if (_witInteraction == null) _witInteraction = FindFirstObjectByType<RunnerWitInteractionController>();
        }

        public void BeginBroadcast(int followers, int gameSkill, int talkingSkill)
        {
            if (_settings == null) return;
            _followers = Mathf.Max(0, followers);
            _gameSkill = Mathf.Max(1, gameSkill);
            _talkingSkill = Mathf.Max(1, talkingSkill);
            Hype = _settings.startingHype;
            CurrentViewers = Mathf.Max(0, Mathf.RoundToInt(_settings.baseDiscoveryViewers
                + _followers * _settings.followerNotificationRate
                + _talkingSkill * _settings.viewersPerTalkingSkill));
            PeakViewers = CurrentViewers;
            TotalVisitors = CurrentViewers;
            _viewerSeconds = 0f;
            _nextViewerUpdate = Time.time + _settings.viewerUpdateInterval;
            LastResult = null;
            LiveDonationWon = 0;
            _nextDonationAt = 0f;
            _witHostingBonus = 0f;
            _witFollowBonus = 0f;
            _running = true;
            RefreshChatScale();
        }

        private void Update()
        {
            if (!_running || _settings == null || _gameManager == null || !_gameManager.BroadcastActive) return;
            _viewerSeconds += CurrentViewers * Time.deltaTime;
            Hype = Mathf.MoveTowards(Hype, _settings.restingHype, _settings.hypeReturnPerSecond * Time.deltaTime);
            if (Time.time < _nextViewerUpdate) return;
            _nextViewerUpdate = Time.time + _settings.viewerUpdateInterval;
            UpdateViewerCount();
        }

        public void OnObstacleCleared()
        {
            AddHype(_settings != null ? _settings.obstacleClearedHype : 0f);
            _witInteraction?.NotifySafeMoment("러너에서 방금 장애물을 깔끔하게 통과함");
            TryLiveDonation(_settings != null ? _settings.obstacleClearDonationChance : 0f, "깔끔하게 피하네! 계속 가자");
        }

        public void OnEnemyDefeated()
        {
            AddHype(_settings != null ? _settings.enemyDefeatedHype : 0f);
            _witInteraction?.NotifySafeMoment("러너에서 방금 적을 정확한 타이밍에 처치함");
            TryLiveDonation(_settings != null ? _settings.enemyDefeatDonationChance : 0f, "방금 공격 타이밍 좋았다!");
        }
        public void OnAttackMissed() => AddHype(_settings != null ? _settings.attackMissedHype : 0f);
        public void OnPlayerHit(bool lowHealth)
        {
            if (_settings == null) return;
            AddHype(_settings.playerHitHype + (lowHealth ? _settings.lowHealthDramaHype : 0f));
        }

        public void OnAttemptDefeated()
        {
            AddHype(_settings != null ? _settings.defeatedHype : 0f);
            _witInteraction?.NotifySafeMoment("러너 게임오버 직후 방금 판을 되짚는 중", 8f);
        }

        public RunnerBroadcastResult FinishBroadcast(int score, int targetScore, int hitsTaken, int enemiesDefeated,
            float elapsedSeconds, float durationSeconds, bool completed)
        {
            if (_settings == null) return new RunnerBroadcastResult { startingFollowers = _followers };
            if (completed) AddHype(_settings.completedBroadcastHype);
            UpdateViewerCount();
            _running = false;
            float elapsed = Mathf.Max(1f, elapsedSeconds);
            float average = _viewerSeconds / elapsed;
            float scoreRatio = Mathf.Clamp01(score / (float)Mathf.Max(1, targetScore));
            float survivalRatio = completed ? 1f : Mathf.Clamp01(elapsedSeconds / Mathf.Max(1f, durationSeconds));
            float gameplayRating = 1f + scoreRatio * 4f;
            float survivalRating = 1f + survivalRatio * 4f;
            float safetyRating = Mathf.Clamp(5f - hitsTaken * 1.15f, 1f, 5f);
            float combatRating = Mathf.Clamp(2.5f + enemiesDefeated * 0.35f - hitsTaken * 0.35f, 1f, 5f);
            float hostingRating = Mathf.Clamp(2.2f + (_talkingSkill - 1) * 0.28f + Hype / 100f * 1.6f
                + _witHostingBonus, 1f, 5f);
            float weight = Mathf.Max(0.01f, _settings.gameplayRatingWeight + _settings.survivalRatingWeight
                + _settings.safetyRatingWeight + _settings.combatRatingWeight + _settings.hostingRatingWeight);
            float finalRating = (gameplayRating * _settings.gameplayRatingWeight + survivalRating * _settings.survivalRatingWeight
                + safetyRating * _settings.safetyRatingWeight + combatRating * _settings.combatRatingWeight
                + hostingRating * _settings.hostingRatingWeight) / weight;
            finalRating = Mathf.Clamp(finalRating, 1f, 5f);

            float conversion = _settings.baseFollowConversion
                + finalRating * _settings.followConversionPerRatingPoint
                + Mathf.Max(0, _talkingSkill - 1) * _settings.followConversionPerTalkingLevel
                + _witFollowBonus
                + (completed ? _settings.completionFollowBonus : 0f);
            conversion = Mathf.Clamp(conversion, 0f, _settings.maximumFollowConversion);
            int gained = Mathf.Max(0, Mathf.RoundToInt(TotalVisitors * conversion));
            int lost = finalRating < _settings.unfollowRatingThreshold
                ? Mathf.Min(_followers, Mathf.CeilToInt(_followers * (_settings.unfollowRatingThreshold - finalRating)
                    * _settings.unfollowRatePerMissingRatingPoint))
                : 0;
            float donationVariation = UnityEngine.Random.Range(1f - _settings.donationRandomVariation, 1f + _settings.donationRandomVariation);
            int donation = LiveDonationWon + Mathf.Max(0, Mathf.RoundToInt(average * finalRating
                * (1f + Mathf.Max(0, _talkingSkill - 1) * _settings.donationBonusPerTalkingLevel)
                * _settings.wonPerViewerRatingPoint * donationVariation));

            LastResult = new RunnerBroadcastResult
            {
                startingFollowers = _followers,
                endingViewers = CurrentViewers,
                peakViewers = PeakViewers,
                totalVisitors = TotalVisitors,
                averageViewers = average,
                chattingViewers = ChattingViewers,
                gameplayRating = gameplayRating,
                survivalRating = survivalRating,
                safetyRating = safetyRating,
                combatRating = combatRating,
                hostingRating = hostingRating,
                finalRating = finalRating,
                followConversionRate = conversion,
                followersGained = gained,
                followersLost = lost,
                netFollowerChange = gained - lost,
                donationWon = donation
            };
            RefreshChatScale();
            return LastResult;
        }

        private void UpdateViewerCount()
        {
            if (_settings == null) return;
            float target = _settings.baseDiscoveryViewers
                + _followers * _settings.followerNotificationRate
                + Hype * _settings.viewersPerHypePoint
                + _gameSkill * _settings.viewersPerGameSkill
                + _talkingSkill * _settings.viewersPerTalkingSkill;
            target *= UnityEngine.Random.Range(1f - _settings.viewerRandomVariation, 1f + _settings.viewerRandomVariation);
            int next = Mathf.Max(0, Mathf.RoundToInt(Mathf.Lerp(CurrentViewers, target, _settings.viewerAdjustmentRate)));
            if (next == CurrentViewers && CurrentViewers <= _settings.idleFluctuationMaximumViewers
                && UnityEngine.Random.value < _settings.idleViewerFluctuationChance)
                next = Mathf.Max(0, next + (UnityEngine.Random.value < 0.5f ? -1 : 1));
            if (next > CurrentViewers) TotalVisitors += next - CurrentViewers;
            CurrentViewers = next;
            PeakViewers = Mathf.Max(PeakViewers, CurrentViewers);
            RefreshChatScale();
        }

        private void AddHype(float amount) => Hype = Mathf.Clamp(Hype + amount, 0f, 100f);

        public void ApplyWitInteraction(int quality)
        {
            if (!_running || _settings == null) return;
            if (quality >= 2)
            {
                float levelThreeMultiplier = quality >= 3 ? 1.35f : 1f;
                AddHype(_settings.witSuccessHype * levelThreeMultiplier);
                _witHostingBonus = Mathf.Min(_settings.maximumWitHostingBonus,
                    _witHostingBonus + _settings.witHostingRatingBonus * levelThreeMultiplier);
                _witFollowBonus = Mathf.Min(_settings.maximumWitFollowBonus,
                    _witFollowBonus + _settings.witFollowConversionBonus * levelThreeMultiplier);
                TryLiveDonation(_settings.witSuccessDonationChance, "이런 받아치기 좋다 ㅋㅋ");
                _chat?.React(RunnerChatEvent.WitReplySuccess);
            }
            else if (quality == 1)
            {
                AddHype(_settings.witOkayHype);
                _chat?.React(RunnerChatEvent.WitReplyOkay);
            }
            else
            {
                AddHype(_settings.witAwkwardHype);
                _chat?.React(RunnerChatEvent.WitReplyAwkward);
            }
            RefreshChatScale();
        }

        private void TryLiveDonation(float baseChance, string message)
        {
            if (!_running || _settings == null || CurrentViewers < _settings.minimumViewersForDonation
                || Time.unscaledTime < _nextDonationAt) return;
            float chance = baseChance * (1f + Mathf.Max(0, _talkingSkill - 1) * _settings.donationChancePerTalkingLevel);
            if (UnityEngine.Random.value > chance) return;
            _nextDonationAt = Time.unscaledTime + _settings.liveDonationCooldown;
            int amount = RollDonationAmount(_settings);
            string donor = _chat != null ? _chat.PickDonationViewerNickname() : "익명의 시청자";
            LiveDonationWon += amount;
            _donationPopup?.ShowDonation(donor, amount, message);
            _chat?.OnDonationReceived(donor, amount);
        }

        public static int RollDonationAmount(RunnerBroadcastGrowthSettings settings)
        {
            float roll = UnityEngine.Random.value;
            if (roll < settings.largeDonationChance) return settings.largeDonationWon;
            if (roll < settings.largeDonationChance + settings.mediumDonationChance) return settings.mediumDonationWon;
            return settings.smallDonationWon;
        }

        private void RefreshChatScale()
        {
            ChattingViewers = _settings != null ? _settings.ChattersForViewers(CurrentViewers) : 0;
            _chat?.ConfigureAudience(CurrentViewers, ChattingViewers,
                _settings != null ? _settings.ChatDelayMultiplierForViewers(CurrentViewers) : 1f,
                _settings != null ? _settings.EventReactionChanceForViewers(CurrentViewers) : 1f,
                _settings != null ? _settings.EventCooldownForViewers(CurrentViewers) : 0f);
        }
    }

    public sealed class RunnerDonationPopupController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text donorText;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField, Min(0.1f)] private float fadeSeconds = 0.18f;
        [SerializeField, Min(0.5f)] private float visibleSeconds = 2.8f;

        private readonly Queue<DonationNotice> _pending = new Queue<DonationNotice>();
        private Coroutine _pump;

        public void ShowDonation(string donor, int amount, string message)
        {
            _pending.Enqueue(new DonationNotice { donor = donor, amount = amount, message = message });
            if (_pump == null) _pump = StartCoroutine(Pump());
        }

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        private IEnumerator Pump()
        {
            while (_pending.Count > 0)
            {
                DonationNotice notice = _pending.Dequeue();
                if (donorText != null) donorText.text = $"{notice.donor}님이";
                if (amountText != null) amountText.text = $"{notice.amount:N0}원을 후원해 주셨어요!";
                if (messageText != null) messageText.text = notice.message;
                yield return Fade(0f, 1f);
                yield return new WaitForSecondsRealtime(visibleSeconds);
                yield return Fade(1f, 0f);
            }
            _pump = null;
        }

        private IEnumerator Fade(float from, float to)
        {
            if (canvasGroup == null) yield break;
            float startedAt = Time.unscaledTime;
            while (Time.unscaledTime - startedAt < fadeSeconds)
            {
                canvasGroup.alpha = Mathf.Lerp(from, to, (Time.unscaledTime - startedAt) / fadeSeconds);
                yield return null;
            }
            canvasGroup.alpha = to;
        }

        private struct DonationNotice
        {
            public string donor;
            public int amount;
            public string message;
        }
    }
}
