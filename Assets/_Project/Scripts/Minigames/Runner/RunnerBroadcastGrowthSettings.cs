using System.Collections.Generic;
using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    public enum BroadcastPerformanceState
    {
        Good,
        Neutral,
        Poor
    }

    public sealed class RunnerBroadcastPerformanceMeter
    {
        private readonly Queue<float> _successes = new Queue<float>();
        private readonly Queue<float> _mistakes = new Queue<float>();
        private float _lastSuccessAt = float.NegativeInfinity;
        private float _lastMistakeAt;
        private float _lastMistakeSampleAt = float.NegativeInfinity;
        private float _nextEvaluationAt;
        private float _nextHeatStepAt;

        public BroadcastPerformanceState State { get; private set; } = BroadcastPerformanceState.Neutral;

        public void Reset(float now)
        {
            _successes.Clear();
            _mistakes.Clear();
            _lastSuccessAt = float.NegativeInfinity;
            _lastMistakeAt = now;
            _lastMistakeSampleAt = float.NegativeInfinity;
            _nextEvaluationAt = now;
            _nextHeatStepAt = float.PositiveInfinity;
            State = BroadcastPerformanceState.Neutral;
        }

        public void RecordSuccess(float now, RunnerBroadcastGrowthSettings settings)
        {
            if (settings == null || now - _lastSuccessAt < settings.minimumSuccessSampleSpacing) return;
            _lastSuccessAt = now;
            _successes.Enqueue(now);
        }

        public void RecordMistake(float now, RunnerBroadcastGrowthSettings settings)
        {
            if (settings == null || now - _lastMistakeSampleAt < settings.minimumMistakeSampleSpacing) return;
            _lastMistakeSampleAt = now;
            _lastMistakeAt = now;
            _mistakes.Enqueue(now);
        }

        public void ClearRecentMistakes()
        {
            _mistakes.Clear();
            _lastMistakeAt = float.NegativeInfinity;
            _lastMistakeSampleAt = float.NegativeInfinity;
            State = BroadcastPerformanceState.Neutral;
        }

        public float Tick(float now, RunnerBroadcastGrowthSettings settings, float poorTickIntervalOverride = -1f,
            int extraMistakesForPoorState = 0, float neutralRecoveryTimeReduction = 0f)
        {
            if (settings == null) return 0f;
            float recoveryScale = 1f - Mathf.Clamp(neutralRecoveryTimeReduction, 0f, .9f);
            Prune(_successes, now - settings.performanceWindowSeconds);
            // Mental shortens how long old failures keep the state in Poor. Success
            // samples retain the full window, so the perk never removes good play.
            Prune(_mistakes, now - settings.performanceWindowSeconds * recoveryScale);

            if (now >= _nextEvaluationAt)
            {
                _nextEvaluationAt = now + settings.performanceEvaluationInterval * recoveryScale;
                bool poor = _mistakes.Count >= settings.poorStateMinimumMistakes + Mathf.Max(0, extraMistakesForPoorState)
                    && _successes.Count <= _mistakes.Count * settings.poorStateMaximumSuccessesPerMistake;
                bool good = !poor && now - _lastMistakeAt >= settings.goodStateMistakeFreeSeconds
                    && _successes.Count >= settings.goodStateMinimumSuccesses;
                BroadcastPerformanceState next = poor ? BroadcastPerformanceState.Poor
                    : good ? BroadcastPerformanceState.Good : BroadcastPerformanceState.Neutral;
                if (next != State)
                {
                    State = next;
                    _nextHeatStepAt = State == BroadcastPerformanceState.Good
                        ? now + settings.goodStateHeatStepInterval
                        : State == BroadcastPerformanceState.Poor
                            ? now + (poorTickIntervalOverride > 0f ? poorTickIntervalOverride : settings.poorStateHeatStepInterval)
                            : float.PositiveInfinity;
                }
            }

            if (State == BroadcastPerformanceState.Neutral || now < _nextHeatStepAt) return 0f;
            _nextHeatStepAt = now + (State == BroadcastPerformanceState.Good
                ? settings.goodStateHeatStepInterval
                : poorTickIntervalOverride > 0f ? poorTickIntervalOverride : settings.poorStateHeatStepInterval);
            return State == BroadcastPerformanceState.Good ? 1f : -1f;
        }

        private static void Prune(Queue<float> samples, float oldestAllowed)
        {
            while (samples.Count > 0 && samples.Peek() < oldestAllowed) samples.Dequeue();
        }
    }

    [CreateAssetMenu(fileName = "Runner Broadcast Growth Settings", menuName = "STREAM ON/Runner/Broadcast Growth Settings")]
    public sealed class RunnerBroadcastGrowthSettings : ScriptableObject
    {
        [Header("Live Viewer Simulation")]
        [Min(0)] public int baseDiscoveryViewers = 2;
        [Range(0f, 1f)] public float followerNotificationRate = 0.08f;
        [Min(0f)] public float viewersPerHypePoint = 0.12f;
        [Min(0f)] public float viewersPerGameSkill = 0.45f;
        [Min(0f)] public float viewersPerTalkingSkill = 0.8f;
        [Min(0.5f)] public float viewerUpdateInterval = 5f;
        [Range(0.05f, 1f)] public float viewerAdjustmentRate = 0.38f;
        [Range(0f, 0.5f)] public float viewerRandomVariation = 0.12f;
        [Range(0f, 1f)] public float idleViewerFluctuationChance = 0.55f;
        [Min(1)] public int idleFluctuationMaximumViewers = 50;
        [Min(0f)] public float viewerTargetMultiplierAtZeroHeat = .55f;
        [Min(0f)] public float viewerTargetMultiplierAtFullHeat = 1.25f;
        [Min(0f)] public float viewerGrowthRateMultiplierAtZeroHeat = .65f;
        [Min(0f)] public float viewerGrowthRateMultiplierAtFullHeat = 1.35f;
        [Min(0f)] public float viewerDeclineRateMultiplierAtZeroHeat = 1.55f;
        [Min(0f)] public float viewerDeclineRateMultiplierAtFullHeat = .70f;
        [Range(0f, 100f)] public float startingHype = 50f;
        [Range(0f, 100f)] public float restingHype = 50f;
        [Tooltip("보통 상태에서 기준 열기로 돌아가는 속도입니다. 0이면 보통 상태의 열기는 변하지 않습니다.")]
        [Min(0f)] public float hypeReturnPerSecond = 0f;

        [Header("Broadcast Heat Rules")]
        [Tooltip("재치 레벨이 1 오를 때 방송 열기 상승량에 더해지는 비율입니다.")]
        [Range(0f, 1f)] public float heatGainBonusPerTalkingLevel = 0.18f;
        [Tooltip("재치 레벨이 1 오를 때 방송 열기 감소량에서 덜 받는 비율입니다.")]
        [Range(0f, 0.45f)] public float heatPenaltyReductionPerTalkingLevel = 0.14f;
        [Min(0f)] public float newHighScoreHype = 8f;
        [Min(0.5f)] public float viewerExitDuration = 3.2f;

        [Header("Social Event Audience Impact")]
        [Range(0f, 1f)] public float socialViewerLeaveFraction = .018f;
        [Min(0)] public int socialMinimumViewerLeave = 1;
        [Min(0f)] public float socialFollowerPenaltyMinimum = .18f;
        [Min(0f)] public float socialFollowerPenaltyPerFollower = .0008f;
        [Range(0f, 1f)] public float socialResolutionViewerReturnMinimum = .02f;
        [Range(0f, 1f)] public float socialResolutionViewerReturnMaximum = .07f;
        [Min(0)] public int socialResolutionFollowerBonusMaximum = 3;

        [Header("Performance State Heat")]
        [Tooltip("최근 성공과 실수를 평가할 시간 범위입니다.")]
        [Min(2f)] public float performanceWindowSeconds = 12f;
        [Tooltip("잘함/보통/못함 상태를 다시 판정하는 간격입니다.")]
        [Min(0.1f)] public float performanceEvaluationInterval = 0.5f;
        [Tooltip("마지막 실수 이후 이 시간 이상 지나야 잘하고 있는 상태가 될 수 있습니다.")]
        [Min(0f)] public float goodStateMistakeFreeSeconds = 8f;
        [Tooltip("판정 시간 안에 이만큼의 성공 표본이 있어야 잘하고 있는 상태가 됩니다.")]
        [Min(1)] public int goodStateMinimumSuccesses = 3;
        [Tooltip("판정 시간 안에 이만큼의 실수가 쌓여야 못하고 있는 상태가 됩니다.")]
        [Min(1)] public int poorStateMinimumMistakes = 2;
        [Tooltip("실수 1회당 허용할 성공 표본 수입니다. 성공이 더 많으면 보통 상태로 판정합니다.")]
        [Min(0f)] public float poorStateMaximumSuccessesPerMistake = 0.75f;
        [Tooltip("반복 호출되는 성공을 하나로 뭉개는 최소 간격입니다.")]
        [Min(0f)] public float minimumSuccessSampleSpacing = 0.75f;
        [Tooltip("한 피격에서 중복 호출되는 실수를 하나로 뭉개는 최소 간격입니다.")]
        [Min(0f)] public float minimumMistakeSampleSpacing = 0.35f;
        [Tooltip("잘하고 있는 상태에서 열기 1%가 오르는 간격입니다.")]
        [Min(0.1f)] public float goodStateHeatStepInterval = 3f;
        [Tooltip("못하고 있는 상태에서 열기 1%가 내려가는 간격입니다.")]
        [Min(0.1f)] public float poorStateHeatStepInterval = 2f;
        [Tooltip("특별 이벤트 보상/페널티가 실제 열기에 반영되는 초당 속도입니다.")]
        [Min(0.1f)] public float eventHeatChangePerSecond = 3f;
        [Tooltip("특별 이벤트 증감이 한꺼번에 쌓일 수 있는 최대 절댓값입니다.")]
        [Min(1f)] public float maximumBufferedHeatChange = 20f;

        [Header("Special Event Heat")]
        public float completedBroadcastHype = 8f;
        public float correctModerationHype = 7f;
        public float wrongModerationHype = -14f;
        public float fraternizationOngoingHype = -1.4f;
        public float slowFraternizationResolutionHype = 3f;
        public float quickFraternizationResolutionHype = 9f;
        [Min(0f)] public float quickFraternizationResponseSeconds = 6f;
        [Min(0f)] public float slowFraternizationResponseSeconds = 32f;

        [Header("Chat Participation")]
        [Tooltip("채팅자 수 = base + sqrt(접속자) x multiplier")]
        [Min(0f)] public float baseChatters = 1.2f;
        [Min(0f)] public float chattersPerViewerSquareRoot = 1.08f;
        [Min(1)] public int maximumSimulatedChatters = 40;
        [Range(0f, 1f)] public float chatSpeedPerViewerLog = 0.28f;
        [Range(0.2f, 1f)] public float minimumChatDelayMultiplier = 0.38f;
        [Range(0f, 1f)] public float smallAudienceEventReactionChance = 0.16f;
        [Range(0f, 1f)] public float largeAudienceEventReactionChance = 0.88f;
        [Min(10)] public int viewersForMaximumChatActivity = 500;
        [Min(0.1f)] public float smallAudienceEventCooldown = 4.5f;
        [Min(0.1f)] public float largeAudienceEventCooldown = 0.75f;
        [Min(0f)] public float eventReactionMultiplierAtZeroHeat = .72f;
        [Min(0f)] public float eventReactionMultiplierAtFullHeat = 1.18f;

        [Header("Follower Conversion")]
        [Range(0f, 1f)] public float baseFollowConversion = 0.08f;
        [Range(0f, 0.2f)] public float followConversionPerRatingPoint = 0.025f;
        [Range(0f, 0.2f)] public float followConversionPerTalkingLevel = 0.01f;
        [Range(0f, 0.2f)] public float completionFollowBonus = 0.03f;
        [Range(0f, 1f)] public float maximumFollowConversion = 0.40f;
        [Range(1f, 5f)] public float unfollowRatingThreshold = 2.5f;
        [Range(0f, 0.2f)] public float unfollowRatePerMissingRatingPoint = 0.03f;
        [Range(0f, 100f)] public float lowHeatUnfollowThreshold = 28f;
        [Range(0f, 0.2f)] public float lowHeatUnfollowRate = 0.025f;
        [Range(0f, 100f)] public float followHeatMinimum = 20f;
        [Range(0f, 100f)] public float followHeatMaximum = 100f;
        [Min(0f)] public float followHeatMaximumMultiplier = 1.4f;

        [Header("Per-game Rating Curves")]
        [Min(0f)] public float runnerSafetyPenaltyPerHit = 1.15f;
        public float runnerCombatRatingBase = 2.5f;
        [Min(0f)] public float runnerCombatRatingPerEnemy = .35f;
        [Min(0f)] public float runnerCombatRatingPenaltyPerHit = .35f;
        public float runnerHostingRatingBase = 2.2f;
        [Min(0f)] public float runnerHostingRatingPerTalkingLevel = .28f;
        [Min(0f)] public float runnerHostingRatingHeatRange = 1.6f;
        [Min(0f)] public float tileSafetyPenaltyPerHit = .45f;
        public float tileHostingRatingBase = 2.2f;
        [Min(0f)] public float tileHostingRatingHeatRange = 1.6f;

        [Header("Broadcast Rating Weights")]
        [Min(0f)] public float gameplayRatingWeight = 0.40f;
        [Min(0f)] public float survivalRatingWeight = 0.20f;
        [Min(0f)] public float safetyRatingWeight = 0.15f;
        [Min(0f)] public float combatRatingWeight = 0.15f;
        [Min(0f)] public float hostingRatingWeight = 0.10f;

        [Header("Donation Economy")]
        [Min(0f)] public float wonPerViewerRatingPoint = 50f;
        [Range(0f, 1f)] public float donationBonusPerTalkingLevel = 0.08f;
        [Range(0f, 0.8f)] public float donationRandomVariation = 0.15f;
        [Min(0f)] public float donationValueMultiplierAtZeroHeat = .45f;
        [Min(0f)] public float donationValueMultiplierAtFullHeat = 1.65f;

        [Header("Live Donation Events")]
        [Min(0)] public int minimumViewersForDonation = 1;
        [Min(0.5f)] public float liveDonationCooldown = 9f;
        [Tooltip("성공 이벤트와 무관하게 방송 중 임의의 시점에 들어오는 일반 후원입니다.")]
        public bool enableAmbientDonations = true;
        [Min(1f)] public float ambientDonationMinimumInterval = 12f;
        [Min(1f)] public float ambientDonationMaximumInterval = 24f;
        public string[] ambientDonationMessages =
        {
            "방송 재밌게 보고 있어요 ㅋㅋ",
            "오늘도 방송 파이팅!",
            "커피값 보태고 갑니다",
            "조용히 보다가 한 번 쏩니다",
            "계속 가보자고"
        };
        [Range(0f, 1f)] public float obstacleClearDonationChance = 0.025f;
        [Range(0f, 1f)] public float enemyDefeatDonationChance = 0.16f;
        [Range(0f, 1f)] public float tilePickupDonationChance = 0.018f;
        [Range(0f, 1f)] public float tileStageClearDonationChance = 0.20f;
        [Range(0f, 1f)] public float donationChancePerTalkingLevel = 0.12f;
        [Min(100)] public int smallDonationWon = 1000;
        [Min(100)] public int mediumDonationWon = 5000;
        [Min(100)] public int largeDonationWon = 10000;
        [Range(0f, 1f)] public float mediumDonationChance = 0.22f;
        [Range(0f, 1f)] public float largeDonationChance = 0.05f;
        [Min(0f)] public float donationEventChanceMultiplierAtZeroHeat = .25f;
        [Min(0f)] public float donationEventChanceMultiplierAtFullHeat = 1.85f;
        [Min(0f)] public float donationIntervalMultiplierAtZeroHeat = 1.8f;
        [Min(0f)] public float donationIntervalMultiplierAtFullHeat = .62f;
        [Min(0f)] public float largeDonationChanceMultiplierAtZeroHeat = .2f;
        [Min(0f)] public float largeDonationChanceMultiplierAtFullHeat = 2.6f;
        [Min(0f)] public float mediumDonationChanceMultiplierAtZeroHeat = .45f;
        [Min(0f)] public float mediumDonationChanceMultiplierAtFullHeat = 1.75f;

        [Header("Wit Interaction Rewards")]
        public float witSuccessHype = 7f;
        public float witOkayHype = 2f;
        public float witAwkwardHype = -3f;
        [Range(0f, 1f)] public float witSuccessDonationChance = 0.10f;
        [Range(0f, 1f)] public float witHostingRatingBonus = 0.15f;
        [Range(0f, 0.1f)] public float witFollowConversionBonus = 0.005f;
        [Range(0f, 1f)] public float maximumWitHostingBonus = 0.60f;
        [Range(0f, 0.2f)] public float maximumWitFollowBonus = 0.02f;

        public int ChattersForViewers(int viewers)
        {
            if (viewers <= 0) return 0;
            int chatters = Mathf.CeilToInt(baseChatters + Mathf.Sqrt(viewers) * chattersPerViewerSquareRoot);
            return Mathf.Clamp(chatters, 1, Mathf.Min(viewers, maximumSimulatedChatters));
        }

        public float ChatDelayMultiplierForViewers(int viewers)
        {
            float divisor = 1f + Mathf.Log10(Mathf.Max(1, viewers)) * chatSpeedPerViewerLog;
            return Mathf.Clamp(1f / divisor, minimumChatDelayMultiplier, 1f);
        }

        public float EventReactionChanceForViewers(int viewers) => Mathf.Lerp(smallAudienceEventReactionChance,
            largeAudienceEventReactionChance, AudienceActivity01(viewers));

        public float EventCooldownForViewers(int viewers) => Mathf.Lerp(smallAudienceEventCooldown,
            largeAudienceEventCooldown, AudienceActivity01(viewers));

        private float AudienceActivity01(int viewers) => Mathf.Clamp01(Mathf.Log10(Mathf.Max(1, viewers) + 1f)
            / Mathf.Log10(Mathf.Max(10, viewersForMaximumChatActivity) + 1f));
    }
}
