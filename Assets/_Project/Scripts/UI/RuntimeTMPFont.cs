using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StreamOn.UI.Common
{
    public static class RuntimeTMPFont
    {
        private static TMP_FontAsset _koreanFont;

        public static TMP_FontAsset CreateKoreanFont()
        {
            if (_koreanFont != null) return _koreanFont;

            _koreanFont = TMP_Settings.defaultFontAsset;
            return _koreanFont;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyToSerializedSceneText()
        {
            if (SceneManager.GetActiveScene().name != "BroadcastRunner") return;
            TMP_FontAsset font = CreateKoreanFont();
            foreach (Text legacy in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                ConvertLegacyText(legacy, font);
            foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (text.font != font)
                    text.font = font;
            }
        }

        private static void ConvertLegacyText(Text legacy, TMP_FontAsset font)
        {
            GameObject target = legacy.gameObject;
            TextMeshProUGUI text = target.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = legacy.text;
            text.fontSize = legacy.fontSize;
            text.fontStyle = legacy.fontStyle switch
            {
                FontStyle.Bold => FontStyles.Bold,
                FontStyle.Italic => FontStyles.Italic,
                FontStyle.BoldAndItalic => FontStyles.Bold | FontStyles.Italic,
                _ => FontStyles.Normal
            };
            text.alignment = legacy.alignment switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.MidlineLeft,
                TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
                TextAnchor.MiddleRight => TextAlignmentOptions.MidlineRight,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.Center
            };
            text.color = legacy.color;
            text.richText = legacy.supportRichText;
            text.raycastTarget = legacy.raycastTarget;
            text.textWrappingMode = legacy.horizontalOverflow == HorizontalWrapMode.Wrap
                ? TextWrappingModes.Normal
                : TextWrappingModes.NoWrap;
            text.overflowMode = legacy.verticalOverflow == VerticalWrapMode.Truncate
                ? TextOverflowModes.Truncate
                : TextOverflowModes.Overflow;
            Object.Destroy(legacy);
        }
    }
}
