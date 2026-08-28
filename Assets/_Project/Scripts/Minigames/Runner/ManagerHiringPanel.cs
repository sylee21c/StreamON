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
                {
                    bool selected = tier == _save.hiredManagerTier;
                    string name = selected ? $"<color=#FFF23D>{rule.displayName}</color>" : rule.displayName;
                    tierDescriptions[index].richText = true;
                    tierDescriptions[index].text =
                        $"<size=18><b>{name}</b></size>\n" +
                        $"<size=12>분탕 {rule.conflictResolveChance * 100f:0}% / 친목 {rule.fraternizationResolveChance * 100f:0}%</size>\n" +
                        $"<size=12>해금 {rule.unlockCost:N0}원 / 일급 {rule.hireCostPerBroadcast:N0}원</size>";
                }
                if (unlockButtons != null && index < unlockButtons.Length && unlockButtons[index] != null)
                    SetButtonState(unlockButtons[index], tier == _save.unlockedManagerTier + 1 && _save.cash >= rule.unlockCost);
                if (hireButtons != null && index < hireButtons.Length && hireButtons[index] != null)
                    SetButtonState(hireButtons[index], tier <= _save.unlockedManagerTier && tier != _save.hiredManagerTier);
            }
        }

        private static void SetButtonState(Button button, bool interactable)
        {
            button.interactable = interactable;
            ColorBlock colors = button.colors;
            colors.disabledColor = new Color(0.16f, 0.17f, 0.20f, 1f);
            button.colors = colors;
        }
    }
}
