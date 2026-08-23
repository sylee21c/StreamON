using System.Collections;
using StreamOn.Minigames.Runner;
using UnityEngine;

namespace StreamOn.Minigames.TileArena
{
    public sealed class TileArenaChatAdapter : MonoBehaviour
    {
        [Header("Shared Chat References")]
        [SerializeField] private TileArenaController gameController;
        [SerializeField] private RunnerChatController chatController;
        [SerializeField] private RunnerBroadcastGrowthSettings growthSettings;
        [SerializeField] private RunnerCampaignSettings campaignSettings;
        [SerializeField] private RunnerDonationPopupController donationPopup;

        [Header("Editable Tile Arena Audience")]
        [SerializeField, Min(0)] private int startingViewers = 3;
        [SerializeField, Min(0)] private int maximumViewers = 500;
        [SerializeField, Min(0f)] private float viewersPerScore = 0.18f;
        [SerializeField, Min(0f)] private float viewersPerHypePoint = 0.10f;
        [SerializeField, Min(0.5f)] private float viewerUpdateInterval = 5f;
        [SerializeField, Range(0.01f, 1f)] private float viewerAdjustmentRate = 0.35f;
        [SerializeField, Range(0f, 0.5f)] private float randomVariation = 0.12f;

        [Header("Editable Hype Reactions")]
        [SerializeField, Range(0f, 100f)] private float startingHype = 50f;
        [SerializeField, Range(0f, 100f)] private float restingHype = 50f;
        [SerializeField, Min(0f)] private float hypeReturnPerSecond = 0f;
        [SerializeField] private float pickupHype = 0.7f;
        [SerializeField] private float stageClearHype = 6f;
        [SerializeField] private float playerHitHype = -7f;
        [SerializeField] private float lowLivesDramaHype = 3f;

        private int _currentViewers;
        private int _chattingViewers;
        private int _peakViewers;
        private float _hype;
        private float _nextViewerUpdate;
        private bool _initialized;
        private float _nextDonationAt;
        private float _nextAmbientDonationAt;
        private int _liveDonationWon;
        private int _talkingSkill = 1;
        private int _mentalLevel = 1;
        private RunnerWitInteractionController _witInteraction;
        private int _followers;
        private int _gameSkill = 1;
        private int _microphoneLevel = 1;
        private int _interiorLevel = 1;
        private int _totalVisitors;
        private float _viewerSeconds;
        private bool _broadcastSessionActive;
        private float _socialFollowerPenalty;
        private int _moderationFollowerBonus;
        private readonly RunnerBroadcastPerformanceMeter _performanceMeter = new RunnerBroadcastPerformanceMeter();
        private float _bufferedHypeChange;
        private bool _cleanMistakeProtectionUsed;
        private bool _largePenaltyProtectionUsed;
        private float _nextMistakeClearAt;

        public int CurrentViewers => _currentViewers;
        public int PeakViewers => _peakViewers;
        public int TalkingSkill => _talkingSkill;
        public bool CanShowWitInteraction => _initialized && gameController != null && gameController.IsRunning;
        public int LiveDonationWon => _liveDonationWon;
        public float Hype => _hype;
        public float ViewerExitDuration => growthSettings != null ? growthSettings.viewerExitDuration : 3.2f;

        private void Start() => InitializeIfNeeded();

        private void Update()
        {
            InitializeIfNeeded();
            if (!_initialized || gameController == null) return;
            if (_broadcastSessionActive) _viewerSeconds += _currentViewers * Time.unscaledDeltaTime;
            if (_broadcastSessionActive) TryAmbientDonation();
            if (_broadcastSessionActive && gameController.IsRunning)
            {
                MentalRankRule mental = CurrentMentalRule();
                float performanceStep = _performanceMeter.Tick(Time.time, growthSettings,
                    mental != null ? mental.poorStateTickInterval : -1f,
                    mental != null ? mental.extraMistakesRequiredForPoorState : 0,
                    mental != null ? mental.neutralRecoveryTimeReduction : 0f);
                if (!Mathf.Approximately(performanceStep, 0f)) ApplyPerformanceHeatStep(performanceStep);
                if (_performanceMeter.State != BroadcastPerformanceState.Good) _cleanMistakeProtectionUsed = false;
                ApplyBufferedHype(Time.deltaTime);
                _hype = Mathf.MoveTowards(_hype, restingHype, hypeReturnPerSecond * Time.unscaledDeltaTime);
                RunnerBroadcastHeatGauge.SetValue(_hype);
            }
            if (_broadcastSessionActive && Time.unscaledTime >= _nextViewerUpdate)
            {
                _nextViewerUpdate = Time.unscaledTime + viewerUpdateInterval;
                UpdateAudience();
            }
            PushSnapshot();
        }

