using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StreamOn.Minigames.Runner
{
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
