#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using StreamOn.Minigames.Runner;

namespace StreamOn.EditorTools
{
    public static class MalgunGothicChatFontBaker
    {
        private const string FontAssetPath = "Assets/TextMesh Pro/Examples & Extras/Fonts/Galmuri14 SDF.asset";
        private static readonly string[] BroadcastPrefabPaths =
        {
            "Assets/_Project/Prefabs/SharedLiveChat.prefab",
            "Assets/_Project/Prefabs/SharedDonationPopup.prefab",
            "Assets/_Project/Prefabs/SharedMissionEvent.prefab",
            "Assets/_Project/Prefabs/SharedWitInteraction.prefab",
            "Assets/Prefabs/Broadcast Heat Scene UI.prefab",
            "Assets/_Project/Prefabs/SharedBroadcastSettlement.prefab"
        };
        private const string SessionKey = "StreamOn.Galmuri14BroadcastFont.2026-08-25.v1";
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
                    Bake();
            };
        }

        [MenuItem("STREAM ON/Apply Galmuri14 To Broadcast System")]
        public static void Bake()
        {
            TMP_FontAsset font = EnsureFontAsset();
            if (font == null) return;
            foreach (string prefabPath in BroadcastPrefabPaths) ApplyToBroadcastPrefab(prefabPath, font);
            foreach (string scenePath in ChatScenes) ApplyToScene(scenePath, font);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("STREAM ON: every shared broadcast UI now uses Galmuri14 SDF.");
        }

        private static TMP_FontAsset EnsureFontAsset()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null) Debug.LogError("Galmuri14 SDF was not found at " + FontAssetPath);
            return font;
        }

        private static void ApplyToBroadcastPrefab(string prefabPath, TMP_FontAsset font)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null) return;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = font;
                EditorUtility.SetDirty(text);
            }
            RunnerChatController chat = root.GetComponent<RunnerChatController>();
            if (chat != null)
            {
                SerializedObject serialized = new SerializedObject(chat);
                serialized.FindProperty("chatFont").objectReferenceValue = font;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void ApplyToScene(string scenePath, TMP_FontAsset font)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) return;
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            List<RunnerChatController> chats = new List<RunnerChatController>();
            foreach (GameObject root in scene.GetRootGameObjects())
                chats.AddRange(root.GetComponentsInChildren<RunnerChatController>(true));
            foreach (RunnerChatController chat in chats)
            {
                SerializedObject serialized = new SerializedObject(chat);
                serialized.FindProperty("chatFont").objectReferenceValue = font;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                foreach (TMP_Text text in chat.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.font = font;
                    EditorUtility.SetDirty(text);
                }
            }
            if (chats.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
#endif
