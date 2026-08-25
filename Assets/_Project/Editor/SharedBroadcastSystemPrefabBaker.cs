#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using StreamOn.Minigames.Runner;
using StreamOn.Minigames.TileArena;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StreamOn.EditorTools
{
    public static class SharedBroadcastSystemPrefabBaker
    {
        private const string CompositePrefabPath = "Assets/_Project/Prefabs/SharedBroadcastSystem.prefab";
        private const string ChatPrefabPath = "Assets/_Project/Prefabs/SharedLiveChat.prefab";
        private const string DonationPrefabPath = "Assets/_Project/Prefabs/SharedDonationPopup.prefab";
        private const string MissionPrefabPath = "Assets/_Project/Prefabs/SharedMissionEvent.prefab";
        private const string WitPrefabPath = "Assets/_Project/Prefabs/SharedWitInteraction.prefab";
        private const string HeatPrefabPath = "Assets/Prefabs/Broadcast Heat Scene UI.prefab";
        private const string SettlementPrefabPath = "Assets/_Project/Prefabs/SharedBroadcastSettlement.prefab";
        private const string RunnerScenePath = "Assets/Scenes/BroadcastRunner.unity";
        private const string TileScenePath = "Assets/Scenes/TileArena.unity";
        private const string PlasticScenePath = "Assets/Scenes/MainScene.unity";

        private sealed class LayoutSnapshot
        {
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 pivot;
            public Vector2 anchoredPosition;
            public Vector2 sizeDelta;
            public Vector3 localScale;
        }

        [MenuItem("STREAM ON/Shared UI/Bake Shared Broadcast System Prefab")]
        public static void Bake()
        {
            if (!BuildCompositePrefab()) return;
            BakeScene(RunnerScenePath);
            BakeScene(TileScenePath);
            BakeScene(PlasticScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("STREAM ON: shared broadcast system prefab applied to Runner, Tile Arena, and Plastic Knightmare.");
        }

        private static bool BuildCompositePrefab()
        {
            BroadcastMissionPrefabBuilder.EnsurePrefab();
            GameObject chatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChatPrefabPath);
            GameObject donationPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DonationPrefabPath);
            GameObject missionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MissionPrefabPath);
            GameObject witPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WitPrefabPath);
            GameObject heatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeatPrefabPath);
            GameObject settlementPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettlementPrefabPath);
            if (chatPrefab == null || donationPrefab == null || missionPrefab == null || witPrefab == null
                || heatPrefab == null || settlementPrefab == null)
            {
                Debug.LogError("STREAM ON: a child prefab required by SharedBroadcastSystem is missing.");
                return false;
            }

            Dictionary<Type, LayoutSnapshot> runnerLayout = CaptureRunnerLayout();
            GameObject root = new GameObject("Shared Broadcast System", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(SharedBroadcastSystemRoot));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RunnerChatController chat = AddChild<RunnerChatController>(chatPrefab, root.transform,
                runnerLayout, DefaultChatLayout());
            RunnerDonationPopupController donation = AddChild<RunnerDonationPopupController>(donationPrefab,
                root.transform, runnerLayout, DefaultDonationLayout());
            BroadcastMissionEventController mission = AddChild<BroadcastMissionEventController>(missionPrefab,
                root.transform, runnerLayout, DefaultDonationLayout());
            RunnerWitInteractionController wit = AddChild<RunnerWitInteractionController>(witPrefab,
                root.transform, runnerLayout, DefaultWitLayout());
            ApplyLayout(wit.GetComponent<RectTransform>(), DefaultWitLayout());
            RunnerBroadcastHeatGauge heat = AddChild<RunnerBroadcastHeatGauge>(heatPrefab,
                root.transform, runnerLayout, DefaultHeatLayout());
            RunnerBroadcastSettlementView settlement = AddChild<RunnerBroadcastSettlementView>(settlementPrefab,
                root.transform, runnerLayout, DefaultSettlementLayout());

            SerializedObject rootSo = new SerializedObject(root.GetComponent<SharedBroadcastSystemRoot>());
            rootSo.FindProperty("chat").objectReferenceValue = chat;
            rootSo.FindProperty("donationPopup").objectReferenceValue = donation;
            rootSo.FindProperty("missionEvent").objectReferenceValue = mission;
            rootSo.FindProperty("witInteraction").objectReferenceValue = wit;
            rootSo.FindProperty("heatAndFocusGauge").objectReferenceValue = heat;
            rootSo.FindProperty("settlementView").objectReferenceValue = settlement;
            rootSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, CompositePrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return true;
        }

        private static Dictionary<Type, LayoutSnapshot> CaptureRunnerLayout()
        {
            Dictionary<Type, LayoutSnapshot> result = new Dictionary<Type, LayoutSnapshot>();
            Scene scene = SceneManager.GetSceneByPath(RunnerScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(RunnerScenePath, OpenSceneMode.Additive);
            Capture<RunnerChatController>(scene, result);
            Capture<RunnerDonationPopupController>(scene, result);
            Capture<BroadcastMissionEventController>(scene, result);
            Capture<RunnerWitInteractionController>(scene, result);
            Capture<RunnerBroadcastHeatGauge>(scene, result);
            Capture<RunnerBroadcastSettlementView>(scene, result);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            return result;
        }

        private static void Capture<T>(Scene scene, IDictionary<Type, LayoutSnapshot> result) where T : Component
        {
            T component = FindInScene<T>(scene);
            if (component != null && component.transform is RectTransform rect)
                result[typeof(T)] = Snapshot(rect);
        }

        private static T AddChild<T>(GameObject prefab, Transform parent,
            IReadOnlyDictionary<Type, LayoutSnapshot> runnerLayout, LayoutSnapshot fallback) where T : Component
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            instance.transform.SetParent(parent, false);
            instance.name = prefab.name;
            T component = instance.GetComponentInChildren<T>(true);
            LayoutSnapshot layout = runnerLayout.TryGetValue(typeof(T), out LayoutSnapshot captured)
                ? captured : fallback;
            ApplyLayout(instance.GetComponent<RectTransform>(), layout);
            return component;
        }

        private static void BakeScene(string path)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            foreach (SharedBroadcastSystemRoot oldComposite in FindAllInScene<SharedBroadcastSystemRoot>(scene))
                DestroyUiInstance(oldComposite.gameObject);
            DestroyAllUiInstances<RunnerChatController>(scene);
            DestroyAllUiInstances<RunnerDonationPopupController>(scene);
            DestroyAllUiInstances<BroadcastMissionEventController>(scene);
            DestroyAllUiInstances<RunnerWitInteractionController>(scene);
            DestroyAllUiInstances<RunnerBroadcastHeatGauge>(scene);
            DestroyAllUiInstances<RunnerBroadcastSettlementView>(scene);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CompositePrefabPath);
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            instance.name = "Shared Broadcast System";
            SharedBroadcastSystemRoot shared = instance.GetComponent<SharedBroadcastSystemRoot>();
            ReconnectScene(scene, shared);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }

        private static void ReconnectScene(Scene scene, SharedBroadcastSystemRoot shared)
        {
            RunnerGameManager runner = FindInScene<RunnerGameManager>(scene);
            if (runner != null)
            {
                SerializedObject so = new SerializedObject(runner);
                so.FindProperty("chat").objectReferenceValue = shared.Chat;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            TileArenaChatAdapter tileAudience = FindInScene<TileArenaChatAdapter>(scene);
            if (tileAudience != null)
            {
                SerializedObject so = new SerializedObject(tileAudience);
                so.FindProperty("chatController").objectReferenceValue = shared.Chat;
                so.FindProperty("donationPopup").objectReferenceValue = shared.DonationPopup;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            TileArenaBroadcastSessionController tileSession = FindInScene<TileArenaBroadcastSessionController>(scene);
            if (tileSession != null)
            {
                SerializedObject so = new SerializedObject(tileSession);
                so.FindProperty("settlementView").objectReferenceValue = shared.SettlementView;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PlasticKnightmareBroadcastController plastic = FindInScene<PlasticKnightmareBroadcastController>(scene);
            if (plastic != null)
            {
                SerializedObject so = new SerializedObject(plastic);
                so.FindProperty("chat").objectReferenceValue = shared.Chat;
                so.FindProperty("donationPopup").objectReferenceValue = shared.DonationPopup;
                so.FindProperty("witInteraction").objectReferenceValue = shared.WitInteraction;
                so.FindProperty("settlementView").objectReferenceValue = shared.SettlementView;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void DestroyAllUiInstances<T>(Scene scene) where T : Component
        {
            HashSet<GameObject> destroyed = new HashSet<GameObject>();
            foreach (T component in FindAllInScene<T>(scene))
            {
                GameObject target = PrefabUtility.IsPartOfPrefabInstance(component)
                    ? PrefabUtility.GetOutermostPrefabInstanceRoot(component.gameObject)
                    : component.gameObject;
                if (target != null && destroyed.Add(target)) UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void DestroyUiInstance(GameObject gameObject)
        {
            GameObject target = PrefabUtility.IsPartOfPrefabInstance(gameObject)
                ? PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject)
                : gameObject;
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }

        private static T FindInScene<T>(Scene scene) where T : Component =>
            FindAllInScene<T>(scene).FirstOrDefault();

        private static T[] FindAllInScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static LayoutSnapshot Snapshot(RectTransform rect) => new LayoutSnapshot
        {
            anchorMin = rect.anchorMin,
            anchorMax = rect.anchorMax,
            pivot = rect.pivot,
            anchoredPosition = rect.anchoredPosition,
            sizeDelta = rect.sizeDelta,
            localScale = rect.localScale
        };

        private static void ApplyLayout(RectTransform rect, LayoutSnapshot layout)
        {
            rect.anchorMin = layout.anchorMin;
            rect.anchorMax = layout.anchorMax;
            rect.pivot = layout.pivot;
            rect.anchoredPosition = layout.anchoredPosition;
            rect.sizeDelta = layout.sizeDelta;
            rect.localScale = layout.localScale;
        }

        private static LayoutSnapshot Layout(Vector2 anchor, Vector2 pivot, Vector2 position,
            Vector2 size, Vector3 scale) => new LayoutSnapshot
        {
            anchorMin = anchor,
            anchorMax = anchor,
            pivot = pivot,
            anchoredPosition = position,
            sizeDelta = size,
            localScale = scale
        };

        private static LayoutSnapshot DefaultChatLayout() =>
            Layout(new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(-150f, 0f), new Vector2(300f, 720f), Vector3.one);
        private static LayoutSnapshot DefaultDonationLayout() =>
            Layout(new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(-150f, -82f), new Vector2(560f, 150f), Vector3.one);
        private static LayoutSnapshot DefaultWitLayout() =>
            Layout(new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(-120f, -190f), new Vector2(760f, 400f), Vector3.one);
        private static LayoutSnapshot DefaultHeatLayout() =>
            Layout(new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(538.2f, -3.7f), new Vector2(360f, 116f), Vector3.one * .54880387f);
        private static LayoutSnapshot DefaultSettlementLayout() =>
            Layout(new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(680f, 530f), Vector3.one);
    }
}
#endif
