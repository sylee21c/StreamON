using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace StreamOn.Minigames.Runner
{
    /// <summary>씬에서 배치한 텍스트/버튼만 갱신하는 성장 패널입니다.</summary>
    public sealed class BroadcasterProgressionPanel : MonoBehaviour
    {
        public RunnerCampaignSettings settings;
        public TMP_Text levelText;
        public TMP_Text experienceText;
        public TMP_Text pointText;
        public TMP_Text witText;
        [FormerlySerializedAs("mentalText")] public TMP_Text composureText;
        [FormerlySerializedAs("staminaText")] public TMP_Text controlText;
        public TMP_Text cashText;
        public TMP_Text managerText;
        public Button witButton;
        [FormerlySerializedAs("mentalButton")] public Button composureButton;
        [FormerlySerializedAs("staminaButton")] public Button controlButton;
        public Button respecButton;

        private RunnerCampaignSaveData _save;

        private void Awake()
        {
            witButton?.onClick.AddListener(UpgradeWit);
            composureButton?.onClick.AddListener(UpgradeComposure);
            controlButton?.onClick.AddListener(UpgradeControl);
            respecButton?.onClick.AddListener(Respec);
        }
        private void OnEnable() => Refresh();
        public void UpgradeWit() { if (BroadcasterProgression.TryUpgrade(settings, _save, BroadcasterStatType.Wit)) Refresh(); }
        public void UpgradeComposure() { if (BroadcasterProgression.TryUpgrade(settings, _save, BroadcasterStatType.Composure)) Refresh(); }
        public void UpgradeControl() { if (BroadcasterProgression.TryUpgrade(settings, _save, BroadcasterStatType.Control)) Refresh(); }
        public void Respec() { if (BroadcasterProgression.TryRespec(settings, _save)) Refresh(); }

        public void Refresh()
        {
            if (settings == null || !RunnerCampaignSaveStore.TryLoad(settings, out _save)) return;
            if (levelText != null) levelText.text = $"방송인 Lv.{_save.broadcasterLevel}/{settings.maximumBroadcasterLevel}";
            if (experienceText != null) experienceText.text = $"EXP {_save.broadcasterExperience}";
            if (pointText != null) pointText.text = $"남은 포인트 {_save.unspentStatPoints}";
            int maxWit = BroadcasterProgression.MaximumRank(settings, BroadcasterStatType.Wit);
            int maxComposure = BroadcasterProgression.MaximumRank(settings, BroadcasterStatType.Composure);
            int maxControl = BroadcasterProgression.MaximumRank(settings, BroadcasterStatType.Control);
            int witCost = _save.witRank < maxWit ? settings.WitRule(_save.witRank + 1)?.upgradeCost ?? _save.witRank + 1 : 0;
            int composureCost = _save.ComposureRank < maxComposure ? settings.ComposureRule(_save.ComposureRank + 1)?.upgradeCost ?? _save.ComposureRank + 1 : 0;
            int controlCost = _save.ControlRank < maxControl ? settings.ControlRule(_save.ControlRank + 1)?.upgradeCost ?? _save.ControlRank + 1 : 0;
            if (witText != null) witText.text = _save.witRank < maxWit ? $"재치 {_save.witRank}/{maxWit} · {witCost}P" : $"재치 {maxWit}/{maxWit} · MAX";
            if (composureText != null) composureText.text = _save.ComposureRank < maxComposure ? $"평정심 {_save.ComposureRank}/{maxComposure} · {composureCost}P" : $"평정심 {maxComposure}/{maxComposure} · MAX";
            if (controlText != null) controlText.text = _save.ControlRank < maxControl ? $"통제력 {_save.ControlRank}/{maxControl} · {controlCost}P" : $"통제력 {maxControl}/{maxControl} · MAX";
            if (cashText != null) cashText.text = $"보유금 {_save.cash:N0}원";
            if (managerText != null) managerText.text = _save.hiredManagerTier > 0 ? $"고용 매니저 Lv.{_save.hiredManagerTier}" : "매니저 미고용";
            if (witButton != null) witButton.interactable = _save.witRank < maxWit && _save.unspentStatPoints >= witCost;
            if (composureButton != null) composureButton.interactable = _save.ComposureRank < maxComposure && _save.unspentStatPoints >= composureCost;
            if (controlButton != null) controlButton.interactable = _save.ControlRank < maxControl && _save.unspentStatPoints >= controlCost;
        }
    }
}
