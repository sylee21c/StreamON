using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StreamOn.Minigames.Runner
{
    public sealed class RunnerEquipmentShopController : MonoBehaviour
    {
        [SerializeField] private RunnerCampaignSettings settings;
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text cashText;
        [SerializeField] private Button pcButton;
        [SerializeField] private Button microphoneButton;
        [SerializeField] private Button fitnessButton;
        [SerializeField] private Button interiorButton;
        [SerializeField] private TMP_Text pcText;
        [SerializeField] private TMP_Text microphoneText;
        [SerializeField] private TMP_Text fitnessText;
        [SerializeField] private TMP_Text interiorText;
        [SerializeField] private TMP_Text noticeText;

        private RunnerCampaignSaveData _save;

        private void Start()
        {
            openButton?.onClick.AddListener(Open);
            closeButton?.onClick.AddListener(Close);
            pcButton?.onClick.AddListener(() => Purchase(RunnerEquipmentType.Pc));
            microphoneButton?.onClick.AddListener(() => Purchase(RunnerEquipmentType.Microphone));
            fitnessButton?.onClick.AddListener(() => Purchase(RunnerEquipmentType.Fitness));
            interiorButton?.onClick.AddListener(() => Purchase(RunnerEquipmentType.Interior));
            if (shopPanel != null) shopPanel.SetActive(false);
            Reload();
        }

        private void Open() { Reload(); if (shopPanel != null) shopPanel.SetActive(true); }
        private void Close() { if (shopPanel != null) shopPanel.SetActive(false); }

        private void Reload()
        {
            if (settings != null) RunnerCampaignSaveStore.TryLoad(settings, out _save);
            Refresh();
        }

        private void Purchase(RunnerEquipmentType type)
        {
            if (_save == null || settings == null) return;
            int level = GetLevel(type);
            if (level >= 3) { SetNotice("이미 최고 레벨입니다."); return; }
            int target = level + 1;
            int cost = settings.UpgradeCost(type, target);
            if (_save.cash < cost) { SetNotice($"보유금이 {cost - _save.cash:N0}원 부족합니다."); return; }
            _save.cash -= cost;
            SetLevel(type, target);
            RunnerCampaignSaveStore.Save(settings, _save, true);
            SetNotice($"{DisplayName(type)} Lv.{target} 업그레이드 완료!");
            Refresh();
            FindFirstObjectByType<RunnerRoomController>()?.RefreshStatus();
        }

        private void Refresh()
        {
            if (_save == null) return;
            if (cashText != null) cashText.text = $"보유금  {_save.cash:N0}원";
            SetEquipmentText(pcText, RunnerEquipmentType.Pc, _save.pcLevel, $"매니저 처리속도 +{settings.managerDelayReductionPerPcUpgrade * 100f:0}%/Lv");
            SetEquipmentText(microphoneText, RunnerEquipmentType.Microphone, _save.microphoneLevel, "팔로워/후원 보너스");
            SetEquipmentText(fitnessText, RunnerEquipmentType.Fitness, _save.fitnessLevel, $"집중력 +{settings.focusCapacityPerFitnessUpgrade:0}/Lv");
            SetEquipmentText(interiorText, RunnerEquipmentType.Interior, _save.interiorLevel, $"초기 시청자 +{settings.startingViewersPerInteriorUpgrade:0}/Lv");
            if (pcButton != null) pcButton.interactable = _save.pcLevel < 3;
            if (microphoneButton != null) microphoneButton.interactable = _save.microphoneLevel < 3;
            if (fitnessButton != null) fitnessButton.interactable = _save.fitnessLevel < 3;
            if (interiorButton != null) interiorButton.interactable = _save.interiorLevel < 3;
        }

        private void SetEquipmentText(TMP_Text label, RunnerEquipmentType type, int level, string effect)
        {
            if (label == null) return;
            string price = level >= 3 ? "MAX" : $"다음 {settings.UpgradeCost(type, level + 1):N0}원";
            label.text = $"{DisplayName(type)}  Lv.{level}\n{effect}    {price}";
        }

        private int GetLevel(RunnerEquipmentType type) => type switch
        {
            RunnerEquipmentType.Pc => _save.pcLevel,
            RunnerEquipmentType.Microphone => _save.microphoneLevel,
            RunnerEquipmentType.Fitness => _save.fitnessLevel,
            _ => _save.interiorLevel
        };

        private void SetLevel(RunnerEquipmentType type, int value)
        {
            switch (type)
            {
                case RunnerEquipmentType.Pc: _save.pcLevel = value; break;
                case RunnerEquipmentType.Microphone: _save.microphoneLevel = value; break;
                case RunnerEquipmentType.Fitness: _save.fitnessLevel = value; break;
                default: _save.interiorLevel = value; break;
            }
        }

        private static string DisplayName(RunnerEquipmentType type) => type switch
        {
            RunnerEquipmentType.Pc => "PC",
            RunnerEquipmentType.Microphone => "마이크",
            RunnerEquipmentType.Fitness => "집중 장비",
            _ => "방 인테리어"
        };

        private void SetNotice(string value) { if (noticeText != null) noticeText.text = value; }
    }
}