        public void OnGameStarted()
        {
            InitializeIfNeeded();
            if (!_initialized) return;
            chatController.ResumeExternalGame();
            RefreshChatScale();
            PushSnapshot(SharedChatGameState.Playing);
            chatController.React(RunnerChatEvent.TileArenaStarted);
        }

        public void OnJumped() => React(RunnerChatEvent.TileArenaJumped, 0f);
        public void OnBluePickedUp(int count)
        {
            React(RunnerChatEvent.TileArenaPickup, pickupHype * Mathf.Max(1, count));
            if (count >= 2) TryLiveDonation(growthSettings != null ? growthSettings.tilePickupDonationChance : 0f,
                "한 번에 여러 개 먹는 거 시원하다");
        }

        public void OnStageCleared()
        {
            React(RunnerChatEvent.TileArenaStageCleared, stageClearHype);
            GrantGameplayExperience(campaignSettings != null ? campaignSettings.tileStageClearExperience : 0);
            _witInteraction?.NotifySafeMoment("타일 아레나 스테이지를 방금 클리어함", 4f);
            TryLiveDonation(growthSettings != null ? growthSettings.tileStageClearDonationChance : 0f,
                "파란 타일 올클리어!");
        }

        public void OnPlayerHit(bool lowLives)
        {
            MentalRankRule mental = CurrentMentalRule();
            if (mental != null && mental.protectsFirstMistakeAfterGoodPlay
                && _performanceMeter.State == BroadcastPerformanceState.Good && !_cleanMistakeProtectionUsed)
            {
                _cleanMistakeProtectionUsed = true;
                PushSnapshot();
                chatController?.React(RunnerChatEvent.TileArenaPlayerHit);
                if (lowLives) chatController?.React(RunnerChatEvent.TileArenaLowLives);
                return;
            }
            React(RunnerChatEvent.TileArenaPlayerHit, playerHitHype + (lowLives ? lowLivesDramaHype : 0f));
            if (lowLives) chatController?.React(RunnerChatEvent.TileArenaLowLives);
        }

        public void OnGameOver(bool isNewHighScore)
        {
            InitializeIfNeeded();
            if (!_initialized) return;
            PushSnapshot(SharedChatGameState.GameOver);
            chatController.React(RunnerChatEvent.TileArenaGameOver);
            if (isNewHighScore) AddHype(growthSettings != null ? growthSettings.newHighScoreHype : 8f);
            _witInteraction?.NotifySafeMoment("타일 아레나 게임오버 직후 방금 판을 되짚는 중", 8f);
            chatController.BeginExternalGameOverChat(isNewHighScore);
        }

        public void OnModerationResult(bool correct) => AddHype(growthSettings == null ? 0f
            : correct ? growthSettings.correctModerationHype : growthSettings.wrongModerationHype, correct ? 1f : 0f);

        public void OnFraternizationTick()
        {
            AddHype(growthSettings != null ? growthSettings.fraternizationOngoingHype : 0f, .5f);
            int leaving = Mathf.Max(growthSettings.socialMinimumViewerLeave,
                Mathf.CeilToInt(_currentViewers * growthSettings.socialViewerLeaveFraction));
            _currentViewers = Mathf.Max(0, _currentViewers - leaving);
            _socialFollowerPenalty += Mathf.Max(growthSettings.socialFollowerPenaltyMinimum,
                _followers * growthSettings.socialFollowerPenaltyPerFollower);
            RefreshChatScale();
            PushSnapshot();
        }

