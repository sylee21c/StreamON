#if UNITY_EDITOR
using System.Linq;
using StreamOn.Minigames.Runner;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StreamOn.EditorTools
{
    [InitializeOnLoad]
    public static class RunnerTMPMigration
    {
        private const string ScenePath = "Assets/Scenes/BroadcastRunner.unity";
        private const string FontPath = "Assets/TextMesh Pro/Examples & Extras/Fonts/Galmuri14 SDF.asset";
        private const string ChatFontPath = "Assets/_Project/Fonts/Malgun Gothic SDF.asset";
        private static bool _isMigrating;

        // Delay until the asset database and TMP settings are ready after a domain reload.
        static RunnerTMPMigration()
        {
            EditorApplication.delayCall += Migrate;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (!_isMigrating && scene.path == ScenePath)
                EditorApplication.delayCall += Migrate;
        }

        [MenuItem("STREAM ON/Migrate Runner Text To TMP")]
        public static void Migrate()
        {
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += Migrate;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro/Resources"))
            {
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                EditorApplication.delayCall += Migrate;
                return;
            }

            TMP_FontAsset font = LoadGalmuriFont();
            if (font == null) return;
            TMP_FontAsset chatFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ChatFontPath);

            _isMigrating = true;
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            Text[] legacyTexts = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Text>(true))
                .ToArray();
            foreach (Text legacy in legacyTexts)
                Convert(legacy, font);

            TMP_Text[] tmpTexts = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true))
                .ToArray();
            foreach (TMP_Text text in tmpTexts)
                text.font = chatFont != null && text.GetComponentInParent<RunnerChatController>() != null
                    ? chatFont : font;

            RebindRunnerUI(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            _isMigrating = false;
            Debug.Log($"STREAM ON runner UI now uses Korean-capable TMP ({legacyTexts.Length} legacy texts converted, {tmpTexts.Length} TMP texts rebound).");
        }

        private static TMP_FontAsset LoadGalmuriFont()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                Debug.LogError($"STREAM ON could not load Galmuri14 SDF at '{FontPath}'.");
            return font;
        }

        private static void Convert(Text legacy, TMP_FontAsset font)
        {
            GameObject target = legacy.gameObject;
            string value = legacy.text;
            float size = legacy.fontSize;
            FontStyle style = legacy.fontStyle;
            TextAnchor alignment = legacy.alignment;
            Color color = legacy.color;
            bool richText = legacy.supportRichText;
            bool raycastTarget = legacy.raycastTarget;
            float lineSpacing = legacy.lineSpacing;
            HorizontalWrapMode horizontal = legacy.horizontalOverflow;
            VerticalWrapMode vertical = legacy.verticalOverflow;

            Object.DestroyImmediate(legacy, true);
            TextMeshProUGUI text = target.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = ToTMPStyle(style);
            text.alignment = ToTMPAlignment(alignment);
            text.color = color;
            text.richText = richText;
            text.raycastTarget = raycastTarget;
            text.lineSpacing = lineSpacing;
            text.textWrappingMode = horizontal == HorizontalWrapMode.Wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = vertical == VerticalWrapMode.Truncate ? TextOverflowModes.Truncate : TextOverflowModes.Overflow;
        }

        private static void RebindRunnerUI(Scene scene)
        {
            TMP_Text[] allText = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true))
                .ToArray();

            RunnerHUD hud = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RunnerHUD>(true)).FirstOrDefault();
            if (hud != null)
            {
                SerializedObject serialized = new SerializedObject(hud);
                SetText(serialized, "scoreText", allText, "Score");
                SetText(serialized, "highScoreText", allText, "Best");
                SetText(serialized, "speedText", allText, "Speed");
                SetText(serialized, "healthText", allText, "Health");
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            RunnerChatController chat = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RunnerChatController>(true)).FirstOrDefault();
            if (chat != null)
            {
                TMP_Text[] messages = allText.Where(text => text.name.StartsWith("Message "))
                    .OrderBy(text => MessageSlotIndex(text.name)).ToArray();
                SerializedObject serialized = new SerializedObject(chat);
                SerializedProperty slots = serialized.FindProperty("messageSlots");
                slots.arraySize = messages.Length;
                for (int i = 0; i < messages.Length; i++)
                    slots.GetArrayElementAtIndex(i).objectReferenceValue = messages[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetText(SerializedObject target, string field, TMP_Text[] texts, string objectName)
        {
            target.FindProperty(field).objectReferenceValue = texts.FirstOrDefault(text => text.name == objectName);
        }

        private static int MessageSlotIndex(string objectName)
        {
            string suffix = objectName.Substring("Message ".Length);
            return int.TryParse(suffix, out int index) ? index : int.MaxValue;
        }

        private static FontStyles ToTMPStyle(FontStyle style) => style switch
        {
            FontStyle.Bold => FontStyles.Bold,
            FontStyle.Italic => FontStyles.Italic,
            FontStyle.BoldAndItalic => FontStyles.Bold | FontStyles.Italic,
            _ => FontStyles.Normal
        };

        private static TextAlignmentOptions ToTMPAlignment(TextAnchor anchor) => anchor switch
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
    }
}
#endif
