using System;
using System.Collections.Generic;
using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    public enum RunnerCampaignLengthMode { Endless, FixedDays }

    [Serializable]
    public sealed class RunnerCampaignActionDefinition
    {
        public string id = "action";
        public string displayName = "행동";
        [TextArea] public string description = "행동 설명";
        public int gameSkillDelta;
        public int talkingSkillDelta;
        public int healthStatDelta;
        public float mentalDelta;
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
        [Header("Campaign Length")]
        public RunnerCampaignLengthMode lengthMode = RunnerCampaignLengthMode.Endless;
        [Min(1)] public int fixedMaximumDays = 7;

        [Header("Starting State")]
        [Min(0)] public int startingSubscribers = 0;
        [Range(1f, 100f)] public float startingMental = 70f;
        [Min(1)] public int startingGameSkill = 1;
        [Min(1)] public int startingTalkingSkill = 1;
        [Min(1)] public int startingHealthStat = 1;

        [Header("Stat Progression (Lv.1 - Lv.3)")]
        [Tooltip("멘탈은 레벨 스탯이 아니라 매일 오르내리는 컨디션 자원입니다.")]
        [Min(1f)] public float maximumMental = 100f;
        [Range(1, 3)] public int maximumGameSkill = 3;
        [Range(1, 3)] public int maximumTalkingSkill = 3;
        [Range(1, 3)] public int maximumHealthStat = 3;
        [Tooltip("Lv.1에서 Lv.2로 오르는 데 필요한 경험치")]
        [Min(1)] public int experienceForLevel2 = 100;
        [Tooltip("Lv.2에서 Lv.3으로 오르는 데 필요한 경험치")]
        [Min(1)] public int experienceForLevel3 = 150;

        [Header("Day Actions")]
        public List<RunnerCampaignActionDefinition> dayActions = new List<RunnerCampaignActionDefinition>();

        [Header("Broadcast Goals")]
        [Min(50)] public int firstDayTargetScore = 350;
        [Min(0)] public int targetScoreIncreasePerDay = 120;
        [Min(50)] public int maximumTargetScore = 1550;
        [Range(0.1f, 5f)] public float resultDelay = 1.4f;

        [Header("Broadcast Time Limit")]
        [Min(1f)] public float baseBroadcastSeconds = 90f;
        [Min(1f)] public float minimumBroadcastSeconds = 90f;
        [Min(1f)] public float maximumBroadcastSeconds = 120f;
        [Min(0f)] public float secondsPerHealthStatLevel = 15f;

        [Header("Broadcast Retry Rules")]
        [Tooltip("게임오버 한 번마다 방송의 남은 시간에서 차감되는 시간입니다.")]
        [Min(0f)] public float gameOverTimePenaltySeconds = 8f;

        [Header("Broadcast Audience & Economy")]
        public RunnerBroadcastGrowthSettings broadcastGrowthSettings;

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
        public float successMentalChange = -7f;
        public float failureMentalChange = -17f;
        public float mentalPenaltyPerHit = 2f;

        [Header("Pre-Broadcast Safety Floor")]
        [Min(0)] public int minimumSubscribersToStartBroadcast = 0;
        [Range(0f, 100f)] public float minimumMentalToStartBroadcast = 1f;

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
        public string runnerSceneName = "BroadcastRunner";
        public string tileArenaSceneName = "TileArena";

        public bool IsEndless => lengthMode == RunnerCampaignLengthMode.Endless;

        public int TargetScoreForDay(int day)
        {
            long target = firstDayTargetScore + (long)Mathf.Max(0, day - 1) * targetScoreIncreasePerDay;
            return Mathf.Clamp((int)Mathf.Min(target, int.MaxValue), firstDayTargetScore, Mathf.Max(firstDayTargetScore, maximumTargetScore));
        }

        public float BroadcastSecondsForHealth(int healthStat)
        {
            float duration = baseBroadcastSeconds + Mathf.Max(0, healthStat - startingHealthStat) * secondsPerHealthStatLevel;
            return Mathf.Clamp(duration, minimumBroadcastSeconds, maximumBroadcastSeconds);
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
            fixedMaximumDays = Mathf.Max(1, fixedMaximumDays);
            scorePerSubscriber = Mathf.Max(1, scorePerSubscriber);
            maximumTargetScore = Mathf.Max(firstDayTargetScore, maximumTargetScore);
            maximumBroadcastSeconds = Mathf.Max(minimumBroadcastSeconds, maximumBroadcastSeconds);
            baseBroadcastSeconds = Mathf.Clamp(baseBroadcastSeconds, minimumBroadcastSeconds, maximumBroadcastSeconds);
            gameOverTimePenaltySeconds = Mathf.Max(0f, gameOverTimePenaltySeconds);
            maximumEffectiveTalkingLevel = Mathf.Max(1, maximumEffectiveTalkingLevel);
            maximumFailureScalingDay = Mathf.Max(1, maximumFailureScalingDay);
            maximumStoredDayRecords = Mathf.Max(1, maximumStoredDayRecords);
            maximumMental = Mathf.Max(1f, maximumMental);
            maximumGameSkill = Mathf.Clamp(maximumGameSkill, 1, 3);
            maximumTalkingSkill = Mathf.Clamp(maximumTalkingSkill, 1, 3);
            maximumHealthStat = Mathf.Clamp(maximumHealthStat, 1, 3);
            experienceForLevel2 = Mathf.Max(1, experienceForLevel2);
            experienceForLevel3 = Mathf.Max(1, experienceForLevel3);
            saveSlotCount = Mathf.Clamp(saveSlotCount, 1, 8);
            if (dayActions == null) dayActions = new List<RunnerCampaignActionDefinition>();
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
    public sealed class RunnerCampaignSaveData
    {
        public int version = RunnerCampaignSaveStore.CurrentVersion;
        public int slot;
        public string savedAtUtc;
        public int day;
        public int subscribers;
        public float mental;
        public int gameSkill;
        public int gameSkillExperience;
        public int talkingSkill;
        public int talkingSkillExperience;
        public int healthStat;
        public int healthStatExperience;
        public int bestBroadcastScore;
        public long lifetimeDonations;
        public float lastBroadcastRating;
        public int lastPeakViewers;
        public float lastAverageViewers;
        public bool campaignFailed;
        public bool awaitingAdvance;
        public bool broadcastPending;
        public string selectedAction;
        public string selectedBroadcastGame;
        public List<RunnerCampaignDayRecord> records = new List<RunnerCampaignDayRecord>();
    }

}
