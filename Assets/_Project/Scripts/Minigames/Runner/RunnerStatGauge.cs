using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StreamOn.Minigames.Runner
{
    /// <summary>Scene-authored stat gauge. Runtime code only updates its normalized fill.</summary>
    public sealed class RunnerStatGauge : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image fill;
        [SerializeField] private string displayName = "스탯";
        [SerializeField, Min(1f)] private float maximumValue = 100f;

        public float MaximumValue => maximumValue;

        public void SetValue(float currentValue, float configuredMaximum = -1f)
        {
            float maximum = configuredMaximum > 0f ? configuredMaximum : maximumValue;
            maximumValue = Mathf.Max(1f, maximum);
            if (label != null) label.text = displayName;
            SetNormalizedFill(currentValue / maximumValue);
        }

        public void SetLevelProgress(int level, int experience, int requiredExperience, int maximumLevel = 3)
        {
            level = Mathf.Clamp(level, 1, Mathf.Max(1, maximumLevel));
            bool isMaximum = level >= maximumLevel;
            if (label != null) label.text = $"{displayName}  Lv.{level}";
            SetNormalizedFill(isMaximum ? 1f : experience / (float)Mathf.Max(1, requiredExperience));
        }

        public void SetNormalizedLevelProgress(int level, float progress, int maximumLevel = 3)
        {
            level = Mathf.Clamp(level, 1, Mathf.Max(1, maximumLevel));
            if (label != null) label.text = $"{displayName}  Lv.{level}";
            SetNormalizedFill(level >= maximumLevel ? 1f : progress);
        }

        private void SetNormalizedFill(float value)
        {
            if (fill == null) return;
            float normalized = Mathf.Clamp01(value);
            fill.fillAmount = normalized;

            // Image.fillAmount is ignored when an Image has no source sprite. The room gauges
            // intentionally use plain Images, so scale the actual rectangle from its left edge too.
            RectTransform rect = fill.rectTransform;
            rect.pivot = new Vector2(0f, 0.5f);
            Vector3 scale = rect.localScale;
            scale.x = normalized;
            rect.localScale = scale;
        }

        private void OnValidate()
        {
            maximumValue = Mathf.Max(1f, maximumValue);
            if (label != null) label.text = displayName;
            if (fill != null)
            {
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = 0;
                SetNormalizedFill(fill.fillAmount);
            }
        }
    }
}