        public void OnFraternizationResolved(float responseSeconds)
        {
            float quick = growthSettings != null ? Mathf.InverseLerp(growthSettings.slowFraternizationResponseSeconds,
                growthSettings.quickFraternizationResponseSeconds, responseSeconds) : 0f;
            AddHype(growthSettings != null ? Mathf.Lerp(growthSettings.slowFraternizationResolutionHype,
                growthSettings.quickFraternizationResolutionHype, quick) : 0f);
            int returning = Mathf.Max(growthSettings.socialMinimumViewerLeave, Mathf.CeilToInt(_currentViewers
                * Mathf.Lerp(growthSettings.socialResolutionViewerReturnMinimum,
                    growthSettings.socialResolutionViewerReturnMaximum, quick)));
            _currentViewers = Mathf.Min(maximumViewers, _currentViewers + returning);
            _totalVisitors += returning;
            _peakViewers = Mathf.Max(_peakViewers, _currentViewers);
            _moderationFollowerBonus += Mathf.RoundToInt(Mathf.Lerp(0f, growthSettings.socialResolutionFollowerBonusMaximum, quick));
            RefreshChatScale();
            PushSnapshot();
        }

        private void InitializeIfNeeded()
        {
            if (_initialized) return;
            if (gameController == null) gameController = FindFirstObjectByType<TileArenaController>();
            if (chatController == null) chatController = FindFirstObjectByType<RunnerChatController>();
            if (gameController == null || chatController == null) return;
            if (donationPopup == null) donationPopup = FindFirstObjectByType<RunnerDonationPopupController>();
            if (_witInteraction == null) _witInteraction = FindFirstObjectByType<RunnerWitInteractionController>();
            if (campaignSettings != null && RunnerCampaignSaveStore.TryLoad(campaignSettings, out RunnerCampaignSaveData save))
            {
                _talkingSkill = 1;
                _gameSkill = 0;
                _mentalLevel = 1;
                _followers = Mathf.Max(0, save.subscribers);
                _microphoneLevel = Mathf.Clamp(save.microphoneLevel, 1, 3);
                _interiorLevel = Mathf.Clamp(save.interiorLevel, 1, 3);
            }
            float initial = startingViewers + (growthSettings != null ? _followers * growthSettings.followerNotificationRate : 0f)
                + (campaignSettings != null ? Mathf.Max(0, _interiorLevel - 1) * campaignSettings.startingViewersPerInteriorUpgrade : 0f);
            _currentViewers = Mathf.Clamp(Mathf.RoundToInt(initial), 0, maximumViewers);
            _peakViewers = _currentViewers;
            _totalVisitors = _currentViewers;
            _hype = startingHype;
            _liveDonationWon = 0;
            _nextDonationAt = 0f;
            _nextViewerUpdate = Time.unscaledTime + viewerUpdateInterval;
            chatController.BindExternalGame("타일 아레나");
            RefreshChatScale();
            PushSnapshot(SharedChatGameState.Ready);
            _initialized = true;
        }

        public void BeginBroadcastSession()
        {
            InitializeIfNeeded();
            _broadcastSessionActive = true;
            _hype = growthSettings != null ? growthSettings.startingHype : startingHype;
            _performanceMeter.Reset(Time.time);
            _bufferedHypeChange = 0f;
            _cleanMistakeProtectionUsed = false;
            _largePenaltyProtectionUsed = false;
            _nextMistakeClearAt = 0f;
            _socialFollowerPenalty = 0f;
            _moderationFollowerBonus = 0;
            _viewerSeconds = 0f;
            _totalVisitors = _currentViewers;
            _peakViewers = _currentViewers;
            _liveDonationWon = 0;
            _nextDonationAt = 0f;
            ScheduleNextAmbientDonation();
            RunnerBroadcastHeatGauge.Show(_hype);
            RefreshChatScale();
            PushSnapshot();
        }

