using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StreamOn.Minigames.Runner
{
    // Progression logic consumes only Inspector-authored rank and level tables.
    public enum BroadcasterStatType { Wit, Mental, Stamina }

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
            if (rank >= 5 || cost <= 0 || save.unspentStatPoints < cost) return false;
            save.unspentStatPoints -= cost;
            SetRank(save, type, rank + 1);
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
            save.unspentStatPoints += SpentPoints(settings, save);
            save.witRank = save.mentalRank = save.staminaRank = 0;
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
                BroadcasterStatType.Mental => settings.MentalRule(targetRank)?.upgradeCost ?? targetRank,
                _ => settings.StaminaRule(targetRank)?.upgradeCost ?? targetRank
            };
        }

        private static int SpentPoints(RunnerCampaignSettings settings, RunnerCampaignSaveData save)
        {
            int total = 0;
            for (int rank = 1; rank <= save.witRank; rank++) total += UpgradeCost(settings, BroadcasterStatType.Wit, rank);
            for (int rank = 1; rank <= save.mentalRank; rank++) total += UpgradeCost(settings, BroadcasterStatType.Mental, rank);
            for (int rank = 1; rank <= save.staminaRank; rank++) total += UpgradeCost(settings, BroadcasterStatType.Stamina, rank);
            return total;
        }

        private static int Rank(RunnerCampaignSaveData save, BroadcasterStatType type) => type switch
        {
            BroadcasterStatType.Wit => save.witRank,
            BroadcasterStatType.Mental => save.mentalRank,
            _ => save.staminaRank
        };

        private static void SetRank(RunnerCampaignSaveData save, BroadcasterStatType type, int rank)
        {
            if (type == BroadcasterStatType.Wit) save.witRank = rank;
            else if (type == BroadcasterStatType.Mental) save.mentalRank = rank;
            else save.staminaRank = rank;
        }
    }

    /// <summary>씬에서 배치한 텍스트/버튼만 갱신하는 성장 패널입니다.</summary>
    public sealed class BroadcasterProgressionPanel : MonoBehaviour
    {
        public RunnerCampaignSettings settings;
        public TMP_Text levelText;
        public TMP_Text experienceText;
        public TMP_Text pointText;
        public TMP_Text witText;
        public TMP_Text mentalText;
        public TMP_Text staminaText;
        public TMP_Text cashText;
        public TMP_Text managerText;
        public Button witButton;
        public Button mentalButton;
        public Button staminaButton;
        public Button respecButton;

        private RunnerCampaignSaveData _save;

        private void Awake()
        {
            witButton?.onClick.AddListener(UpgradeWit);
            mentalButton?.onClick.AddListener(UpgradeMental);
            staminaButton?.onClick.AddListener(UpgradeStamina);
            respecButton?.onClick.AddListener(Respec);
        }
        private void OnEnable() => Refresh();
        public void UpgradeWit() { if (BroadcasterProgression.TryUpgrade(settings, _save, BroadcasterStatType.Wit)) Refresh(); }
        public void UpgradeMental() { if (BroadcasterProgression.TryUpgrade(settings, _save, BroadcasterStatType.Mental)) Refresh(); }
        public void UpgradeStamina() { if (BroadcasterProgression.TryUpgrade(settings, _save, BroadcasterStatType.Stamina)) Refresh(); }
        public void Respec() { if (BroadcasterProgression.TryRespec(settings, _save)) Refresh(); }

        public void Refresh()
        {
            if (settings == null || !RunnerCampaignSaveStore.TryLoad(settings, out _save)) return;
            if (levelText != null) levelText.text = $"방송인 Lv.{_save.broadcasterLevel}/{settings.maximumBroadcasterLevel}";
            if (experienceText != null) experienceText.text = $"EXP {_save.broadcasterExperience}";
            if (pointText != null) pointText.text = $"남은 포인트 {_save.unspentStatPoints}";
            int witCost = _save.witRank < 5 ? settings.WitRule(_save.witRank + 1)?.upgradeCost ?? _save.witRank + 1 : 0;
            int mentalCost = _save.mentalRank < 5 ? settings.MentalRule(_save.mentalRank + 1)?.upgradeCost ?? _save.mentalRank + 1 : 0;
            int staminaCost = _save.staminaRank < 5 ? settings.StaminaRule(_save.staminaRank + 1)?.upgradeCost ?? _save.staminaRank + 1 : 0;
            if (witText != null) witText.text = _save.witRank < 5 ? $"재치 {_save.witRank}/5 · {witCost}P" : "재치 5/5 · MAX";
            if (mentalText != null) mentalText.text = _save.mentalRank < 5 ? $"멘탈 {_save.mentalRank}/5 · {mentalCost}P" : "멘탈 5/5 · MAX";
            if (staminaText != null) staminaText.text = _save.staminaRank < 5 ? $"체력 {_save.staminaRank}/5 · {staminaCost}P" : "체력 5/5 · MAX";
            if (cashText != null) cashText.text = $"보유금 {_save.cash:N0}원";
            if (managerText != null) managerText.text = _save.hiredManagerTier > 0 ? $"고용 매니저 Lv.{_save.hiredManagerTier}" : "매니저 미고용";
            if (witButton != null) witButton.interactable = _save.witRank < 5 && _save.unspentStatPoints >= witCost;
            if (mentalButton != null) mentalButton.interactable = _save.mentalRank < 5 && _save.unspentStatPoints >= mentalCost;
            if (staminaButton != null) staminaButton.interactable = _save.staminaRank < 5 && _save.unspentStatPoints >= staminaCost;
        }
    }

    public sealed class ManagerHiringPanel : MonoBehaviour
    {
        public RunnerCampaignSettings settings;
        public TMP_Text titleText;
        public TMP_Text cashText;
        public TMP_Text[] tierDescriptions;
        public Button[] unlockButtons;
        public Button[] hireButtons;
        private RunnerCampaignSaveData _save;

        private void Awake()
        {
            for (int index = 0; unlockButtons != null && index < unlockButtons.Length; index++)
            {
                int tier = index + 1;
                unlockButtons[index]?.onClick.AddListener(() => Unlock(tier));
            }
            for (int index = 0; hireButtons != null && index < hireButtons.Length; index++)
            {
                int tier = index + 1;
                hireButtons[index]?.onClick.AddListener(() => Hire(tier));
            }
        }
        private void OnEnable() => Refresh();
        public void UnlockTier1() => Unlock(1);
        public void UnlockTier2() => Unlock(2);
        public void UnlockTier3() => Unlock(3);
        public void HireTier1() => Hire(1);
        public void HireTier2() => Hire(2);
        public void HireTier3() => Hire(3);

        private void Unlock(int tier)
        {
            if (BroadcasterProgression.TryUnlockManager(settings, _save, tier)) Refresh();
        }

        private void Hire(int tier)
        {
            if (BroadcasterProgression.TryHireManager(settings, _save, tier)) Refresh();
        }

        public void Refresh()
        {
            if (settings == null || !RunnerCampaignSaveStore.TryLoad(settings, out _save)) return;
            if (titleText != null) titleText.text = _save.hiredManagerTier > 0 ? $"이번 방송 매니저 Lv.{_save.hiredManagerTier}" : "이번 방송 매니저 선택";
            if (cashText != null) cashText.text = $"보유금 {_save.cash:N0}원";
            for (int index = 0; index < 3; index++)
            {
                int tier = index + 1;
                ManagerTierRule rule = settings.managerTiers?.Find(candidate => candidate != null && candidate.tier == tier);
                if (rule == null) continue;
                if (tierDescriptions != null && index < tierDescriptions.Length && tierDescriptions[index] != null)
                    tierDescriptions[index].text = $"{rule.displayName}\n방송당 {rule.usesPerBroadcast}회 · {rule.handlingDelaySeconds:0.#}초 후 처리\n해금 {rule.unlockCost:N0}원 · 고용 {rule.hireCostPerBroadcast:N0}원";
                if (unlockButtons != null && index < unlockButtons.Length && unlockButtons[index] != null)
                    unlockButtons[index].interactable = tier == _save.unlockedManagerTier + 1 && _save.cash >= rule.unlockCost;
                if (hireButtons != null && index < hireButtons.Length && hireButtons[index] != null)
                    hireButtons[index].interactable = tier <= _save.unlockedManagerTier && _save.cash >= rule.hireCostPerBroadcast;
            }
        }
    }
}
