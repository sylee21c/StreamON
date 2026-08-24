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
    public static class ChzzkChatStyleBaker
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/SharedLiveChat.prefab";
        private const string SessionKey = "StreamOn.ChzzkChatStyle.2026-08-23.v2";
        private static readonly Color32 BackgroundColor = new Color32(20, 21, 23, 255);
        private static readonly Color32 TextColor = new Color32(223, 226, 234, 255);
        private static readonly string[] ChatScenes =
        {
            "Assets/Scenes/BroadcastRunner.unity",
            "Assets/Scenes/TileArena.unity",
            "Assets/Scenes/MainScene.unity"
        };

        [InitializeOnLoadMethod]
        private static void Schedule()
        {
            if (Application.isBatchMode || SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling)
                    Apply();
            };
        }

        [MenuItem("STREAM ON/Shared UI/Apply CHZZK Text Chat Style")]
        public static void Apply()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) return;

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(340f, 720f);
            Image panel = root.GetComponent<Image>();
            panel.sprite = null;
            panel.type = Image.Type.Simple;
            panel.color = BackgroundColor;

            TMP_Text title = root.GetComponentsInChildren<TMP_Text>(true).First(text => text.name == "Title");
            StyleText(title, 16f, new Vector2(306f, 64f), new Vector2(0f, 326f));
            title.text = "채팅  /  LOCAL\n현재 시청자 0명";

            TMP_Text[] existing = MessageSlots(root);
            if (existing.Length == 0)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.LogError("STREAM ON: SharedLiveChat has no message slot to restyle.");
                return;
            }

            for (int i = existing.Length; i < 12; i++)
            {
                GameObject clone = Object.Instantiate(existing[0].gameObject, root.transform);
                clone.name = $"Message {i + 1}";
            }

            foreach (TMP_Text extra in MessageSlots(root).Where(text => SlotIndex(text.name) > 12).ToArray())
                Object.DestroyImmediate(extra.gameObject);

            TMP_Text[] slots = MessageSlots(root).Take(12).ToArray();
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].name = $"Message {i + 1}";
                StyleText(slots[i], 16f, new Vector2(306f, 44f), new Vector2(0f, 274f - i * 49f));
                slots[i].overflowMode = TextOverflowModes.Ellipsis;
                slots[i].raycastTarget = true;
            }

            RunnerChatController chat = root.GetComponent<RunnerChatController>();
            SerializedObject serialized = new SerializedObject(chat);
            SerializedProperty messages = serialized.FindProperty("messageSlots");
            messages.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
                messages.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            foreach (string scenePath in ChatScenes) ApplyToScene(scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("STREAM ON: shared live chat now uses the CHZZK-inspired text-only layout.");
        }

        private static TMP_Text[] MessageSlots(GameObject root) => root.GetComponentsInChildren<TMP_Text>(true)
            .Where(text => text.name.StartsWith("Message "))
            .OrderBy(text => SlotIndex(text.name)).ToArray();

        private static void ApplyToScene(string scenePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) return;
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            RunnerChatController[] chats = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RunnerChatController>(true)).ToArray();
            foreach (RunnerChatController chat in chats)
            {
                RectTransform rootRect = chat.GetComponent<RectTransform>();
                rootRect.sizeDelta = new Vector2(340f, 720f);
                Image panel = chat.GetComponent<Image>();
                panel.sprite = null;
                panel.type = Image.Type.Simple;
                panel.color = BackgroundColor;

                TMP_Text title = chat.GetComponentsInChildren<TMP_Text>(true).First(text => text.name == "Title");
                StyleText(title, 16f, new Vector2(306f, 64f), new Vector2(0f, 326f));
                TMP_Text[] slots = MessageSlots(chat.gameObject).Take(12).ToArray();
                for (int i = 0; i < slots.Length; i++)
                {
                    StyleText(slots[i], 16f, new Vector2(306f, 44f), new Vector2(0f, 274f - i * 49f));
                    slots[i].overflowMode = TextOverflowModes.Ellipsis;
                }

                SerializedObject serialized = new SerializedObject(chat);
                SerializedProperty messages = serialized.FindProperty("messageSlots");
                messages.arraySize = slots.Length;
                for (int i = 0; i < slots.Length; i++)
                    messages.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(chat);
            }

            if (chats.Length > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }

        private static void StyleText(TMP_Text text, float size, Vector2 dimensions, Vector2 position)
        {
            text.fontSize = size;
            text.fontWeight = FontWeight.Regular;
            text.fontStyle = FontStyles.Normal;
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.characterSpacing = 0f;
            text.wordSpacing = 0f;
            text.lineSpacing = 0f;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = dimensions;
            rect.anchoredPosition = position;
        }

        private static int SlotIndex(string objectName)
        {
            string suffix = objectName.Substring("Message ".Length);
            return int.TryParse(suffix, out int index) ? index : int.MaxValue;
        }
    }
}
#endif