        public RunnerBroadcastResult FinishBroadcast(int score, int targetScore, int hitsTaken, float elapsedSeconds, float durationSeconds)
        {
            InitializeIfNeeded();
            _broadcastSessionActive = false;
            float elapsed = Mathf.Max(1f, elapsedSeconds);
            float average = _viewerSeconds / elapsed;
            float scoreRatio = Mathf.Clamp01(score / (float)Mathf.Max(1, targetScore));
            float gameplayRating = 1f + scoreRatio * 4f;
            float survivalRating = 1f + Mathf.Clamp01(elapsedSeconds / Mathf.Max(1f, durationSeconds)) * 4f;
            float safetyRating = Mathf.Clamp(5f - hitsTaken * growthSettings.tileSafetyPenaltyPerHit, 1f, 5f);
            float finalHeat = Mathf.Clamp(_hype + _bufferedHypeChange, 0f, 100f);
            float hostingRating = Mathf.Clamp(growthSettings.tileHostingRatingBase
                + finalHeat / 100f * growthSettings.tileHostingRatingHeatRange, 1f, 5f);
            float weight = Mathf.Max(0.01f, growthSettings.gameplayRatingWeight + growthSettings.survivalRatingWeight
                + growthSettings.safetyRatingWeight + growthSettings.hostingRatingWeight);
            float finalRating = Mathf.Clamp((gameplayRating * growthSettings.gameplayRatingWeight
                + survivalRating * growthSettings.survivalRatingWeight + safetyRating * growthSettings.safetyRatingWeight
                + hostingRating * growthSettings.hostingRatingWeight) / weight, 1f, 5f);
            float conversion = growthSettings.baseFollowConversion + finalRating * growthSettings.followConversionPerRatingPoint
                + Mathf.Max(0, _microphoneLevel - 1) * (campaignSettings != null ? campaignSettings.followerConversionBonusPerMicrophoneUpgrade : 0f)
                + growthSettings.completionFollowBonus;
            float heat01 = finalHeat / 100f;
            conversion *= Mathf.Lerp(0f, growthSettings.followHeatMaximumMultiplier,
                Mathf.InverseLerp(growthSettings.followHeatMinimum, growthSettings.followHeatMaximum, finalHeat));
            conversion = Mathf.Clamp(conversion, 0f, growthSettings.maximumFollowConversion);
            int gained = Mathf.Max(0, Mathf.RoundToInt(_totalVisitors * conversion) + _moderationFollowerBonus);
            int lost = finalRating < growthSettings.unfollowRatingThreshold
                ? Mathf.Min(_followers, Mathf.CeilToInt(_followers * (growthSettings.unfollowRatingThreshold - finalRating)
                    * growthSettings.unfollowRatePerMissingRatingPoint)) : 0;
            if (finalHeat < growthSettings.lowHeatUnfollowThreshold)
                lost = Mathf.Min(_followers, lost + Mathf.CeilToInt(_followers
                    * Mathf.InverseLerp(growthSettings.lowHeatUnfollowThreshold, 0f, finalHeat)
                    * growthSettings.lowHeatUnfollowRate));
            lost = Mathf.Min(_followers, lost + Mathf.CeilToInt(_socialFollowerPenalty));
            float donationMultiplier = 1f + Mathf.Max(0, _microphoneLevel - 1) * (campaignSettings != null ? campaignSettings.donationBonusPerMicrophoneUpgrade : 0f);
            int donation = _liveDonationWon + Mathf.Max(0, Mathf.RoundToInt(average * finalRating
                * donationMultiplier * growthSettings.wonPerViewerRatingPoint
                * Mathf.Lerp(growthSettings.donationValueMultiplierAtZeroHeat,
                    growthSettings.donationValueMultiplierAtFullHeat, heat01)));
            return new RunnerBroadcastResult
            {
                startingFollowers = _followers, endingViewers = _currentViewers, peakViewers = _peakViewers,
                totalVisitors = _totalVisitors, averageViewers = average, chattingViewers = _chattingViewers,
                gameplayRating = gameplayRating, survivalRating = survivalRating, safetyRating = safetyRating,
                combatRating = 0f, hostingRating = hostingRating, finalRating = finalRating,
                followConversionRate = conversion, followersGained = gained, followersLost = lost,
                netFollowerChange = gained - lost, donationWon = donation
            };
        }

