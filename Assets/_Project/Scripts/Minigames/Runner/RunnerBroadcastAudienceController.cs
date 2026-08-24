using System;
using System.Collections;
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
        private int _mentalLevel = 1;
        private float _nextViewerUpdate;
        private float _viewerSeconds;
        private bool _running;
        private float _nextDonationAt;
        private float _nextAmbientDonationAt;
        private RunnerDonationPopupController _donationPopup;
        private RunnerWitInteractionController _witInteraction;
        private float _witHostingBonus;
        private float _witFollowBonus;
        private int _microphoneLevel = 1;
        private int _interiorLevel = 1;
        private float _socialFollowerPenalty;
        private int _moderationFollowerBonus;
        private readonly RunnerBroadcastPerformanceMeter _performanceMeter = new RunnerBroadcastPerformanceMeter();
        private float _bufferedHypeChange;
        private bool _cleanMistakeProtectionUsed;
        private bool _largePenaltyProtectionUsed;
        private float _nextMistakeClearAt;

        public void Configure(RunnerBroadcastGrowthSettings settings, RunnerGameManager gameManager, RunnerChatController chat)
        {
            _settings = settings;
            _gameManager = gameManager;
            _chat = chat;
            if (_donationPopup == null) _donationPopup = FindFirstObjectByType<RunnerDonationPopupController>();
            if (_witInteraction == null) _witInteraction = FindFirstObjectByType<RunnerWitInteractionController>();
        }

        public void ConfigureEquipment(int microphoneLevel, int interiorLevel)
        {
            _microphoneLevel = Mathf.Clamp(microphoneLevel, 1, 3);
            _interiorLevel = Mathf.Clamp(interiorLevel, 1, 3);
        }

        public void BeginBroadcast(int followers, int gameSkill, int talkingSkill, int mentalLevel)
        {
            if (_settings == null) return;
            _followers = Mathf.Max(0, followers);
            _gameSkill = 0;
            _talkingSkill = 1;
            _mentalLevel = 1;
            Hype = _settings.startingHype;
            CurrentViewers = Mathf.Max(0, Mathf.RoundToInt(_settings.baseDiscoveryViewers
                + _followers * _settings.followerNotificationRate
                + Mathf.Max(0, _interiorLevel - 1) * (_gameManager != null && _gameManager.CampaignSettings != null
                    ? _gameManager.CampaignSettings.startingViewersPerInteriorUpgrade : 0f)));
            PeakViewers = CurrentViewers;
            TotalVisitors = CurrentViewers;
            _viewerSeconds = 0f;
            _nextViewerUpdate = Time.time + _settings.viewerUpdateInterval;
            LastResult = null;
            LiveDonationWon = 0;
            _nextDonationAt = 0f;
            ScheduleNextAmbientDonation();
            _witHostingBonus = 0f;
            _witFollowBonus = 0f;
            _performanceMeter.Reset(Time.time);
            _bufferedHypeChange = 0f;
            _socialFollowerPenalty = 0f;
            _moderationFollowerBonus = 0;
            _running = true;
            _cleanMistakeProtectionUsed = false;
            _largePenaltyProtectionUsed = false;
            _nextMistakeClearAt = 0f;
            RunnerBroadcastHeatGauge.Show(Hype);
            RefreshChatScale();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || !_running || _settings == null || _gameManager == null
                || !_gameManager.BroadcastActive) return;
            _viewerSeconds += CurrentViewers * Time.deltaTime;
            ComposureRankRule mental = CurrentComposureRule();
            float performanceStep = _performanceMeter.Tick(Time.time, _settings,
                mental != null ? mental.poorStateTickInterval : -1f,
                mental != null ? mental.extraMistakesRequiredForPoorState : 0,
                mental != null ? mental.neutralRecoveryTimeReduction : 0f);
            if (!Mathf.Approximately(performanceStep, 0f)) ApplyPerformanceHeatStep(performanceStep);
            if (_performanceMeter.State != BroadcastPerformanceState.Good) _cleanMistakeProtectionUsed = false;
            ApplyBufferedHype(Time.deltaTime);
            Hype = Mathf.MoveTowards(Hype, _settings.restingHype, _settings.hypeReturnPerSecond * Time.deltaTime);
            RunnerBroadcastHeatGauge.SetValue(Hype);
            TryAmbientDonation();
            if (Time.time < _nextViewerUpdate) return;
            _nextViewerUpdate = Time.time + _settings.viewerUpdateInterval;
            UpdateViewerCount();
        }

        public void OnObstacleCleared()
        {
            _performanceMeter.RecordSuccess(Time.time, _settings);
            GrantGameplayExperience(_gameManager != null && _gameManager.CampaignSettings != null
                ? _gameManager.CampaignSettings.runnerObstacleClearExperience : 0);
            _witInteraction?.NotifySafeMoment("러너에서 방금 장애물을 깔끔하게 통과함");
            TryLiveDonation(_settings != null ? _settings.obstacleClearDonationChance : 0f, "깔끔하게 피하네! 계속 가자");
        }

        public void OnEnemyDefeated()
        {
            _performanceMeter.RecordSuccess(Time.time, _settings);
            GrantGameplayExperience(_gameManager != null && _gameManager.CampaignSettings != null
                ? _gameManager.CampaignSettings.runnerEnemyDefeatExperience : 0);
            _witInteraction?.NotifySafeMoment("러너에서 방금 적을 정확한 타이밍에 처치함");
            TryLiveDonation(_settings != null ? _settings.enemyDefeatDonationChance : 0f, "방금 공격 타이밍 좋았다!");
        }
        public void OnAttackMissed() => _performanceMeter.RecordMistake(Time.time, _settings);
        public void OnPlayerHit(bool lowHealth)
        {
            if (_settings == null) return;
            ComposureRankRule mental = CurrentComposureRule();
            if (mental != null && mental.protectsFirstMistakeAfterGoodPlay
                && _performanceMeter.State == BroadcastPerformanceState.Good && !_cleanMistakeProtectionUsed)
            {
                _cleanMistakeProtectionUsed = true;
                return;
            }
            _performanceMeter.RecordMistake(Time.time, _settings);
        }

        public void OnAttemptDefeated()
        {
            _performanceMeter.RecordMistake(Time.time, _settings);
            _witInteraction?.NotifySafeMoment("러너 게임오버 직후 방금 판을 되짚는 중", 8f);
        }

        public void OnNewHighScore() => AddHype(_settings != null ? _settings.newHighScoreHype : 0f);

        public void OnModerationResult(bool correct) => AddHype(_settings == null ? 0f
            : correct ? _settings.correctModerationHype : _settings.wrongModerationHype, correct ? 1f : 0f);

        public void OnFraternizationTick()
        {
            AddHype(_settings != null ? _settings.fraternizationOngoingHype : 0f, .5f);
            int leaving = Mathf.Max(_settings.socialMinimumViewerLeave,
                Mathf.CeilToInt(CurrentViewers * _settings.socialViewerLeaveFraction));
            CurrentViewers = Mathf.Max(0, CurrentViewers - leaving);
            _socialFollowerPenalty += Mathf.Max(_settings.socialFollowerPenaltyMinimum,
                _followers * _settings.socialFollowerPenaltyPerFollower);
            RefreshChatScale();
        }

        public void OnFraternizationResolved(float responseSeconds)
        {
            float quick = _settings != null ? Mathf.InverseLerp(_settings.slowFraternizationResponseSeconds,
                _settings.quickFraternizationResponseSeconds, responseSeconds) : 0f;
            AddHype(_settings != null ? Mathf.Lerp(_settings.slowFraternizationResolutionHype,
                _settings.quickFraternizationResolutionHype, quick) : 0f);
            int returning = Mathf.Max(_settings.socialMinimumViewerLeave, Mathf.CeilToInt(CurrentViewers
                * Mathf.Lerp(_settings.socialResolutionViewerReturnMinimum, _settings.socialResolutionViewerReturnMaximum, quick)));
            CurrentViewers += returning;
            TotalVisitors += returning;
            PeakViewers = Mathf.Max(PeakViewers, CurrentViewers);
            _moderationFollowerBonus += Mathf.RoundToInt(Mathf.Lerp(0f, _settings.socialResolutionFollowerBonusMaximum, quick));
            RefreshChatScale();
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
            float survivalRatio = completed ? 1f : Mathf.Clamp01(elapsedSeconds / Mathf.Max(1f, durationSeconds));
            float gameplayRating = RatingFromBroadcastScore(score, targetScore);
            float survivalRating = 1f + survivalRatio * 4f;
            float safetyRating = Mathf.Clamp(5f - hitsTaken * _settings.runnerSafetyPenaltyPerHit, 1f, 5f);
            float combatRating = Mathf.Clamp(_settings.runnerCombatRatingBase
                + enemiesDefeated * _settings.runnerCombatRatingPerEnemy
                - hitsTaken * _settings.runnerCombatRatingPenaltyPerHit, 1f, 5f);
            float finalHeat = Mathf.Clamp(Hype + _bufferedHypeChange, 0f, 100f);
            float hostingRating = Mathf.Clamp(_settings.runnerHostingRatingBase
                + (_talkingSkill - 1) * _settings.runnerHostingRatingPerTalkingLevel
                + finalHeat / 100f * _settings.runnerHostingRatingHeatRange
                + _witHostingBonus, 1f, 5f);
            float finalRating = gameplayRating;

            float conversion = _settings.baseFollowConversion
                + finalRating * _settings.followConversionPerRatingPoint
                + Mathf.Max(0, _talkingSkill - 1) * _settings.followConversionPerTalkingLevel
                + _witFollowBonus
                + Mathf.Max(0, _microphoneLevel - 1) * (_gameManager != null && _gameManager.CampaignSettings != null
                    ? _gameManager.CampaignSettings.followerConversionBonusPerMicrophoneUpgrade : 0f)
                + (completed ? _settings.completionFollowBonus : 0f);
            float heat01 = finalHeat / 100f;
            conversion *= Mathf.Lerp(0f, _settings.followHeatMaximumMultiplier,
                Mathf.InverseLerp(_settings.followHeatMinimum, _settings.followHeatMaximum, finalHeat));
            conversion = Mathf.Clamp(conversion, 0f, _settings.maximumFollowConversion);
            int gained = Mathf.Max(0, Mathf.RoundToInt(TotalVisitors * conversion) + _moderationFollowerBonus);
            int lost = finalRating < _settings.unfollowRatingThreshold
                ? Mathf.Min(_followers, Mathf.CeilToInt(_followers * (_settings.unfollowRatingThreshold - finalRating)
                    * _settings.unfollowRatePerMissingRatingPoint))
                : 0;
            if (finalHeat < _settings.lowHeatUnfollowThreshold)
                lost = Mathf.Min(_followers, lost + Mathf.CeilToInt(_followers
                    * Mathf.InverseLerp(_settings.lowHeatUnfollowThreshold, 0f, finalHeat) * _settings.lowHeatUnfollowRate));
            lost = Mathf.Min(_followers, lost + Mathf.CeilToInt(_socialFollowerPenalty));
            float donationVariation = UnityEngine.Random.Range(1f - _settings.donationRandomVariation, 1f + _settings.donationRandomVariation);
            int donation = LiveDonationWon + Mathf.Max(0, Mathf.RoundToInt(average * finalRating
                * (1f + Mathf.Max(0, _talkingSkill - 1) * _settings.donationBonusPerTalkingLevel)
                * (1f + Mathf.Max(0, _microphoneLevel - 1) * (_gameManager != null && _gameManager.CampaignSettings != null
                    ? _gameManager.CampaignSettings.donationBonusPerMicrophoneUpgrade : 0f))
                * _settings.wonPerViewerRatingPoint * Mathf.Lerp(_settings.donationValueMultiplierAtZeroHeat,
                    _settings.donationValueMultiplierAtFullHeat, heat01) * donationVariation));

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

        public static float RatingFromBroadcastScore(int score, int targetScore)
        {
            float ratio = Mathf.Max(0, score) / (float)Mathf.Max(1, targetScore);
            if (ratio <= .5f) return Mathf.Lerp(1f, 2f, ratio / .5f);
            if (ratio <= .8f) return Mathf.Lerp(2f, 3f, Mathf.InverseLerp(.5f, .8f, ratio));
            if (ratio <= 1f) return Mathf.Lerp(3f, 4f, Mathf.InverseLerp(.8f, 1f, ratio));
            if (ratio <= 1.3f) return Mathf.Lerp(4f, 5f, Mathf.InverseLerp(1f, 1.3f, ratio));
            return 5f;
        }

        private void UpdateViewerCount()
        {
            if (_settings == null) return;
            float target = _settings.baseDiscoveryViewers
                + _followers * _settings.followerNotificationRate
                + Hype * _settings.viewersPerHypePoint
                ;
            float heat01 = Hype / 100f;
            target *= Mathf.Lerp(_settings.viewerTargetMultiplierAtZeroHeat, _settings.viewerTargetMultiplierAtFullHeat, heat01);
            target *= UnityEngine.Random.Range(1f - _settings.viewerRandomVariation, 1f + _settings.viewerRandomVariation);
            bool growing = target >= CurrentViewers;
            float adjustment = _settings.viewerAdjustmentRate * (growing
                ? Mathf.Lerp(_settings.viewerGrowthRateMultiplierAtZeroHeat, _settings.viewerGrowthRateMultiplierAtFullHeat, heat01)
                : Mathf.Lerp(_settings.viewerDeclineRateMultiplierAtZeroHeat, _settings.viewerDeclineRateMultiplierAtFullHeat, heat01));
            int next = Mathf.Max(0, Mathf.RoundToInt(Mathf.Lerp(CurrentViewers, target, Mathf.Clamp01(adjustment))));
            if (next == CurrentViewers && CurrentViewers <= _settings.idleFluctuationMaximumViewers
                && UnityEngine.Random.value < _settings.idleViewerFluctuationChance)
                next = Mathf.Max(0, next + (UnityEngine.Random.value < 0.5f ? -1 : 1));
            if (next > CurrentViewers) TotalVisitors += next - CurrentViewers;
            CurrentViewers = next;
            PeakViewers = Mathf.Max(PeakViewers, CurrentViewers);
            RefreshChatScale();
        }

        private void AddHype(float amount, float mentalReductionScale = 1f)
        {
            if (_settings == null) return;
            RunnerCampaignSettings campaignSettings = _gameManager != null ? _gameManager.CampaignSettings : null;
            if (amount < 0f && campaignSettings != null
                && RunnerCampaignSaveStore.TryLoad(campaignSettings, out RunnerCampaignSaveData save))
            {
                ComposureRankRule mental = campaignSettings.ComposureRule(save.ComposureRank);
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
                -_settings.maximumBufferedHeatChange, _settings.maximumBufferedHeatChange);
        }

        private ComposureRankRule CurrentComposureRule()
        {
            RunnerCampaignSettings campaignSettings = _gameManager != null ? _gameManager.CampaignSettings : null;
            if (campaignSettings == null || !RunnerCampaignSaveStore.TryLoad(campaignSettings, out RunnerCampaignSaveData save)) return null;
            return campaignSettings.ComposureRule(save.ComposureRank);
        }

        private void GrantGameplayExperience(int amount)
        {
            RunnerCampaignSettings campaignSettings = _gameManager != null ? _gameManager.CampaignSettings : null;
            if (amount <= 0 || campaignSettings == null
                || !RunnerCampaignSaveStore.TryLoad(campaignSettings, out RunnerCampaignSaveData save)) return;
            BroadcasterProgression.AddBroadcastExperience(campaignSettings, save, amount);
            RunnerCampaignSaveStore.Save(campaignSettings, save, true);
        }

        private void ApplyBufferedHype(float deltaTime)
        {
            if (Mathf.Approximately(_bufferedHypeChange, 0f)) return;
            float amount = Mathf.Sign(_bufferedHypeChange) * Mathf.Min(Mathf.Abs(_bufferedHypeChange),
                _settings.eventHeatChangePerSecond * deltaTime);
            Hype = Mathf.Clamp(Hype + amount, 0f, 100f);
            _bufferedHypeChange -= amount;
            if ((Hype <= 0f && _bufferedHypeChange < 0f) || (Hype >= 100f && _bufferedHypeChange > 0f))
                _bufferedHypeChange = 0f;
        }

        private void ApplyPerformanceHeatStep(float amount)
        {
            Hype = Mathf.Clamp(Hype + amount, 0f, 100f);
        }

        public void ApplyWitInteraction(int quality)
        {
            if (!_running || _settings == null) return;
            if (quality >= 2)
            {
                RunnerCampaignSettings campaignSettings = _gameManager != null ? _gameManager.CampaignSettings : null;
                WitRankRule wit = null;
                ComposureRankRule mental = null;
                if (campaignSettings != null && RunnerCampaignSaveStore.TryLoad(campaignSettings, out RunnerCampaignSaveData save))
                {
                    wit = campaignSettings.WitRule(save.witRank);
                    mental = campaignSettings.ComposureRule(save.ComposureRank);
                }
                float perkMultiplier = quality >= 5 ? (wit != null ? wit.comebackRewardMultiplier : 1f)
                    : quality >= 4 ? (wit != null ? wit.correctStreakRewardMultiplier : 1f)
                    : quality >= 3 ? (wit != null ? wit.advancedAnswerRewardMultiplier : 1f) : 1f;
                float levelThreeMultiplier = (1f + (wit != null ? wit.correctHeatGainBonus : 0f)) * perkMultiplier;
                AddHype(_settings.witSuccessHype * levelThreeMultiplier);
                if (mental != null && mental.correctWitClearsRecentMistakes && Time.time >= _nextMistakeClearAt)
                {
                    _performanceMeter.ClearRecentMistakes();
                    _nextMistakeClearAt = Time.time + mental.mistakeClearCooldownSeconds;
                }
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
                || Time.time < _nextDonationAt) return;
            float chance = baseChance * (1f + Mathf.Max(0, _talkingSkill - 1) * _settings.donationChancePerTalkingLevel);
            chance *= Mathf.Lerp(_settings.donationEventChanceMultiplierAtZeroHeat,
                _settings.donationEventChanceMultiplierAtFullHeat, Hype / 100f);
            if (UnityEngine.Random.value > chance) return;
            GrantLiveDonation(message);
        }

        private void TryAmbientDonation()
        {
            if (!_running || _settings == null || !_settings.enableAmbientDonations
                || Time.time < _nextAmbientDonationAt) return;
            if (CurrentViewers >= _settings.minimumViewersForDonation && Time.time >= _nextDonationAt)
                GrantLiveDonation(PickAmbientDonationMessage());
            else
                ScheduleNextAmbientDonation();
        }

        private void GrantLiveDonation(string message)
        {
            _nextDonationAt = Time.time + _settings.liveDonationCooldown;
            ScheduleNextAmbientDonation();
            int amount = RollDonationAmount(_settings, Hype);
            string donor = _chat != null ? _chat.PickDonationViewerNickname() : "익명의 시청자";
            LiveDonationWon += amount;
            _donationPopup?.ShowDonation(donor, amount, message);
            _chat?.OnDonationReceived(donor, amount, message, amount == _settings.largeDonationWon);
        }

        private void ScheduleNextAmbientDonation()
        {
            if (_settings == null) { _nextAmbientDonationAt = float.PositiveInfinity; return; }
            float minimum = Mathf.Max(_settings.liveDonationCooldown, _settings.ambientDonationMinimumInterval);
            float maximum = Mathf.Max(minimum, _settings.ambientDonationMaximumInterval);
            float intervalMultiplier = Mathf.Lerp(_settings.donationIntervalMultiplierAtZeroHeat,
                _settings.donationIntervalMultiplierAtFullHeat, Hype / 100f);
            minimum *= intervalMultiplier;
            maximum *= intervalMultiplier;
            _nextAmbientDonationAt = Time.time + UnityEngine.Random.Range(minimum, maximum);
        }

        private string PickAmbientDonationMessage()
        {
            string[] messages = _settings != null ? _settings.ambientDonationMessages : null;
            return messages != null && messages.Length > 0
                ? messages[UnityEngine.Random.Range(0, messages.Length)]
                : "방송 잘 보고 있어요!";
        }

        public static int RollDonationAmount(RunnerBroadcastGrowthSettings settings, float heat = 50f)
        {
            float roll = UnityEngine.Random.value;
            float heat01 = Mathf.Clamp01(heat / 100f);
            float largeChance = Mathf.Clamp01(settings.largeDonationChance * Mathf.Lerp(
                settings.largeDonationChanceMultiplierAtZeroHeat, settings.largeDonationChanceMultiplierAtFullHeat, heat01));
            float mediumChance = Mathf.Clamp01(settings.mediumDonationChance * Mathf.Lerp(
                settings.mediumDonationChanceMultiplierAtZeroHeat, settings.mediumDonationChanceMultiplierAtFullHeat, heat01));
            if (roll < largeChance) return settings.largeDonationWon;
            if (roll < largeChance + mediumChance) return settings.mediumDonationWon;
            return settings.smallDonationWon;
        }

        public IEnumerator DrainViewersToZero(float duration)
        {
            _running = false;
            int startingViewers = CurrentViewers;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                CurrentViewers = Mathf.Max(0, Mathf.CeilToInt(Mathf.Lerp(startingViewers, 0f,
                    Mathf.Clamp01(elapsed / Mathf.Max(0.1f, duration)))));
                RefreshChatScale();
                yield return null;
            }
            CurrentViewers = 0;
            RefreshChatScale();
            RunnerBroadcastHeatGauge.Hide();
        }

        private void RefreshChatScale()
        {
            ChattingViewers = _settings != null ? _settings.ChattersForViewers(CurrentViewers) : 0;
            float heat01 = Hype / 100f;
            _chat?.ConfigureAudience(CurrentViewers, ChattingViewers,
                _settings != null ? _settings.ChatDelayMultiplierForViewers(CurrentViewers) : 1f,
                (_settings != null ? _settings.EventReactionChanceForViewers(CurrentViewers) : 1f)
                    * Mathf.Lerp(_settings.eventReactionMultiplierAtZeroHeat, _settings.eventReactionMultiplierAtFullHeat, heat01),
                _settings != null ? _settings.EventCooldownForViewers(CurrentViewers) : 0f, Hype);
        }
    }

}
