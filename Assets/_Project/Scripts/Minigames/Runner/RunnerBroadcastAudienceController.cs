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
            _gameSkill = Mathf.Max(1, gameSkill);
            _talkingSkill = Mathf.Max(1, talkingSkill);
            _mentalLevel = Mathf.Clamp(mentalLevel, 1, 3);
            Hype = _settings.startingHype;
            CurrentViewers = Mathf.Max(0, Mathf.RoundToInt(_settings.baseDiscoveryViewers
                + _followers * _settings.followerNotificationRate
                + _talkingSkill * _settings.viewersPerTalkingSkill
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
            RunnerBroadcastHeatGauge.Show(Hype);
            RefreshChatScale();
        }

        private void Update()
        {
            if (!_running || _settings == null || _gameManager == null || !_gameManager.BroadcastActive) return;
            _viewerSeconds += CurrentViewers * Time.deltaTime;
            float performanceStep = _performanceMeter.Tick(Time.time, _settings);
            if (!Mathf.Approximately(performanceStep, 0f)) ApplyPerformanceHeatStep(performanceStep);
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
            _witInteraction?.NotifySafeMoment("러너에서 방금 장애물을 깔끔하게 통과함");
            TryLiveDonation(_settings != null ? _settings.obstacleClearDonationChance : 0f, "깔끔하게 피하네! 계속 가자");
        }

        public void OnEnemyDefeated()
        {
            _performanceMeter.RecordSuccess(Time.time, _settings);
            _witInteraction?.NotifySafeMoment("러너에서 방금 적을 정확한 타이밍에 처치함");
            TryLiveDonation(_settings != null ? _settings.enemyDefeatDonationChance : 0f, "방금 공격 타이밍 좋았다!");
        }
        public void OnAttackMissed() => _performanceMeter.RecordMistake(Time.time, _settings);
        public void OnPlayerHit(bool lowHealth)
        {
            if (_settings == null) return;
            _performanceMeter.RecordMistake(Time.time, _settings);
        }

        public void OnAttemptDefeated()
        {
            _performanceMeter.RecordMistake(Time.time, _settings);
            _witInteraction?.NotifySafeMoment("러너 게임오버 직후 방금 판을 되짚는 중", 8f);
        }

        public void OnNewHighScore() => AddHype(_settings != null ? _settings.newHighScoreHype : 0f);

        public void OnModerationResult(bool correct) => AddHype(_settings == null ? 0f
            : correct ? _settings.correctModerationHype : _settings.wrongModerationHype);

        public void OnFraternizationTick()
        {
            AddHype(_settings != null ? _settings.fraternizationOngoingHype : 0f);
            int leaving = Mathf.Max(1, Mathf.CeilToInt(CurrentViewers * 0.018f));
            CurrentViewers = Mathf.Max(0, CurrentViewers - leaving);
            _socialFollowerPenalty += Mathf.Max(0.18f, _followers * 0.0008f);
            RefreshChatScale();
        }

        public void OnFraternizationResolved(float responseSeconds)
        {
            float quick = _settings != null ? Mathf.InverseLerp(_settings.slowFraternizationResponseSeconds,
                _settings.quickFraternizationResponseSeconds, responseSeconds) : 0f;
            AddHype(_settings != null ? Mathf.Lerp(_settings.slowFraternizationResolutionHype,
                _settings.quickFraternizationResolutionHype, quick) : 0f);
            int returning = Mathf.Max(1, Mathf.CeilToInt(CurrentViewers * Mathf.Lerp(0.02f, 0.07f, quick)));
            CurrentViewers += returning;
            TotalVisitors += returning;
            PeakViewers = Mathf.Max(PeakViewers, CurrentViewers);
            _moderationFollowerBonus += Mathf.RoundToInt(Mathf.Lerp(0f, 3f, quick));
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
            float scoreRatio = Mathf.Clamp01(score / (float)Mathf.Max(1, targetScore));
            float survivalRatio = completed ? 1f : Mathf.Clamp01(elapsedSeconds / Mathf.Max(1f, durationSeconds));
            float gameplayRating = 1f + scoreRatio * 4f;
            float survivalRating = 1f + survivalRatio * 4f;
            float safetyRating = Mathf.Clamp(5f - hitsTaken * 1.15f, 1f, 5f);
            float combatRating = Mathf.Clamp(2.5f + enemiesDefeated * 0.35f - hitsTaken * 0.35f, 1f, 5f);
            float finalHeat = Mathf.Clamp(Hype + _bufferedHypeChange, 0f, 100f);
            float hostingRating = Mathf.Clamp(2.2f + (_talkingSkill - 1) * 0.28f + finalHeat / 100f * 1.6f
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
                + Mathf.Max(0, _microphoneLevel - 1) * (_gameManager != null && _gameManager.CampaignSettings != null
                    ? _gameManager.CampaignSettings.followerConversionBonusPerMicrophoneUpgrade : 0f)
                + (completed ? _settings.completionFollowBonus : 0f);
            float heat01 = finalHeat / 100f;
            conversion *= Mathf.Lerp(0f, 1.4f, Mathf.InverseLerp(20f, 100f, finalHeat));
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
                * _settings.wonPerViewerRatingPoint * Mathf.Lerp(0.45f, 1.65f, heat01) * donationVariation));

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
            float heat01 = Hype / 100f;
            target *= Mathf.Lerp(0.55f, 1.25f, heat01);
            target *= UnityEngine.Random.Range(1f - _settings.viewerRandomVariation, 1f + _settings.viewerRandomVariation);
            bool growing = target >= CurrentViewers;
            float adjustment = _settings.viewerAdjustmentRate * (growing
                ? Mathf.Lerp(0.65f, 1.35f, heat01)
                : Mathf.Lerp(1.55f, 0.70f, heat01));
            int next = Mathf.Max(0, Mathf.RoundToInt(Mathf.Lerp(CurrentViewers, target, Mathf.Clamp01(adjustment))));
            if (next == CurrentViewers && CurrentViewers <= _settings.idleFluctuationMaximumViewers
                && UnityEngine.Random.value < _settings.idleViewerFluctuationChance)
                next = Mathf.Max(0, next + (UnityEngine.Random.value < 0.5f ? -1 : 1));
            if (next > CurrentViewers) TotalVisitors += next - CurrentViewers;
            CurrentViewers = next;
            PeakViewers = Mathf.Max(PeakViewers, CurrentViewers);
            RefreshChatScale();
        }

        private void AddHype(float amount)
        {
            if (_settings == null) return;
            RunnerCampaignSettings campaignSettings = _gameManager != null ? _gameManager.CampaignSettings : null;
            if (amount > 0f)
                amount *= (1f + Mathf.Max(0, _mentalLevel - 1)
                    * (campaignSettings != null ? campaignSettings.hypeGainBonusPerMentalLevel : 0f))
                    * (1f + Mathf.Max(0, _talkingSkill - 1) * _settings.heatGainBonusPerTalkingLevel);
            else if (amount < 0f)
                amount *= Mathf.Max(0.1f, 1f - Mathf.Max(0, _mentalLevel - 1)
                    * (campaignSettings != null ? campaignSettings.hypePenaltyReductionPerMentalLevel : 0f))
                    * Mathf.Max(0.1f, 1f - Mathf.Max(0, _talkingSkill - 1)
                        * _settings.heatPenaltyReductionPerTalkingLevel);
            _bufferedHypeChange = Mathf.Clamp(_bufferedHypeChange + amount,
                -_settings.maximumBufferedHeatChange, _settings.maximumBufferedHeatChange);
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
            chance *= Mathf.Lerp(0.25f, 1.85f, Hype / 100f);
            if (UnityEngine.Random.value > chance) return;
            GrantLiveDonation(message);
        }

        private void TryAmbientDonation()
        {
            if (!_running || _settings == null || !_settings.enableAmbientDonations
                || Time.unscaledTime < _nextAmbientDonationAt) return;
            if (CurrentViewers >= _settings.minimumViewersForDonation && Time.unscaledTime >= _nextDonationAt)
                GrantLiveDonation(PickAmbientDonationMessage());
            else
                ScheduleNextAmbientDonation();
        }

        private void GrantLiveDonation(string message)
        {
            _nextDonationAt = Time.unscaledTime + _settings.liveDonationCooldown;
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
            float intervalMultiplier = Mathf.Lerp(1.8f, 0.62f, Hype / 100f);
            minimum *= intervalMultiplier;
            maximum *= intervalMultiplier;
            _nextAmbientDonationAt = Time.unscaledTime + UnityEngine.Random.Range(minimum, maximum);
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
            float largeChance = Mathf.Clamp01(settings.largeDonationChance * Mathf.Lerp(0.2f, 2.6f, heat01));
            float mediumChance = Mathf.Clamp01(settings.mediumDonationChance * Mathf.Lerp(0.45f, 1.75f, heat01));
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
                    * Mathf.Lerp(0.72f, 1.18f, heat01),
                _settings != null ? _settings.EventCooldownForViewers(CurrentViewers) : 0f, Hype);
        }
    }

}