        private void React(RunnerChatEvent chatEvent, float hypeDelta)
        {
            InitializeIfNeeded();
            if (!_initialized) return;
            if (hypeDelta > 0f) _performanceMeter.RecordSuccess(Time.time, growthSettings);
            else if (hypeDelta < 0f) _performanceMeter.RecordMistake(Time.time, growthSettings);
            PushSnapshot();
            chatController.React(chatEvent);
        }

        public void ApplyWitInteraction(int quality)
        {
            InitializeIfNeeded();
            if (!_initialized || growthSettings == null) return;
            if (quality >= 2)
            {
                WitRankRule wit = null;
                MentalRankRule mental = null;
                if (campaignSettings != null && RunnerCampaignSaveStore.TryLoad(campaignSettings, out RunnerCampaignSaveData save))
                {
                    wit = campaignSettings.WitRule(save.witRank);
                    mental = campaignSettings.MentalRule(save.mentalRank);
                }
                float perkMultiplier = quality >= 5 ? (wit != null ? wit.comebackRewardMultiplier : 1f)
                    : quality >= 4 ? (wit != null ? wit.correctStreakRewardMultiplier : 1f)
                    : quality >= 3 ? (wit != null ? wit.advancedAnswerRewardMultiplier : 1f) : 1f;
                float multiplier = (1f + (wit != null ? wit.correctHeatGainBonus : 0f)) * perkMultiplier;
                AddHype(growthSettings.witSuccessHype * multiplier);
                if (mental != null && mental.correctWitClearsRecentMistakes && Time.unscaledTime >= _nextMistakeClearAt)
                {
                    _performanceMeter.ClearRecentMistakes();
                    _nextMistakeClearAt = Time.unscaledTime + mental.mistakeClearCooldownSeconds;
                }
                TryLiveDonation(growthSettings.witSuccessDonationChance,
                    "이런 받아치기 좋다 ㅋㅋ");
                chatController.React(RunnerChatEvent.WitReplySuccess);
            }
            else if (quality == 1)
            {
                AddHype(growthSettings.witOkayHype);
                chatController.React(RunnerChatEvent.WitReplyOkay);
            }
            else
            {
                AddHype(growthSettings.witAwkwardHype);
                chatController.React(RunnerChatEvent.WitReplyAwkward);
            }
            UpdateAudience();
            PushSnapshot();
        }

