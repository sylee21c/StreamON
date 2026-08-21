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
            if (fill != null) fill.fillAmount = Mathf.Clamp01(currentValue / maximumValue);
        }

        public void SetLevelProgress(int level, int experience, int requiredExperience, int maximumLevel = 3)
        {
            level = Mathf.Clamp(level, 1, Mathf.Max(1, maximumLevel));
            bool isMaximum = level >= maximumLevel;
            if (label != null) label.text = $"{displayName}  Lv.{level}";
            if (fill != null) fill.fillAmount = isMaximum
                ? 1f
                : Mathf.Clamp01(experience / (float)Mathf.Max(1, requiredExperience));
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
            }
        }
    }
}
