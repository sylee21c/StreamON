#if UNITY_EDITOR
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using StreamOn.Minigames.Runner;

namespace StreamOn.EditorTools
{
    public static class MalgunGothicChatFontBaker
    {
        private const string SourceWindowsFont = @"C:\Windows\Fonts\malgun.ttf";
        private const string FontFolder = "Assets/_Project/Fonts";
        private const string SourceAssetPath = FontFolder + "/MalgunGothic.ttf";
        private const string FontAssetPath = FontFolder + "/Malgun Gothic SDF.asset";
        private const string ChatPrefabPath = "Assets/_Project/Prefabs/SharedLiveChat.prefab";
        private const string SessionKey = "StreamOn.MalgunGothicChatFont.2026-08-23.v4";
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

        [MenuItem("STREAM ON/Apply Malgun Gothic To All Game Chats")]
        public static void Bake()
        {
            TMP_FontAsset font = EnsureFontAsset();
            if (font == null) return;
            ApplyToChatPrefab(font);
            foreach (string scenePath in ChatScenes) ApplyToScene(scenePath, font);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("STREAM ON: every game chat now uses Malgun Gothic Dynamic SDF.");
        }

        private static TMP_FontAsset EnsureFontAsset()
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) return existing;
            if (!File.Exists(SourceWindowsFont))
            {
                Debug.LogError("Windows Malgun Gothic was not found at " + SourceWindowsFont);
                return null;
            }
            if (!AssetDatabase.IsValidFolder(FontFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project")) AssetDatabase.CreateFolder("Assets", "_Project");
                AssetDatabase.CreateFolder("Assets/_Project", "Fonts");
            }
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceAssetPath);
            if (sourceFont == null)
            {
                File.Copy(SourceWindowsFont, Path.GetFullPath(SourceAssetPath), true);
                AssetDatabase.ImportAsset(SourceAssetPath, ImportAssetOptions.ForceSynchronousImport);
                sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceAssetPath);
            }
            if (sourceFont == null)
            {
                Debug.LogError("Malgun Gothic TTF could not be imported as a Unity Font.");
                return null;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont, 48, 9,
                GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fontAsset.name = "Malgun Gothic SDF";
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = true;
            Texture2D[] atlases = fontAsset.atlasTextures;
            Material material = fontAsset.material;
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            if (atlases != null)
                foreach (Texture2D atlas in atlases.Where(atlas => atlas != null && !AssetDatabase.Contains(atlas)))
                    AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            if (material != null && !AssetDatabase.Contains(material)) AssetDatabase.AddObjectToAsset(material, fontAsset);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        private static void ApplyToChatPrefab(TMP_FontAsset font)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ChatPrefabPath);
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
            PrefabUtility.SaveAsPrefabAsset(root, ChatPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void ApplyToScene(string scenePath, TMP_FontAsset font)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) return;
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            RunnerChatController[] chats = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RunnerChatController>(true)).ToArray();
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
            if (chats.Length > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
#endif