        private void UpdateAudience()
        {
            float target = startingViewers + gameController.Score * viewersPerScore + _hype * viewersPerHypePoint
                + _followers * (growthSettings != null ? growthSettings.followerNotificationRate : 0f)
                + _gameSkill * (growthSettings != null ? growthSettings.viewersPerGameSkill : 0f)
                + _talkingSkill * (growthSettings != null ? growthSettings.viewersPerTalkingSkill : 0f)
                + Mathf.Max(0, _interiorLevel - 1) * (campaignSettings != null ? campaignSettings.startingViewersPerInteriorUpgrade : 0f);
            float heat01 = _hype / 100f;
            target *= Mathf.Lerp(growthSettings.viewerTargetMultiplierAtZeroHeat,
                growthSettings.viewerTargetMultiplierAtFullHeat, heat01);
            target *= Random.Range(1f - randomVariation, 1f + randomVariation);
            bool growing = target >= _currentViewers;
            float adjustment = viewerAdjustmentRate * (growing
                ? Mathf.Lerp(growthSettings.viewerGrowthRateMultiplierAtZeroHeat,
                    growthSettings.viewerGrowthRateMultiplierAtFullHeat, heat01)
                : Mathf.Lerp(growthSettings.viewerDeclineRateMultiplierAtZeroHeat,
                    growthSettings.viewerDeclineRateMultiplierAtFullHeat, heat01));
            int next = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(_currentViewers, target,
                Mathf.Clamp01(adjustment))), 0, maximumViewers);
            if (next == _currentViewers && _currentViewers > 0 && _currentViewers < maximumViewers && Random.value < 0.45f)
                next += Random.value < 0.5f ? -1 : 1;
            if (next > _currentViewers) _totalVisitors += next - _currentViewers;
            _currentViewers = Mathf.Clamp(next, 0, maximumViewers);
            _peakViewers = Mathf.Max(_peakViewers, _currentViewers);
            RefreshChatScale();
        }

        private void RefreshChatScale()
        {
            _chattingViewers = growthSettings != null ? growthSettings.ChattersForViewers(_currentViewers)
                : (_currentViewers <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(1.2f + Mathf.Sqrt(_currentViewers)), 1, Mathf.Min(_currentViewers, 40)));
            chatController.ConfigureAudience(_currentViewers, _chattingViewers,
                growthSettings != null ? growthSettings.ChatDelayMultiplierForViewers(_currentViewers) : 1f,
                (growthSettings != null ? growthSettings.EventReactionChanceForViewers(_currentViewers) : 0.55f)
                    * Mathf.Lerp(growthSettings.eventReactionMultiplierAtZeroHeat,
                        growthSettings.eventReactionMultiplierAtFullHeat, _hype / 100f),
                growthSettings != null ? growthSettings.EventCooldownForViewers(_currentViewers) : 2f, _hype);
        }

        private void TryLiveDonation(float chance, string message)
        {
            if (growthSettings == null || _currentViewers < growthSettings.minimumViewersForDonation
                || Time.unscaledTime < _nextDonationAt) return;
            chance *= Mathf.Lerp(growthSettings.donationEventChanceMultiplierAtZeroHeat,
                growthSettings.donationEventChanceMultiplierAtFullHeat, _hype / 100f);
            if (Random.value > chance) return;
            GrantLiveDonation(message);
        }

        private void TryAmbientDonation()
        {
            if (growthSettings == null || !growthSettings.enableAmbientDonations
                || Time.unscaledTime < _nextAmbientDonationAt) return;
            if (_currentViewers >= growthSettings.minimumViewersForDonation && Time.unscaledTime >= _nextDonationAt)
                GrantLiveDonation(PickAmbientDonationMessage());
            else
                ScheduleNextAmbientDonation();
        }

        private void GrantLiveDonation(string message)
        {
            _nextDonationAt = Time.unscaledTime + growthSettings.liveDonationCooldown;
            ScheduleNextAmbientDonation();
            int amount = RunnerBroadcastAudienceController.RollDonationAmount(growthSettings, _hype);
            string donor = chatController.PickDonationViewerNickname();
            _liveDonationWon += amount;
            donationPopup?.ShowDonation(donor, amount, message);
            chatController.OnDonationReceived(donor, amount, message, amount == growthSettings.largeDonationWon);
        }

        private void ScheduleNextAmbientDonation()
        {
            if (growthSettings == null) { _nextAmbientDonationAt = float.PositiveInfinity; return; }
            float minimum = Mathf.Max(growthSettings.liveDonationCooldown, growthSettings.ambientDonationMinimumInterval);
            float maximum = Mathf.Max(minimum, growthSettings.ambientDonationMaximumInterval);
            float intervalMultiplier = Mathf.Lerp(growthSettings.donationIntervalMultiplierAtZeroHeat,
                growthSettings.donationIntervalMultiplierAtFullHeat, _hype / 100f);
            minimum *= intervalMultiplier;
            maximum *= intervalMultiplier;
            _nextAmbientDonationAt = Time.unscaledTime + Random.Range(minimum, maximum);
        }

        private void AddHype(float amount, float mentalReductionScale = 1f)
        {
            if (growthSettings == null) return;
            if (amount < 0f && campaignSettings != null
                && RunnerCampaignSaveStore.TryLoad(campaignSettings, out RunnerCampaignSaveData save))
            {
                MentalRankRule mental = campaignSettings.MentalRule(save.mentalRank);
                float reduction = (mental?.ordinaryPenaltyReduction ?? 0f) * Mathf.Clamp01(mentalReductionScale);
                if (!_largePenaltyProtectionUsed && Mathf.Abs(amount) >= campaignSettings.largeHeatPenaltyThreshold
                    && mental != null && mental.oncePerBroadcastLargePenaltyReduction > 0f && mentalReductionScale > 0f)
                {
                    reduction = Mathf.Max(reduction, mental.oncePerBroadcastLargePenaltyReduction);
                    _largePenaltyProtectionUsed = true;
                }
                amount *= 1f - reduction;
            }
            _bufferedHypeChange = Mathf.Clamp(_bufferedHypeChange + amount,
                -growthSettings.maximumBufferedHeatChange, growthSettings.maximumBufferedHeatChange);
        }

        private MentalRankRule CurrentMentalRule()
        {
            if (campaignSettings == null || !RunnerCampaignSaveStore.TryLoad(campaignSettings, out RunnerCampaignSaveData save)) return null;
            return campaignSettings.MentalRule(save.mentalRank);
        }

        private void GrantGameplayExperience(int amount)
        {
            if (amount <= 0 || campaignSettings == null
                || !RunnerCampaignSaveStore.TryLoad(campaignSettings, out RunnerCampaignSaveData save)) return;
            BroadcasterProgression.AddBroadcastExperience(campaignSettings, save, amount);
            RunnerCampaignSaveStore.Save(campaignSettings, save, true);
        }

        private void ApplyBufferedHype(float deltaTime)
        {
            if (growthSettings == null || Mathf.Approximately(_bufferedHypeChange, 0f)) return;
            float amount = Mathf.Sign(_bufferedHypeChange) * Mathf.Min(Mathf.Abs(_bufferedHypeChange),
                growthSettings.eventHeatChangePerSecond * deltaTime);
            _hype = Mathf.Clamp(_hype + amount, 0f, 100f);
            _bufferedHypeChange -= amount;
            if ((_hype <= 0f && _bufferedHypeChange < 0f) || (_hype >= 100f && _bufferedHypeChange > 0f))
                _bufferedHypeChange = 0f;
        }

        private void ApplyPerformanceHeatStep(float amount)
        {
            _hype = Mathf.Clamp(_hype + amount, 0f, 100f);
        }

        public IEnumerator DrainViewersToZero(float duration)
        {
            _broadcastSessionActive = false;
            int startingAudience = _currentViewers;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _currentViewers = Mathf.Max(0, Mathf.CeilToInt(Mathf.Lerp(startingAudience, 0f,
                    Mathf.Clamp01(elapsed / Mathf.Max(0.1f, duration)))));
                RefreshChatScale();
                PushSnapshot(SharedChatGameState.GameOver);
                yield return null;
            }
            _currentViewers = 0;
            RefreshChatScale();
            PushSnapshot(SharedChatGameState.GameOver);
            RunnerBroadcastHeatGauge.Hide();
        }

        private string PickAmbientDonationMessage()
        {
            string[] messages = growthSettings != null ? growthSettings.ambientDonationMessages : null;
            return messages != null && messages.Length > 0
                ? messages[Random.Range(0, messages.Length)]
                : "방송 잘 보고 있어요!";
        }

        private void PushSnapshot(SharedChatGameState? forcedState = null)
        {
            if (chatController == null || gameController == null) return;
            SharedChatGameState state = forcedState ?? (gameController.IsRunning
                ? SharedChatGameState.Playing
                : gameController.Lives <= 0 ? SharedChatGameState.GameOver : SharedChatGameState.Ready);
            chatController.UpdateExternalGame(state, new RunnerChatSnapshot
            {
                gameTitle = "타일 아레나",
                score = gameController.Score,
                highScore = gameController.BestScore,
                health = gameController.Lives,
                maxHealth = gameController.MaximumLives,
                blueTilesRemaining = gameController.BlueTilesRemaining,
                elapsedSeconds = gameController.ElapsedSeconds,
                currentViewers = _currentViewers,
                chattingViewers = _chattingViewers,
                peakViewers = _peakViewers,
                broadcastHype = _hype,
                donationWon = _liveDonationWon
            });
        }
    }
}
