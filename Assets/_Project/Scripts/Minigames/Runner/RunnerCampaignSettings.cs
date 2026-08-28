using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace StreamOn.Minigames.Runner
{
    public enum RunnerCampaignLengthMode { Endless, FixedDays }
    public enum RunnerEquipmentType { Pc, Microphone, Fitness, Interior }
    public enum BroadcastGameId { Runner, TileArena, PlasticKnightmare }

    [Serializable]
    public sealed class BroadcastTimeBonusRule
    {
        [Min(1)] public int broadcastScoreThreshold = 1000;
        [Min(0f)] public float bonusSeconds = 10f;
    }

    [Serializable]
    public sealed class BroadcastGameRule
    {
        public BroadcastGameId gameId;
        public string displayName = "게임";
        [Tooltip("러너/타일 아레나 방송의 기본 실제 시간입니다. Plastic Knightmare에서는 사용하지 않습니다.")]
        [Min(1f)] public float baseDurationSeconds = 300f;
        [Tooltip("최종 방송 점수 기반 평점에서 4.0점(A)의 기준이 되는 점수입니다.")]
        [Min(1)] public int ratingTargetScore = 10000;
        [Min(0f)] public float maximumBonusSeconds = 20f;
        [Min(0f)] public float baseGameOverTimeLoss = 8f;
        public List<BroadcastTimeBonusRule> timeBonuses = new List<BroadcastTimeBonusRule>();
    }

    [Serializable]
    public sealed class BroadcasterLevelRule
    {
        [Min(1)] public int level = 1;
        [Min(0)] public int experienceToNextLevel = 100;
        [Min(0)] public int statPointsGranted = 1;
    }

    [Serializable]
    public sealed class WitRankRule
    {
        [Min(0)] public int rank;
        [Min(0)] public int upgradeCost;
        [Min(0f)] public float correctHeatGainBonus;
        [Min(0f)] public float responseTimeBonusSeconds;
        [Range(0f, 1f)] public float twoCorrectAnswerChance;
        [Min(0f)] public float advancedAnswerRewardMultiplier = 1f;
        [Min(0)] public int correctStreakRequired;
        [Min(1f)] public float correctStreakRewardMultiplier = 1f;
        [Range(0f, 100f)] public float comebackHeatThreshold;
        [Min(1f)] public float comebackRewardMultiplier = 1f;
    }

    [Serializable]
    public sealed class ComposureRankRule
    {
        [Min(0)] public int rank;
        [Min(0)] public int upgradeCost;
        [Range(0f, 1f)] public float ordinaryPenaltyReduction;
        [Min(0.1f)] public float poorStateTickInterval = 2f;
        [Range(0f, 1f)] public float neutralRecoveryTimeReduction;
        [Min(0)] public int extraMistakesRequiredForPoorState;
        [Range(0f, 1f)] public float oncePerBroadcastLargePenaltyReduction;
        public bool protectsFirstMistakeAfterGoodPlay;
        public bool correctWitClearsRecentMistakes;
        [Min(0f)] public float mistakeClearCooldownSeconds = 20f;
    }

    [Serializable]
    public sealed class ControlRankRule
    {
        [Min(0)] public int rank;
        [Min(0)] public int upgradeCost;
        [Min(1f)] public float maximumFocus = 100f;
        [Min(0f)] public float focusRecoveryBonus;
        [Min(0f)] public float focusRecoveryDelayReduction;
        [Range(0f, 1f)] public float focusDrainReduction;
        [Min(0f)] public float depletionRecoveryAmount;
    }

    [Serializable]
    public sealed class ManagerTierRule
    {
        [Min(0)] public int tier;
        public string displayName = "매니저";
        [Min(0)] public long unlockCost;
        [Min(0)] public long hireCostPerBroadcast;
        [Tooltip("분탕 이벤트가 발생할 때 매니저가 즉시 해결할 확률입니다.")]
        [Range(0f, 1f)] public float conflictResolveChance = .35f;
        [Tooltip("친목 이벤트가 발생할 때 매니저가 즉시 해결할 확률입니다.")]
        [Range(0f, 1f)] public float fraternizationResolveChance = .20f;
    }

    [Serializable]
    public sealed class PlasticNightScoreRule
    {
        [Min(1)] public int night = 1;
        [Min(0)] public int ghostBaseScore = 25;
        [Min(0)] public int clearBonus = 400;
    }

    [Serializable]
    public sealed class RunnerCampaignActionDefinition
    {
        public string id = "action";
        public string displayName = "행동";
        [TextArea] public string description = "행동 설명";
        public int gameSkillDelta;
        public int talkingSkillDelta;
        public int healthStatDelta;
        [Tooltip("멘탈 스탯 경험치 증가량. 멘탈은 더 이상 소모되지 않습니다.")]
        [Min(0)]
        public int mentalExperienceDelta;
        public int subscriberDelta;
        public Color buttonColor = new Color(0.18f, 0.72f, 0.64f);
        [Header("3D Room Presentation")]
        public Vector3 roomPosition;
        public Vector3 roomScale = Vector3.one;
        public Color roomColor = Color.white;
        public GameObject roomPrefab;
    }

    [CreateAssetMenu(fileName = "Runner Campaign Settings", menuName = "STREAM ON/Runner/Campaign Settings")]
    public sealed class RunnerCampaignSettings : ScriptableObject
    {
        public const int MaximumEquipmentLevel = 5;
        [Header("Campaign Length")]
        public RunnerCampaignLengthMode lengthMode = RunnerCampaignLengthMode.Endless;
        [Min(1)] public int fixedMaximumDays = 7;

        [Header("Starting State")]
        [Min(0)] public int startingSubscribers = 0;
        public string defaultStreamerName = "신입스트리머";
        [HideInInspector]
        [Range(1, 3)] public int startingMentalLevel = 1;
        [HideInInspector]
        [Min(1)] public int startingGameSkill = 1;
        [HideInInspector]
        [Min(1)] public int startingTalkingSkill = 1;
        [HideInInspector]
        [Min(1)] public int startingHealthStat = 1;

        // 기존 저장 데이터 변환에만 사용되는 구 스탯 값입니다. 새 게임 설정은
        // 아래 Broadcaster Level & Stat Points의 재치/평정심/통제력 표에서 조절합니다.
        [HideInInspector]
        [Range(1, 3)] public int maximumMentalLevel = 3;
        [HideInInspector]
        [Range(1, 3)] public int maximumGameSkill = 3;
        [HideInInspector]
        [Range(1, 3)] public int maximumTalkingSkill = 3;
        [HideInInspector]
        [Range(1, 3)] public int maximumHealthStat = 3;
        [HideInInspector]
        [Min(1)] public int experienceForLevel2 = 100;
        [HideInInspector]
        [Min(1)] public int experienceForLevel3 = 150;

        [HideInInspector]
        [Range(0f, 1f)] public float hypeGainBonusPerMentalLevel = 0.15f;
        [HideInInspector]
        [Range(0f, 0.45f)] public float hypePenaltyReductionPerMentalLevel = 0.15f;

        [HideInInspector]
        public List<RunnerCampaignActionDefinition> dayActions = new List<RunnerCampaignActionDefinition>();

        [Header("Broadcast Goals")]
        [Min(50)] public int firstDayTargetScore = 350;
        [Min(0)] public int targetScoreIncreasePerDay = 120;
        [Min(50)] public int maximumTargetScore = 1550;
        [Range(0.1f, 5f)] public float resultDelay = 1.4f;

        [Header("Broadcast Time Limit")]
        [Min(1f)] public float baseBroadcastSeconds = 300f;
        [Min(1f)] public float minimumBroadcastSeconds = 300f;
        [Min(1f)] public float maximumBroadcastSeconds = 600f;
        [Min(0f)] public float secondsPerHealthStatLevel = 60f;

        [Header("New Broadcast Session Rules")]
        public List<BroadcastGameRule> gameRules = new List<BroadcastGameRule>
        {
            new BroadcastGameRule { gameId = BroadcastGameId.Runner, displayName = "러너", baseDurationSeconds = 300f,
                ratingTargetScore = 10000,
                maximumBonusSeconds = 20f, baseGameOverTimeLoss = 8f,
                timeBonuses = new List<BroadcastTimeBonusRule> { new BroadcastTimeBonusRule { broadcastScoreThreshold = 2500, bonusSeconds = 10f }, new BroadcastTimeBonusRule { broadcastScoreThreshold = 6000, bonusSeconds = 10f } } },
            new BroadcastGameRule { gameId = BroadcastGameId.TileArena, displayName = "타일 아레나", baseDurationSeconds = 300f,
                ratingTargetScore = 300,
                maximumBonusSeconds = 20f, baseGameOverTimeLoss = 8f,
                timeBonuses = new List<BroadcastTimeBonusRule> { new BroadcastTimeBonusRule { broadcastScoreThreshold = 120, bonusSeconds = 10f }, new BroadcastTimeBonusRule { broadcastScoreThreshold = 300, bonusSeconds = 10f } } },
            new BroadcastGameRule { gameId = BroadcastGameId.PlasticKnightmare, displayName = "Plastic Knightmare",
                baseDurationSeconds = 60f, ratingTargetScore = 1200 }
        };
        [Tooltip("열기 0/50/100에서 방송 점수에 곱해지는 배율입니다.")]
        public AnimationCurve heatScoreMultiplier = new AnimationCurve(new Keyframe(0f, 0.85f), new Keyframe(50f, 1f), new Keyframe(100f, 1.25f));
        [Min(0f)] public float sessionAutosaveIntervalSeconds = 5f;
        [Header("Leaderboard")]
        [Min(1)] public int leaderboardMaximumRows = 50;
        public bool useOnlineLeaderboard = true;
        [Tooltip("Unity Dashboard에서 만든 UGS Environment 이름입니다.")]
        public string leaderboardEnvironmentName = "production";
        [Tooltip("러너 게임 최고점수 UGS Leaderboard ID")]
        public string runnerLeaderboardId = "stream-on-runner";
        [Tooltip("타일 아레나 최고점수 UGS Leaderboard ID")]
        public string tileArenaLeaderboardId = "stream-on-tile-arena";
        [Tooltip("Plastic Knightmare 최고점수 UGS Leaderboard ID")]
        public string plasticKnightmareLeaderboardId = "stream-on-plastic-knightmare";
        [Tooltip("현재 팔로워 수 UGS Leaderboard ID. Dashboard에서는 Update Type을 Latest Score로 설정합니다.")]
        public string followerLeaderboardId = "stream-on-followers";
        [Tooltip("기록 저장 시 전송 대기열에 자동 등록하고, 온라인 연결 시 전송합니다.")]
        public bool automaticallySubmitLeaderboardRecords = true;
        [Min(1)] public int maximumPendingLeaderboardSubmissions = 4;

        [Header("Broadcaster Level & Stat Points")]
        [Min(1)] public int maximumBroadcasterLevel = 20;
        [Min(0)] public int startingBroadcasterLevel = 1;
        [Min(0)] public int startingStatPoints = 1;
        public List<BroadcasterLevelRule> broadcasterLevels = new List<BroadcasterLevelRule>();
        public List<WitRankRule> witRanks = new List<WitRankRule>();
        [FormerlySerializedAs("mentalRanks")]
        [Tooltip("평정심 단계별 열기 페널티 완화 및 상태 회복 설정")]
        public List<ComposureRankRule> composureRanks = new List<ComposureRankRule>();
        [FormerlySerializedAs("staminaRanks")]
        [Tooltip("통제력 단계별 집중력 최대치, 회복, 슬로우모션 효율 설정")]
        public List<ControlRankRule> controlRanks = new List<ControlRankRule>();
        [Min(0)] public int broadcastCompletionExperience = 45;
        [Min(0)] public int broadcastRatingExperiencePerPoint = 5;
        [Min(0)] public int newRecordExperience = 30;
        [Min(0)] public int correctModerationExperience = 15;
        [Min(0)] public int correctWitExperience = 6;
        [Min(0)] public int runnerObstacleClearExperience = 1;
        [Min(0)] public int runnerEnemyDefeatExperience = 2;
        [Min(0)] public int tileStageClearExperience = 8;
        [Min(0)] public int plasticNightClearExperience = 25;
        [Min(0)] public int maximumExperiencePerBroadcast = 120;
        [Min(0)] public long respecCashCost = 2500;
        public bool firstRespecIsFree = true;

        [Header("Focus / Slow Motion")]
        [Range(0.01f, 1f)] public float slowMotionTimeScale = 0.24f;
        [Min(0f)] public float focusDrainPerSecond = 24f;
        [Min(0f)] public float focusRecoveryPerSecond = 13f;
        [Min(0f)] public float focusRecoveryDelaySeconds = 1.5f;
        [Min(0f)] public float heatGaugeVisualSpeed = 55f;
        [Min(0f)] public float largeHeatPenaltyThreshold = 8f;

        [Header("Manager")]
        public List<ManagerTierRule> managerTiers = new List<ManagerTierRule>
        {
            new ManagerTierRule { tier = 1, displayName = "연습생 매니저", unlockCost = 50000, hireCostPerBroadcast = 8000, conflictResolveChance = .35f, fraternizationResolveChance = .20f },
            new ManagerTierRule { tier = 2, displayName = "일반 매니저", unlockCost = 180000, hireCostPerBroadcast = 20000, conflictResolveChance = .65f, fraternizationResolveChance = .50f },
            new ManagerTierRule { tier = 3, displayName = "프로 매니저", unlockCost = 500000, hireCostPerBroadcast = 45000, conflictResolveChance = .90f, fraternizationResolveChance = .80f }
        };

        [Header("Plastic Knightmare Broadcast")]
        [Min(1f)] public float plasticDayPreparationSeconds = 120f;
        [Range(0f, 100f)] public float plasticNightStartingHeatMinimum = 40f;
        [Range(0f, 100f)] public float plasticNightStartingHeatMaximum = 60f;
        [Min(0)] public int plasticBedHealthScore = 200;
        [Min(0)] public int plasticSurvivingFacilityScore = 40;
        [Min(0)] public int plasticNoHitBonus = 250;
        [Min(0)] public int plasticFollowerGainPerClearedNight = 8;
        [Min(0)] public int plasticFollowerLossOnFailure = 4;
        [Min(0)] public int plasticDonationCapPerNight = 10000;
        [Min(0f)] public float plasticDonationWonPerBroadcastPoint = 0.4f;
        [Min(0)] public int plasticStartingViewers = 3;
        [Min(0)] public int plasticMaximumViewers = 100000;
        [Min(0f)] public float plasticViewersPerNight = 1.5f;
        [Range(0f, 1f)] public float plasticGhostDonationChance = .08f;
        [Min(0)] public int plasticFollowersPerHeatBand = 2;
        [Min(1f)] public float plasticFollowerHeatBandSize = 25f;
        [Min(0f)] public float plasticRatingScoreTargetMultiplier = 3f;
        public int[] plasticGhostTierScoreMultipliers = { 1, 2, 3 };
        public List<PlasticNightScoreRule> plasticNightScoreRules = new List<PlasticNightScoreRule>
        {
            new PlasticNightScoreRule { night = 1, ghostBaseScore = 25, clearBonus = 400 },
            new PlasticNightScoreRule { night = 5, ghostBaseScore = 40, clearBonus = 700 },
            new PlasticNightScoreRule { night = 10, ghostBaseScore = 60, clearBonus = 1100 }
        };

        [Header("Broadcast Retry Rules")]
        [Tooltip("게임오버 한 번마다 방송의 남은 시간에서 차감되는 시간입니다.")]
        [Min(0f)] public float gameOverTimePenaltySeconds = 8f;

        [Header("Broadcast Audience & Economy")]
        public RunnerBroadcastGrowthSettings broadcastGrowthSettings;

        [Header("Equipment Upgrade Economy")]
        [Tooltip("0번은 미사용, 이후 항목은 Lv.2~Lv.5 구매 가격입니다.")]
        public int[] pcUpgradeCosts = { 0, 10000, 20000, 40000, 80000 };
        public int[] microphoneUpgradeCosts = { 0, 8000, 16000, 32000, 64000 };
        public int[] fitnessUpgradeCosts = { 0, 7000, 14000, 28000, 56000 };
        public int[] interiorUpgradeCosts = { 0, 6000, 12000, 24000, 48000 };
        [Min(0f)] public float scoreBonusPerPcUpgrade = 0.06f;
        [Min(0f)] public float followerConversionBonusPerMicrophoneUpgrade = 0.008f;
        [Min(0f)] public float donationBonusPerMicrophoneUpgrade = 0.08f;
        [Min(0f)] public float broadcastSecondsPerFitnessUpgrade = 30f;
        [Tooltip("팔로워 중 방송을 시청하는 기본 비율입니다. Lv.1 웹캠 기준입니다.")]
        [Range(0f, 1f)] public float baseViewerRatio = 0.40f;
        [Tooltip("웹캠 레벨이 하나 오를 때 추가되는 팔로워 대비 시청자 비율입니다.")]
        [Range(0f, .25f)] public float viewerRatioBonusPerWebcamUpgrade = 0.05f;
        [HideInInspector] [Min(0f)] public float startingViewersPerInteriorUpgrade = 2f;
        [HideInInspector] [Min(0f)] public float managerDelayReductionPerPcUpgrade = 0.08f;
        [Min(0f)] public float focusCapacityPerFitnessUpgrade = 15f;
        [Min(0f)] public float focusRecoveryPerFitnessUpgrade = 0.10f;

        [Header("Result Balance")]
        public int successBaseSubscriberGain = 10;
        public int failureBaseSubscriberLoss = 16;
        public int failureSubscriberLossIncreasePerDay = 2;
        [Min(1)] public int maximumFailureScalingDay = 50;
        [Min(1)] public int scorePerSubscriber = 65;
        public int subscriberGainPerEnemy = 3;
        public int subscriberPenaltyPerHit = 2;
        public int subscriberGainPerTalkingLevel = 3;
        [Min(1)] public int maximumEffectiveTalkingLevel = 10;
        [Header("Pre-Broadcast Safety Floor")]
        [Min(0)] public int minimumSubscribersToStartBroadcast = 0;

        [Header("Persistence")]
        public bool enableAutomaticSave = true;
        [Range(1, 8)] public int saveSlotCount = 3;
        public string saveFolderName = "Saves";
        [Tooltip("기존 PlayerPrefs 저장을 최초 1회 파일 슬롯으로 이전할 때만 사용합니다.")]
        public string playerPrefsSaveKey = "StreamOn.RunnerCampaign.Save.v1";
        [Min(1)] public int maximumStoredDayRecords = 100;

        [Header("Scene Flow")]
        public bool useThreeDimensionalRoomFlow = true;
        public string roomSceneName = "StreamerRoom";
        [Tooltip("기존 저장/씬과의 호환을 위한 러너 씬 이름")]
        public string broadcastSceneName = "BroadcastRunner";
        public string runnerMenuSceneName = "RunnerMainMenu";
        public string runnerSceneName = "BroadcastRunner";
        public string tileArenaMenuSceneName = "TileArenaMainMenu";
        public string tileArenaSceneName = "TileArena";
        public string plasticKnightmareMenuSceneName = "MainMenu";
        public string plasticKnightmareSceneName = "MainScene";

        public bool IsEndless => lengthMode == RunnerCampaignLengthMode.Endless;

        public int TargetScoreForDay(int day)
        {
            long target = firstDayTargetScore + (long)Mathf.Max(0, day - 1) * targetScoreIncreasePerDay;
            return Mathf.Clamp((int)Mathf.Min(target, int.MaxValue), firstDayTargetScore, Mathf.Max(firstDayTargetScore, maximumTargetScore));
        }

        public float BroadcastSecondsForHealth(int healthStat, int fitnessLevel = 1)
        {
            float duration = baseBroadcastSeconds + Mathf.Max(0, healthStat - startingHealthStat) * secondsPerHealthStatLevel
                + Mathf.Max(0, fitnessLevel - 1) * broadcastSecondsPerFitnessUpgrade;
            return Mathf.Clamp(duration, minimumBroadcastSeconds,
                maximumBroadcastSeconds + broadcastSecondsPerFitnessUpgrade * (MaximumEquipmentLevel - 1));
        }

        public float ViewerRatioForWebcamLevel(int webcamLevel) => Mathf.Clamp01(baseViewerRatio
            + Mathf.Max(0, Mathf.Clamp(webcamLevel, 1, MaximumEquipmentLevel) - 1) * viewerRatioBonusPerWebcamUpgrade);

        public BroadcastGameRule GameRule(BroadcastGameId gameId)
        {
            BroadcastGameRule rule = gameRules?.Find(candidate => candidate != null && candidate.gameId == gameId);
            return rule ?? new BroadcastGameRule { gameId = gameId, displayName = gameId.ToString(), baseDurationSeconds = baseBroadcastSeconds, baseGameOverTimeLoss = gameOverTimePenaltySeconds };
        }

        public int RatingTargetScore(BroadcastGameId gameId)
        {
            BroadcastGameRule rule = GameRule(gameId);
            if (rule != null && rule.ratingTargetScore > 0) return rule.ratingTargetScore;
            return gameId == BroadcastGameId.TileArena ? 300 : gameId == BroadcastGameId.PlasticKnightmare ? 1200 : 10000;
        }

        public string LeaderboardId(BroadcastGameId gameId, bool followerBoard)
        {
            if (followerBoard) return followerLeaderboardId;
            return gameId == BroadcastGameId.Runner ? runnerLeaderboardId
                : gameId == BroadcastGameId.TileArena ? tileArenaLeaderboardId
                : plasticKnightmareLeaderboardId;
        }

        public float HeatScoreMultiplier(float heat) => Mathf.Max(0f, heatScoreMultiplier != null ? heatScoreMultiplier.Evaluate(Mathf.Clamp(heat, 0f, 100f)) : 1f);
        public WitRankRule WitRule(int rank) => RuleAt(witRanks, rank);
        public ComposureRankRule ComposureRule(int rank) => RuleAt(composureRanks, rank);
        public ControlRankRule ControlRule(int rank) => RuleAt(controlRanks, rank);
        private static T RuleAt<T>(List<T> rules, int rank) where T : class => rules != null && rules.Count > 0 ? rules[Mathf.Clamp(rank, 0, rules.Count - 1)] : null;

        public int UpgradeCost(RunnerEquipmentType type, int targetLevel)
        {
            int[] costs = type switch
            {
                RunnerEquipmentType.Pc => pcUpgradeCosts,
                RunnerEquipmentType.Microphone => microphoneUpgradeCosts,
                RunnerEquipmentType.Fitness => fitnessUpgradeCosts,
                _ => interiorUpgradeCosts
            };
            // Element 1 is Lv.1 -> Lv.2, through element 4 for Lv.4 -> Lv.5.
            return costs != null && targetLevel >= 2 && targetLevel - 1 < costs.Length
                ? Mathf.Max(0, costs[targetLevel - 1])
                : int.MaxValue;
        }

        public int ExperienceRequiredForLevel(int level) => level <= 1
            ? Mathf.Max(1, experienceForLevel2)
            : Mathf.Max(1, experienceForLevel3);

        public void AddStatExperience(ref int level, ref int experience, int amount, int maximumLevel = 3)
        {
            level = Mathf.Clamp(level, 1, Mathf.Max(1, maximumLevel));
            experience = Mathf.Max(0, experience + amount);
            while (level < maximumLevel && experience >= ExperienceRequiredForLevel(level))
            {
                experience -= ExperienceRequiredForLevel(level);
                level++;
            }
            if (level >= maximumLevel) experience = 0;
        }

        private void OnValidate()
        {
            EnsureNewProgressionRules();
            fixedMaximumDays = Mathf.Max(1, fixedMaximumDays);
            scorePerSubscriber = Mathf.Max(1, scorePerSubscriber);
            maximumTargetScore = Mathf.Max(firstDayTargetScore, maximumTargetScore);
            maximumBroadcastSeconds = Mathf.Max(minimumBroadcastSeconds, maximumBroadcastSeconds);
            baseBroadcastSeconds = Mathf.Clamp(baseBroadcastSeconds, minimumBroadcastSeconds, maximumBroadcastSeconds);
            gameOverTimePenaltySeconds = Mathf.Max(0f, gameOverTimePenaltySeconds);
            maximumEffectiveTalkingLevel = Mathf.Max(1, maximumEffectiveTalkingLevel);
            maximumFailureScalingDay = Mathf.Max(1, maximumFailureScalingDay);
            maximumStoredDayRecords = Mathf.Max(1, maximumStoredDayRecords);
            startingMentalLevel = Mathf.Clamp(startingMentalLevel, 1, 3);
            maximumMentalLevel = Mathf.Clamp(maximumMentalLevel, 1, 3);
            startingMentalLevel = Mathf.Min(startingMentalLevel, maximumMentalLevel);
            maximumGameSkill = Mathf.Clamp(maximumGameSkill, 1, 3);
            maximumTalkingSkill = Mathf.Clamp(maximumTalkingSkill, 1, 3);
            maximumHealthStat = Mathf.Clamp(maximumHealthStat, 1, 3);
            experienceForLevel2 = Mathf.Max(1, experienceForLevel2);
            experienceForLevel3 = Mathf.Max(1, experienceForLevel3);
            saveSlotCount = Mathf.Clamp(saveSlotCount, 1, 8);
            if (dayActions == null) dayActions = new List<RunnerCampaignActionDefinition>();
            EnsureUpgradeCosts(ref pcUpgradeCosts, 10000, 20000, 40000, 80000);
            EnsureUpgradeCosts(ref microphoneUpgradeCosts, 8000, 16000, 32000, 64000);
            EnsureUpgradeCosts(ref fitnessUpgradeCosts, 7000, 14000, 28000, 56000);
            EnsureUpgradeCosts(ref interiorUpgradeCosts, 6000, 12000, 24000, 48000);
        }

        private void EnsureNewProgressionRules()
        {
            if (gameRules == null) gameRules = new List<BroadcastGameRule>();
            if (!gameRules.Exists(rule => rule != null && rule.gameId == BroadcastGameId.Runner))
                gameRules.Add(new BroadcastGameRule
                {
                    gameId = BroadcastGameId.Runner, displayName = "러너", baseDurationSeconds = 300f,
                    ratingTargetScore = 10000,
                    maximumBonusSeconds = 20f, baseGameOverTimeLoss = 8f,
                    timeBonuses = new List<BroadcastTimeBonusRule>
                    {
                        new BroadcastTimeBonusRule { broadcastScoreThreshold = 2500, bonusSeconds = 10f },
                        new BroadcastTimeBonusRule { broadcastScoreThreshold = 6000, bonusSeconds = 10f }
                    }
                });
            if (!gameRules.Exists(rule => rule != null && rule.gameId == BroadcastGameId.TileArena))
                gameRules.Add(new BroadcastGameRule
                {
                    gameId = BroadcastGameId.TileArena, displayName = "타일 아레나", baseDurationSeconds = 300f,
                    ratingTargetScore = 300,
                    maximumBonusSeconds = 20f, baseGameOverTimeLoss = 8f,
                    timeBonuses = new List<BroadcastTimeBonusRule>
                    {
                        new BroadcastTimeBonusRule { broadcastScoreThreshold = 120, bonusSeconds = 10f },
                        new BroadcastTimeBonusRule { broadcastScoreThreshold = 300, bonusSeconds = 10f }
                    }
                });
            if (!gameRules.Exists(rule => rule != null && rule.gameId == BroadcastGameId.PlasticKnightmare))
                gameRules.Add(new BroadcastGameRule
                {
                    gameId = BroadcastGameId.PlasticKnightmare, displayName = "Plastic Knightmare",
                    baseDurationSeconds = 60f, ratingTargetScore = 1200
                });
            foreach (BroadcastGameRule rule in gameRules)
            {
                if (rule == null || rule.ratingTargetScore > 0) continue;
                rule.ratingTargetScore = rule.gameId == BroadcastGameId.TileArena ? 300
                    : rule.gameId == BroadcastGameId.PlasticKnightmare ? 1200 : 10000;
            }
            maximumBroadcasterLevel = Mathf.Max(1, maximumBroadcasterLevel);
            if (broadcasterLevels == null) broadcasterLevels = new List<BroadcasterLevelRule>();
            if (broadcasterLevels.Count == 0)
                for (int level = 1; level <= maximumBroadcasterLevel; level++)
                    broadcasterLevels.Add(new BroadcasterLevelRule
                    {
                        level = level,
                        experienceToNextLevel = 70 + level * 15,
                        statPointsGranted = level == 10 || level == 20 ? 2 : 1
                    });
            if (witRanks == null || witRanks.Count == 0)
                witRanks = new List<WitRankRule>
                {
                    new WitRankRule { rank = 0, upgradeCost = 0, advancedAnswerRewardMultiplier = 1f },
                    new WitRankRule { rank = 1, upgradeCost = 1, correctHeatGainBonus = .15f, responseTimeBonusSeconds = .75f, advancedAnswerRewardMultiplier = 1f },
                    new WitRankRule { rank = 2, upgradeCost = 2, correctHeatGainBonus = .30f, responseTimeBonusSeconds = 1.5f, twoCorrectAnswerChance = .25f, advancedAnswerRewardMultiplier = 1f },
                    new WitRankRule { rank = 3, upgradeCost = 3, correctHeatGainBonus = .45f, responseTimeBonusSeconds = 2.25f, twoCorrectAnswerChance = .50f, advancedAnswerRewardMultiplier = 1f, correctStreakRequired = 3, correctStreakRewardMultiplier = 1.35f },
                    new WitRankRule { rank = 4, upgradeCost = 4, correctHeatGainBonus = .60f, responseTimeBonusSeconds = 3f, twoCorrectAnswerChance = .75f, advancedAnswerRewardMultiplier = 1f, correctStreakRequired = 3, correctStreakRewardMultiplier = 1.35f, comebackHeatThreshold = 35f, comebackRewardMultiplier = 1.55f },
                    new WitRankRule { rank = 5, upgradeCost = 5, correctHeatGainBonus = .80f, responseTimeBonusSeconds = 4f, twoCorrectAnswerChance = 1f, advancedAnswerRewardMultiplier = 1.3f, correctStreakRequired = 3, correctStreakRewardMultiplier = 1.45f, comebackHeatThreshold = 40f, comebackRewardMultiplier = 1.7f }
                };
            foreach (WitRankRule rule in witRanks)
            {
                if (rule == null) continue;
                rule.advancedAnswerRewardMultiplier = Mathf.Max(1f, rule.advancedAnswerRewardMultiplier);
                rule.correctStreakRewardMultiplier = Mathf.Max(1f, rule.correctStreakRewardMultiplier);
                rule.comebackRewardMultiplier = Mathf.Max(1f, rule.comebackRewardMultiplier);
                // Existing serialized assets predate these traits. Populate only the
                // missing high-rank defaults; afterwards every value stays editable.
                if (rule.rank >= 3 && rule.correctStreakRequired <= 0)
                {
                    rule.correctStreakRequired = 3;
                    rule.correctStreakRewardMultiplier = rule.rank >= 5 ? 1.45f : 1.35f;
                }
                if (rule.rank >= 4 && rule.comebackHeatThreshold <= 0f)
                {
                    rule.comebackHeatThreshold = rule.rank >= 5 ? 40f : 35f;
                    rule.comebackRewardMultiplier = rule.rank >= 5 ? 1.7f : 1.55f;
                }
            }
            if (composureRanks == null || composureRanks.Count == 0)
                composureRanks = new List<ComposureRankRule>
                {
                    new ComposureRankRule { rank = 0, upgradeCost = 0, poorStateTickInterval = 2f },
                    new ComposureRankRule { rank = 1, upgradeCost = 1, ordinaryPenaltyReduction = .12f, poorStateTickInterval = 2.3f, neutralRecoveryTimeReduction = .10f },
                    new ComposureRankRule { rank = 2, upgradeCost = 2, ordinaryPenaltyReduction = .24f, poorStateTickInterval = 2.6f, neutralRecoveryTimeReduction = .20f, protectsFirstMistakeAfterGoodPlay = true },
                    new ComposureRankRule { rank = 3, upgradeCost = 3, ordinaryPenaltyReduction = .36f, poorStateTickInterval = 3f, neutralRecoveryTimeReduction = .30f, protectsFirstMistakeAfterGoodPlay = true, correctWitClearsRecentMistakes = true, mistakeClearCooldownSeconds = 20f },
                    new ComposureRankRule { rank = 4, upgradeCost = 4, ordinaryPenaltyReduction = .48f, poorStateTickInterval = 3.5f, neutralRecoveryTimeReduction = .40f, extraMistakesRequiredForPoorState = 1, protectsFirstMistakeAfterGoodPlay = true, correctWitClearsRecentMistakes = true, mistakeClearCooldownSeconds = 20f },
                    new ComposureRankRule { rank = 5, upgradeCost = 5, ordinaryPenaltyReduction = .60f, poorStateTickInterval = 4f, neutralRecoveryTimeReduction = .50f, extraMistakesRequiredForPoorState = 1, oncePerBroadcastLargePenaltyReduction = .80f, protectsFirstMistakeAfterGoodPlay = true, correctWitClearsRecentMistakes = true, mistakeClearCooldownSeconds = 20f }
                };
            if (controlRanks == null || controlRanks.Count == 0)
                controlRanks = new List<ControlRankRule>
                {
                    new ControlRankRule { rank = 0, upgradeCost = 0, maximumFocus = 100f },
                    new ControlRankRule { rank = 1, upgradeCost = 1, maximumFocus = 120f, focusRecoveryBonus = .15f },
                    new ControlRankRule { rank = 2, upgradeCost = 2, maximumFocus = 140f, focusRecoveryBonus = .30f, focusRecoveryDelayReduction = .7f },
                    new ControlRankRule { rank = 3, upgradeCost = 3, maximumFocus = 160f, focusRecoveryBonus = .45f, focusRecoveryDelayReduction = .7f, focusDrainReduction = .08f },
                    new ControlRankRule { rank = 4, upgradeCost = 4, maximumFocus = 190f, focusRecoveryBonus = .60f, focusRecoveryDelayReduction = .7f, focusDrainReduction = .15f },
                    new ControlRankRule { rank = 5, upgradeCost = 5, maximumFocus = 220f, focusRecoveryBonus = .80f, focusRecoveryDelayReduction = .7f, focusDrainReduction = .20f, depletionRecoveryAmount = 35f }
                };
        }

        private static void EnsureUpgradeCosts(ref int[] values, params int[] defaults)
        {
            int[] expanded = new int[MaximumEquipmentLevel];
            expanded[0] = 0;
            for (int index = 1; index < expanded.Length; index++)
                expanded[index] = values != null && index < values.Length
                    ? Mathf.Max(0, values[index])
                    : Mathf.Max(0, defaults[Mathf.Min(index - 1, defaults.Length - 1)]);
            values = expanded;
            values[0] = 0;
        }
    }

    [Serializable]
    public sealed class RunnerCampaignDayRecord
    {
        public int day;
        public string selectedAction;
        public int score;
        public int targetScore;
        public bool succeeded;
        public int enemiesDefeated;
        public int hitsTaken;
        public int subscriberDelta;
        public float mentalDelta;
        public int subscribersAfter;
        public float mentalAfter;
        public float broadcastRating;
        public int peakViewers;
        public float averageViewers;
        public int totalVisitors;
        public int donationWon;
    }

    [Serializable]
    public sealed class PlasticKnightmareInventoryEntry
    {
        public string id;
        public int count;
        public int slot = -1;
    }

    [Serializable]
    public sealed class PlasticKnightmarePlacedObject
    {
        public string id;
        public int cellX;
        public int cellY;
        public int stackIndex;
        public float rotationY;
        public float currentHealth;
    }

    [Serializable]
    public sealed class PlasticKnightmareSaveData
    {
        public bool initialized;
        public int day = 1;
        public int coins = 1000;
        public int attackUpgradeLevel;
        public int healthUpgradeLevel;
        public List<PlasticKnightmareInventoryEntry> brickInventory = new List<PlasticKnightmareInventoryEntry>();
        public List<PlasticKnightmareInventoryEntry> companionInventory = new List<PlasticKnightmareInventoryEntry>();
        public List<PlasticKnightmarePlacedObject> placedBricks = new List<PlasticKnightmarePlacedObject>();
        public List<PlasticKnightmarePlacedObject> placedCompanions = new List<PlasticKnightmarePlacedObject>();
    }

    [Serializable]
    public sealed class BroadcastLeaderboardRecord
    {
        public BroadcastGameId gameId;
        public int highestClearedNight;
        public int bestBroadcastScore;
        public string achievedAtUtc;
    }

    [Serializable]
    public sealed class RunnerCampaignSaveData
    {
        public int version = RunnerCampaignSaveStore.CurrentVersion;
        public string streamerName;
        public string playerId;
        public int slot;
        public string savedAtUtc;
        public int day;
        public int subscribers;
        // Legacy v5 value retained only so old JSON files can be migrated safely.
        public float mental;
        public int mentalLevel = 1;
        public int mentalExperience;
        public int gameSkill;
        public int gameSkillExperience;
        public int talkingSkill;
        public int talkingSkillExperience;
        public int healthStat;
        public int healthStatExperience;
        public int bestBroadcastScore;
        public long lifetimeDonations;
        public long cash;
        public int pcLevel = 1;
        public int microphoneLevel = 1;
        public int fitnessLevel = 1;
        public int interiorLevel = 1;
        public float lastBroadcastRating;
        public int lastPeakViewers;
        public float lastAverageViewers;
        public bool campaignFailed;
        public bool awaitingAdvance;
        public bool broadcastPending;
        public string selectedAction;
        public string selectedBroadcastGame;
        public bool broadcastSessionActive;
        public float broadcastSessionDurationSeconds;
        public float broadcastSessionRemainingSeconds;
        public float broadcastSessionElapsedSeconds;
        public string broadcastSessionGameId;
        public int broadcastSessionRawScore;
        public int broadcastSessionScore;
        public float broadcastSessionGrantedBonusSeconds;
        public int broadcastSessionNextBonusIndex;
        public int broadcasterLevel = 1;
        public int broadcasterExperience;
        public int broadcastSessionExperienceEarned;
        public int unspentStatPoints = 1;
        public int witRank;
        public int witInvestedPoints;
        // These legacy JSON field names are intentionally retained so existing saves
        // migrate without losing invested points. Runtime code uses the semantic aliases below.
        public int mentalRank;
        public int staminaRank;
        public int composureInvestedPoints;
        public int controlInvestedPoints;
        public int ComposureRank { get => mentalRank; set => mentalRank = value; }
        public int ControlRank { get => staminaRank; set => staminaRank = value; }
        public bool freeRespecUsed;
        public int unlockedManagerTier;
        public int hiredManagerTier;
        public int managerUsesRemaining;
        public int bestRunnerBroadcastScore;
        public int bestTileArenaBroadcastScore;
        public int bestRunnerGameScore;
        public int bestTileArenaGameScore;
        public int bestPlasticNight;
        public int bestPlasticBroadcastScoreAtNight;
        public int bestPlasticGameScoreAtNight;
        public List<BroadcastLeaderboardRecord> leaderboardRecords = new List<BroadcastLeaderboardRecord>();
        public PlasticKnightmareSaveData plasticKnightmare = new PlasticKnightmareSaveData();
        public List<RunnerCampaignDayRecord> records = new List<RunnerCampaignDayRecord>();
    }

}
