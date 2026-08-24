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

        [Header("Scene-authored stat progress")]
        public TMP_Text witLevelText;
        public TMP_Text composureLevelText;
        public TMP_Text controlLevelText;
        public Image witProgressFill;
        public Image composureProgressFill;
        public Image controlProgressFill;
        public TMP_Text respecButtonLabel;
        [SerializeField, Min(0.1f)] private float gaugeAnimationSpeed = 5f;

        private RunnerCampaignSaveData _save;
        private float _witDisplayed;
        private float _composureDisplayed;
        private float _controlDisplayed;
        private float _witTarget;
        private float _composureTarget;
        private float _controlTarget;
        private float _witResetTarget;
        private float _composureResetTarget;
        private float _controlResetTarget;
        private bool _witCompleting;
        private bool _composureCompleting;
        private bool _controlCompleting;

        private void Awake()
        {
            witButton?.onClick.AddListener(UpgradeWit);
            composureButton?.onClick.AddListener(UpgradeComposure);
            controlButton?.onClick.AddListener(UpgradeControl);
            respecButton?.onClick.AddListener(Respec);
        }

        private void OnEnable() => Refresh(true);

        private void Update()
        {
            AnimateGauge(ref _witDisplayed, ref _witTarget, ref _witCompleting, _witResetTarget, witProgressFill);
            AnimateGauge(ref _composureDisplayed, ref _composureTarget, ref _composureCompleting,
                _composureResetTarget, composureProgressFill);
            AnimateGauge(ref _controlDisplayed, ref _controlTarget, ref _controlCompleting,
                _controlResetTarget, controlProgressFill);
        }

        public void UpgradeWit() => InvestPoint(BroadcasterStatType.Wit);
        public void UpgradeComposure() => InvestPoint(BroadcasterStatType.Composure);
        public void UpgradeControl() => InvestPoint(BroadcasterStatType.Control);
        public void Respec() { if (BroadcasterProgression.TryRespec(settings, _save)) Refresh(false); }
        public void Refresh() => Refresh(false);

        private void Refresh(bool immediate)
        {
            if (settings == null || !RunnerCampaignSaveStore.TryLoad(settings, out _save)) return;
            if (levelText != null) levelText.text = $"방송인 Lv.{_save.broadcasterLevel}/{settings.maximumBroadcasterLevel}";
            if (experienceText != null) experienceText.text = $"EXP {_save.broadcasterExperience}";
            if (pointText != null) pointText.text = $"남은 포인트 {_save.unspentStatPoints}";

            int maxWit = BroadcasterProgression.MaximumRank(settings, BroadcasterStatType.Wit);
            int maxComposure = BroadcasterProgression.MaximumRank(settings, BroadcasterStatType.Composure);
            int maxControl = BroadcasterProgression.MaximumRank(settings, BroadcasterStatType.Control);
            int witCost = BroadcasterProgression.NextUpgradeCost(settings, _save, BroadcasterStatType.Wit);
            int composureCost = BroadcasterProgression.NextUpgradeCost(settings, _save, BroadcasterStatType.Composure);
            int controlCost = BroadcasterProgression.NextUpgradeCost(settings, _save, BroadcasterStatType.Control);
            int witInvested = BroadcasterProgression.InvestedPoints(_save, BroadcasterStatType.Wit);
            int composureInvested = BroadcasterProgression.InvestedPoints(_save, BroadcasterStatType.Composure);
            int controlInvested = BroadcasterProgression.InvestedPoints(_save, BroadcasterStatType.Control);

            if (witText != null) witText.text = _save.witRank < maxWit
                ? $"재치 {_save.witRank}/{maxWit} / {witInvested}/{witCost}P" : $"재치 {maxWit}/{maxWit} / MAX";
            if (composureText != null) composureText.text = _save.ComposureRank < maxComposure
                ? $"평정심 {_save.ComposureRank}/{maxComposure} / {composureInvested}/{composureCost}P" : $"평정심 {maxComposure}/{maxComposure} / MAX";
            if (controlText != null) controlText.text = _save.ControlRank < maxControl
                ? $"통제력 {_save.ControlRank}/{maxControl} / {controlInvested}/{controlCost}P" : $"통제력 {maxControl}/{maxControl} / MAX";
            if (cashText != null) cashText.text = $"보유금 {_save.cash:N0}원";
            if (managerText != null) managerText.text = _save.hiredManagerTier > 0
                ? $"고용 매니저 Lv.{_save.hiredManagerTier}" : "매니저 미고용";

            if (witButton != null) witButton.interactable = _save.witRank < maxWit && _save.unspentStatPoints > 0;
            if (composureButton != null) composureButton.interactable = _save.ComposureRank < maxComposure && _save.unspentStatPoints > 0;
            if (controlButton != null) controlButton.interactable = _save.ControlRank < maxControl && _save.unspentStatPoints > 0;

            SetLevelText(witLevelText, _save.witRank, maxWit);
            SetLevelText(composureLevelText, _save.ComposureRank, maxComposure);
            SetLevelText(controlLevelText, _save.ControlRank, maxControl);
            _witTarget = StatProgress(BroadcasterStatType.Wit);
            _composureTarget = StatProgress(BroadcasterStatType.Composure);
            _controlTarget = StatProgress(BroadcasterStatType.Control);
            if (immediate)
            {
                _witDisplayed = _witTarget;
                _composureDisplayed = _composureTarget;
                _controlDisplayed = _controlTarget;
                ApplyFill(witProgressFill, _witDisplayed);
                ApplyFill(composureProgressFill, _composureDisplayed);
                ApplyFill(controlProgressFill, _controlDisplayed);
            }

            long respecCost = settings.firstRespecIsFree && !_save.freeRespecUsed
                ? 0L : System.Math.Max(0L, settings.respecCashCost);
            if (respecButtonLabel != null) respecButtonLabel.text = respecCost <= 0L
                ? "스탯 초기화 (무료)"
                : $"스탯 초기화 ({respecCost:N0} 원)";
            if (respecButton != null)
            {
                bool hasInvestment = _save.witRank > 0 || _save.ComposureRank > 0 || _save.ControlRank > 0
                    || _save.witInvestedPoints > 0 || _save.composureInvestedPoints > 0 || _save.controlInvestedPoints > 0;
                respecButton.interactable = hasInvestment && _save.cash >= respecCost;
            }
        }

        private void InvestPoint(BroadcasterStatType type)
        {
            if (_save == null) return;
            int oldRank = BroadcasterProgression.Rank(_save, type);
            if (!BroadcasterProgression.TryUpgrade(settings, _save, type)) return;
            int newRank = BroadcasterProgression.Rank(_save, type);
            Refresh(false);
            if (newRank <= oldRank) return;

            float resetTarget = StatProgress(type);
            bool reachedMaximum = newRank >= BroadcasterProgression.MaximumRank(settings, type);
            if (type == BroadcasterStatType.Wit)
            {
                _witTarget = 1f;
                _witResetTarget = resetTarget;
                _witCompleting = !reachedMaximum;
            }
            else if (type == BroadcasterStatType.Composure)
            {
                _composureTarget = 1f;
                _composureResetTarget = resetTarget;
                _composureCompleting = !reachedMaximum;
            }
            else
            {
                _controlTarget = 1f;
                _controlResetTarget = resetTarget;
                _controlCompleting = !reachedMaximum;
            }
        }

        private float StatProgress(BroadcasterStatType type)
        {
            return BroadcasterProgression.UpgradeProgress(settings, _save, type);
        }

        private void AnimateGauge(ref float displayed, ref float target, ref bool completing,
            float resetTarget, Image fill)
        {
            displayed = Mathf.MoveTowards(displayed, target, gaugeAnimationSpeed * Time.unscaledDeltaTime);
            ApplyFill(fill, displayed);
            if (!completing || displayed < 0.999f) return;
            completing = false;
            displayed = 0f;
            target = resetTarget;
            ApplyFill(fill, displayed);
        }

        private static void ApplyFill(Image fill, float progress)
        {
            if (fill == null) return;
            float normalized = Mathf.Clamp01(progress);
            fill.fillAmount = normalized;
            RectTransform rect = fill.rectTransform;
            rect.anchorMax = new Vector2(normalized, rect.anchorMax.y);
        }

        private static void SetLevelText(TMP_Text label, int rank, int maximum)
        {
            if (label != null) label.text = rank >= maximum ? "Lvl. MAX" : $"Lvl. {rank + 1}";
        }
    }
}
