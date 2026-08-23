using System;
using System.Collections.Generic;
using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    // Progression logic consumes only Inspector-authored rank and level tables.
    public enum BroadcasterStatType { Wit, Composure, Control }

    public static class BroadcasterProgression
    {
        public static int AddExperience(RunnerCampaignSettings settings, RunnerCampaignSaveData save, int amount)
        {
            if (settings == null || save == null || amount <= 0) return 0;
            int oldLevel = save.broadcasterLevel;
            save.broadcasterExperience += amount;
            while (save.broadcasterLevel < settings.maximumBroadcasterLevel)
            {
                BroadcasterLevelRule rule = LevelRule(settings, save.broadcasterLevel);
                int required = Mathf.Max(1, rule != null ? rule.experienceToNextLevel : 100);
                if (save.broadcasterExperience < required) break;
                save.broadcasterExperience -= required;
                save.broadcasterLevel++;
                BroadcasterLevelRule reached = LevelRule(settings, save.broadcasterLevel);
                save.unspentStatPoints += Mathf.Max(0, reached != null ? reached.statPointsGranted : 1);
            }
            if (save.broadcasterLevel >= settings.maximumBroadcasterLevel) save.broadcasterExperience = 0;
            return save.broadcasterLevel - oldLevel;
        }

        public static int AddBroadcastExperience(RunnerCampaignSettings settings, RunnerCampaignSaveData save, int amount)
        {
            if (settings == null || save == null || amount <= 0) return 0;
            int remaining = Mathf.Max(0, settings.maximumExperiencePerBroadcast - save.broadcastSessionExperienceEarned);
            int granted = Mathf.Min(amount, remaining);
            if (granted <= 0) return 0;
            save.broadcastSessionExperienceEarned += granted;
            AddExperience(settings, save, granted);
            return granted;
        }

        public static bool TryUpgrade(RunnerCampaignSettings settings, RunnerCampaignSaveData save, BroadcasterStatType type)
        {
            if (settings == null || save == null) return false;
            int rank = Rank(save, type);
            int cost = UpgradeCost(settings, type, rank + 1);
            if (rank >= MaximumRank(settings, type) || cost <= 0 || save.unspentStatPoints <= 0) return false;

            save.unspentStatPoints--;
            int invested = InvestedPoints(save, type) + 1;
            if (invested >= cost)
            {
                SetRank(save, type, rank + 1);
                invested = 0;
            }
            SetInvestedPoints(save, type, invested);
            RunnerCampaignSaveStore.Save(settings, save, true);
            return true;
        }

        public static bool TryRespec(RunnerCampaignSettings settings, RunnerCampaignSaveData save)
        {
            if (settings == null || save == null) return false;
            long cost = settings.firstRespecIsFree && !save.freeRespecUsed ? 0L : Math.Max(0L, settings.respecCashCost);
            if (save.cash < cost) return false;
            save.cash -= cost;
            save.freeRespecUsed = true;
            save.unspentStatPoints += SpentPoints(settings, save)
                + save.witInvestedPoints + save.composureInvestedPoints + save.controlInvestedPoints;
            save.witRank = 0;
            save.ComposureRank = 0;
            save.ControlRank = 0;
            save.witInvestedPoints = 0;
            save.composureInvestedPoints = 0;
            save.controlInvestedPoints = 0;
            RunnerCampaignSaveStore.Save(settings, save, true);
            return true;
        }

        public static bool TryUnlockManager(RunnerCampaignSettings settings, RunnerCampaignSaveData save, int tier)
        {
            ManagerTierRule rule = settings?.managerTiers?.Find(candidate => candidate != null && candidate.tier == tier);
            if (rule == null || save == null || tier != save.unlockedManagerTier + 1 || save.cash < rule.unlockCost) return false;
            save.cash -= rule.unlockCost;
            save.unlockedManagerTier = tier;
            RunnerCampaignSaveStore.Save(settings, save, true);
            return true;
        }

        public static bool TryHireManager(RunnerCampaignSettings settings, RunnerCampaignSaveData save, int tier)
        {
            ManagerTierRule rule = settings?.managerTiers?.Find(candidate => candidate != null && candidate.tier == tier);
            if (rule == null || save == null || tier > save.unlockedManagerTier || save.cash < rule.hireCostPerBroadcast) return false;
            save.cash -= rule.hireCostPerBroadcast;
            save.hiredManagerTier = tier;
            save.managerUsesRemaining = Mathf.Max(0, rule.usesPerBroadcast);
            RunnerCampaignSaveStore.Save(settings, save, true);
            return true;
        }

        public static ManagerTierRule HiredManager(RunnerCampaignSettings settings, RunnerCampaignSaveData save) =>
            settings?.managerTiers?.Find(candidate => candidate != null && save != null && candidate.tier == save.hiredManagerTier);

        private static BroadcasterLevelRule LevelRule(RunnerCampaignSettings settings, int level) =>
            settings.broadcasterLevels?.Find(candidate => candidate != null && candidate.level == level);

        private static int UpgradeCost(RunnerCampaignSettings settings, BroadcasterStatType type, int targetRank)
        {
            return type switch
            {
                BroadcasterStatType.Wit => settings.WitRule(targetRank)?.upgradeCost ?? targetRank,
                BroadcasterStatType.Composure => settings.ComposureRule(targetRank)?.upgradeCost ?? targetRank,
                _ => settings.ControlRule(targetRank)?.upgradeCost ?? targetRank
            };
        }

        public static int MaximumRank(RunnerCampaignSettings settings, BroadcasterStatType type)
        {
            if (settings == null) return 0;
            return type switch
            {
                BroadcasterStatType.Wit => Mathf.Max(0, (settings.witRanks?.Count ?? 1) - 1),
                BroadcasterStatType.Composure => Mathf.Max(0, (settings.composureRanks?.Count ?? 1) - 1),
                _ => Mathf.Max(0, (settings.controlRanks?.Count ?? 1) - 1)
            };
        }

        public static int NextUpgradeCost(RunnerCampaignSettings settings, RunnerCampaignSaveData save,
            BroadcasterStatType type)
        {
            if (settings == null || save == null || Rank(save, type) >= MaximumRank(settings, type)) return 0;
            return Mathf.Max(1, UpgradeCost(settings, type, Rank(save, type) + 1));
        }

        public static int InvestedPoints(RunnerCampaignSaveData save, BroadcasterStatType type)
        {
            if (save == null) return 0;
            return type switch
            {
                BroadcasterStatType.Wit => save.witInvestedPoints,
                BroadcasterStatType.Composure => save.composureInvestedPoints,
                _ => save.controlInvestedPoints
            };
        }

        public static int Rank(RunnerCampaignSaveData save, BroadcasterStatType type) => type switch
        {
            BroadcasterStatType.Wit => save.witRank,
            BroadcasterStatType.Composure => save.ComposureRank,
            _ => save.ControlRank
        };

        private static int SpentPoints(RunnerCampaignSettings settings, RunnerCampaignSaveData save)
        {
            int total = 0;
            for (int rank = 1; rank <= save.witRank; rank++) total += UpgradeCost(settings, BroadcasterStatType.Wit, rank);
            for (int rank = 1; rank <= save.ComposureRank; rank++) total += UpgradeCost(settings, BroadcasterStatType.Composure, rank);
            for (int rank = 1; rank <= save.ControlRank; rank++) total += UpgradeCost(settings, BroadcasterStatType.Control, rank);
            return total;
        }

        private static void SetRank(RunnerCampaignSaveData save, BroadcasterStatType type, int rank)
        {
            if (type == BroadcasterStatType.Wit) save.witRank = rank;
            else if (type == BroadcasterStatType.Composure) save.ComposureRank = rank;
            else save.ControlRank = rank;
        }

        private static void SetInvestedPoints(RunnerCampaignSaveData save, BroadcasterStatType type, int value)
        {
            value = Mathf.Max(0, value);
            if (type == BroadcasterStatType.Wit) save.witInvestedPoints = value;
            else if (type == BroadcasterStatType.Composure) save.composureInvestedPoints = value;
            else save.controlInvestedPoints = value;
        }
    }
}
