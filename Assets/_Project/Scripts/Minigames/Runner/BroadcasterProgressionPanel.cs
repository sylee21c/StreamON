using System.Linq;
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
        [Header("Scene-authored stat tooltip")]
        [SerializeField] private TMP_Text statTooltipText;
        [SerializeField] private Image statTooltipBackground;
        [SerializeField, Min(0.1f)] private float gaugeAnimationSpeed = 5f;

        private TMP_Text _witHoverTarget;
        private TMP_Text _composureHoverTarget;
        private TMP_Text _controlHoverTarget;

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
            Canvas dashboardCanvas = GetComponentInParent<Canvas>();
            Transform dashboardRoot = dashboardCanvas != null ? dashboardCanvas.transform : transform.root;
            TMP_Text[] labels = dashboardRoot.GetComponentsInChildren<TMP_Text>(true);
            _witHoverTarget = labels.FirstOrDefault(label => label.name == "Wit Text");
            _composureHoverTarget = labels.FirstOrDefault(label => label.name == "Composure Text");
            _controlHoverTarget = labels.FirstOrDefault(label => label.name == "Control Text");
            SetDashboardStatLabel(_witHoverTarget, "재치");
            SetDashboardStatLabel(_composureHoverTarget, "평정심");
            SetDashboardStatLabel(_controlHoverTarget, "통제력");
            ConfigureTooltip();
        }

        private void OnEnable() => Refresh(true);

        private void Update()
        {
            AnimateGauge(ref _witDisplayed, ref _witTarget, ref _witCompleting, _witResetTarget, witProgressFill);
            AnimateGauge(ref _composureDisplayed, ref _composureTarget, ref _composureCompleting,
                _composureResetTarget, composureProgressFill);
            AnimateGauge(ref _controlDisplayed, ref _controlTarget, ref _controlCompleting,
                _controlResetTarget, controlProgressFill);
            UpdateStatTooltip();
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

        private void ConfigureTooltip()
        {
            if (statTooltipText == null || statTooltipBackground == null) return;
            Canvas dashboardCanvas = GetComponentInParent<Canvas>();
            statTooltipBackground.transform.SetParent(
                dashboardCanvas != null ? dashboardCanvas.transform : transform.root, false);
            statTooltipBackground.transform.SetAsLastSibling();
            statTooltipText.transform.SetParent(statTooltipBackground.transform, false);
            statTooltipText.fontSize = 15f;
            statTooltipText.alignment = TextAlignmentOptions.MidlineLeft;
            statTooltipText.raycastTarget = false;
            RectTransform rect = statTooltipText.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(14f, 8f);
            rect.offsetMax = new Vector2(-14f, -8f);
            RectTransform backgroundRect = statTooltipBackground.rectTransform;
            backgroundRect.anchorMin = backgroundRect.anchorMax = Vector2.zero;
            backgroundRect.pivot = Vector2.zero;
            backgroundRect.sizeDelta = new Vector2(390f, 76f);
            statTooltipBackground.color = new Color(0.015f, 0.02f, 0.03f, .94f);
            statTooltipBackground.raycastTarget = false;
            statTooltipBackground.gameObject.SetActive(false);
        }

        private static void SetDashboardStatLabel(TMP_Text label, string title)
        {
            if (label == null) return;
            label.text = $"<size=21><b>{title}</b></size>";
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.lineSpacing = 0f;
        }

        private void UpdateStatTooltip()
        {
            if (statTooltipText == null || statTooltipBackground == null
                || UnityEngine.InputSystem.Mouse.current == null) return;
            Vector2 pointer = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            string description = IsPointerOverStat(_witHoverTarget, pointer)
                ? "재치\n돌발 상황에 재치 있게 대응해 방송 반응과 보상을 높입니다."
                : IsPointerOverStat(_composureHoverTarget, pointer)
                    ? "평정심\n실수로 받는 불이익을 줄이고 무너진 방송 흐름을 회복합니다."
                    : IsPointerOverStat(_controlHoverTarget, pointer)
                        ? "통제력\n집중력 최대치와 회복력을 높이고 집중력 소모를 줄입니다."
                        : null;
            bool visible = !string.IsNullOrEmpty(description);
            statTooltipBackground.gameObject.SetActive(visible);
            if (!visible) return;
            statTooltipText.text = description;
            statTooltipBackground.rectTransform.position = pointer + new Vector2(18f, 18f);
        }

        private static bool IsPointerOverStat(TMP_Text label, Vector2 pointer)
        {
            if (label == null || !label.gameObject.activeInHierarchy) return false;
            Camera eventCamera = label.canvas != null && label.canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? label.canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    label.rectTransform, pointer, eventCamera, out Vector2 local)) return false;
            Rect area = label.rectTransform.rect;
            area.xMin -= 12f;
            area.xMax += 410f;
            area.yMin -= 8f;
            area.yMax += 8f;
            return area.Contains(local);
        }
    }
}
