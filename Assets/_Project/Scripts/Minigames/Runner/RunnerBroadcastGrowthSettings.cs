using UnityEngine;

namespace StreamOn.Minigames.Runner
{
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
        [Range(0f, 100f)] public float startingHype = 35f;
        [Range(0f, 100f)] public float restingHype = 30f;
        [Min(0f)] public float hypeReturnPerSecond = 0.08f;

        [Header("Gameplay Hype")]
        public float obstacleClearedHype = 1.5f;
        public float enemyDefeatedHype = 5f;
        public float attackMissedHype = -2.5f;
        public float playerHitHype = -9f;
        public float lowHealthDramaHype = 3f;
        public float completedBroadcastHype = 8f;
        public float defeatedHype = -12f;

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

        [Header("Follower Conversion")]
        [Range(0f, 1f)] public float baseFollowConversion = 0.08f;
        [Range(0f, 0.2f)] public float followConversionPerRatingPoint = 0.025f;
        [Range(0f, 0.2f)] public float followConversionPerTalkingLevel = 0.01f;
        [Range(0f, 0.2f)] public float completionFollowBonus = 0.03f;
        [Range(0f, 1f)] public float maximumFollowConversion = 0.40f;
        [Range(1f, 5f)] public float unfollowRatingThreshold = 2.5f;
        [Range(0f, 0.2f)] public float unfollowRatePerMissingRatingPoint = 0.03f;

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

        [Header("Live Donation Events")]
        [Min(0)] public int minimumViewersForDonation = 1;
        [Min(0.5f)] public float liveDonationCooldown = 9f;
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
