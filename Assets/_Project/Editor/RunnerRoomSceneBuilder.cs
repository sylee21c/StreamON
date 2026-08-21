#if UNITY_EDITOR
using System;
using System.Linq;
using StreamOn.Minigames.Runner;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace StreamOn.EditorTools
{
    public static class RunnerRoomSceneBuilder
    {
        private const string RoomScenePath = "Assets/Scenes/StreamerRoom.unity";
        private const string RunnerScenePath = "Assets/Scenes/BroadcastRunner.unity";
        private const string SettingsPath = "Assets/_Project/Settings/RunnerCampaignSettings.asset";
        private const string FontPath = "Assets/TextMesh Pro/Examples & Extras/Fonts/Galmuri14 SDF.asset";

        static RunnerRoomSceneBuilder()
        {
            EditorApplication.delayCall += BuildMissingRoom;
        }

        private static void BuildMissingRoom()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RoomScenePath) == null) BuildAll();
        }

        [MenuItem("STREAM ON/Build or Refresh 3D Streamer Room")]
        public static void BuildAll()
        {
            BuildRoomScene();
            InstallPauseController();
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("STREAM ON: 3D room, pause menu, and build settings are ready.");
            if (Environment.GetCommandLineArgs().Contains("-quitAfterRoomBuild")) EditorApplication.Exit(0);
        }

        public static void ValidateSaveSystem()
        {
            bool valid = true;
            RunnerCampaignSettings settings = AssetDatabase.LoadAssetAtPath<RunnerCampaignSettings>(SettingsPath);
            valid &= settings != null && settings.saveSlotCount >= 1;
            RunnerBroadcastGrowthSettings growth = settings != null ? settings.broadcastGrowthSettings : null;
            valid &= growth != null;
            if (growth != null)
            {
                int chatters2 = growth.ChattersForViewers(2);
                int chatters10 = growth.ChattersForViewers(10);
                int chatters100 = growth.ChattersForViewers(100);
                int chatters1000 = growth.ChattersForViewers(1000);
                valid &= chatters2 <= 2 && chatters10 < 10 && chatters100 < 100 && chatters1000 < 1000;
                valid &= chatters2 <= chatters10 && chatters10 <= chatters100 && chatters100 <= chatters1000;
                Debug.Log($"STREAM ON audience curve: 2->{chatters2}, 10->{chatters10}, 100->{chatters100}, 1000->{chatters1000} chatters.");
            }
            valid &= ValidateSceneComponent<RunnerRoomController>(RoomScenePath);
            valid &= ValidateSceneComponent<RunnerPauseController>(RunnerScenePath);
            if (valid) Debug.Log("STREAM ON validation: save slots, audience growth, room controller, and pause controller are connected.");
            else Debug.LogError("STREAM ON validation failed: a required save-system reference is missing.");
            if (Environment.GetCommandLineArgs().Contains("-quitAfterValidation")) EditorApplication.Exit(valid ? 0 : 1);
        }

        private static void BuildRoomScene()
        {
            RunnerCampaignSettings settings = AssetDatabase.LoadAssetAtPath<RunnerCampaignSettings>(SettingsPath);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            scene.name = "StreamerRoom";

            GameObject environment = Root(scene, "Room Environment");
            Primitive("Floor", environment.transform, PrimitiveType.Cube, new Vector3(0, -0.15f, 0), new Vector3(11, .3f, 10), new Color(.20f, .18f, .17f));
            Primitive("Back Wall", environment.transform, PrimitiveType.Cube, new Vector3(0, 2.5f, 5), new Vector3(11, 5, .25f), new Color(.43f, .45f, .50f));
            Primitive("Left Wall", environment.transform, PrimitiveType.Cube, new Vector3(-5.5f, 2.5f, 0), new Vector3(.25f, 5, 10), new Color(.36f, .38f, .44f));
            Primitive("Right Wall", environment.transform, PrimitiveType.Cube, new Vector3(5.5f, 2.5f, 0), new Vector3(.25f, 5, 10), new Color(.36f, .38f, .44f));
            Primitive("Rug", environment.transform, PrimitiveType.Cube, new Vector3(0, .03f, 0), new Vector3(5.4f, .06f, 4.2f), new Color(.12f, .28f, .32f));

            GameObject activitiesRoot = Root(scene, "Editable Day Activities");
            foreach (RunnerCampaignActionDefinition action in settings.dayActions.Where(action => action != null))
            {
                GameObject activity;
                if (action.roomPrefab != null)
                {
                    activity = (GameObject)PrefabUtility.InstantiatePrefab(action.roomPrefab, scene);
                    activity.name = action.displayName + " Activity";
                    activity.transform.SetParent(activitiesRoot.transform);
                    activity.transform.position = action.roomPosition;
                    activity.transform.localScale = action.roomScale;
                }
                else
                {
                    activity = Primitive(action.displayName + " Activity", activitiesRoot.transform, PrimitiveType.Cube,
                        action.roomPosition, action.roomScale, action.roomColor);
                }
                activity.AddComponent<RunnerRoomActivity>().Configure(action.id, false, action.displayName);
            }

            GameObject computerArea = new GameObject("Broadcast Computer");
            SceneManager.MoveGameObjectToScene(computerArea, scene);
            computerArea.transform.SetParent(activitiesRoot.transform);
            computerArea.transform.position = new Vector3(0, 0, 3.65f);
            computerArea.AddComponent<RunnerRoomActivity>().Configure(string.Empty, true, "컴퓨터로 방송 시작");
            Primitive("Desk", computerArea.transform, PrimitiveType.Cube, new Vector3(0, .7f, 0), new Vector3(3.2f, .18f, 1.1f), new Color(.28f, .16f, .09f), true);
            Primitive("Monitor", computerArea.transform, PrimitiveType.Cube, new Vector3(0, 1.45f, .1f), new Vector3(1.45f, .9f, .14f), new Color(.04f, .08f, .11f), true);
            Primitive("Monitor Glow", computerArea.transform, PrimitiveType.Cube, new Vector3(0, 1.45f, .015f), new Vector3(1.25f, .7f, .03f), new Color(.10f, .78f, .76f), true);

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Room Player";
            SceneManager.MoveGameObjectToScene(player, scene);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.transform.position = new Vector3(0, 1, -2.5f);
            CharacterController character = player.AddComponent<CharacterController>();
            character.height = 2f; character.radius = .48f;
            player.AddComponent<RunnerRoomPlayerController>();
            Renderer placeholderRenderer = player.GetComponent<Renderer>();
            if (placeholderRenderer != null) placeholderRenderer.enabled = false;

            GameObject cameraObject = Root(scene, "Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 9.5f, -10.5f);
            cameraObject.transform.LookAt(new Vector3(0, .8f, .7f));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 52f; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.055f, .07f, .10f);
            cameraObject.AddComponent<AudioListener>();

            GameObject lightObject = Root(scene, "Room Light");
            Light light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f; light.color = new Color(1f, .91f, .78f);
            lightObject.transform.rotation = Quaternion.Euler(48, -28, 0);
            GameObject fillLight = Root(scene, "Computer Fill Light");
            Light point = fillLight.AddComponent<Light>(); point.type = LightType.Point; point.range = 8f; point.intensity = 4f; point.color = new Color(.15f, .75f, 1f);
            fillLight.transform.position = new Vector3(0, 2.2f, 3.2f);

            GameObject canvasObject = Root(scene, "Room UI");
            Canvas canvas = canvasObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720);
            canvasObject.AddComponent<GraphicRaycaster>();
            TextMeshProUGUI title = Label("Title", canvas.transform, "STREAMER ROOM", font, 34, TextAlignmentOptions.TopLeft, new Vector2(500, 60), new Vector2(35, -25), new Vector2(0, 1));
            title.color = new Color(.35f, .92f, .84f);
            TextMeshProUGUI status = Label("Status", canvas.transform, string.Empty, font, 22, TextAlignmentOptions.TopRight, new Vector2(700, 90), new Vector2(-35, -25), new Vector2(1, 1));
            TextMeshProUGUI prompt = Label("Interaction Prompt", canvas.transform, "WASD 이동  |  가까이 가서 E 상호작용", font, 25, TextAlignmentOptions.Center, new Vector2(950, 90), new Vector2(0, 35), new Vector2(.5f, 0));

            RunnerRoomController controller = new GameObject("Room Game Manager").AddComponent<RunnerRoomController>();
            SceneManager.MoveGameObjectToScene(controller.gameObject, scene);
            Set(controller, "settings", settings); Set(controller, "player", player.transform); Set(controller, "statusText", status); Set(controller, "promptText", prompt);

            GameObject eventSystem = Root(scene, "EventSystem"); eventSystem.AddComponent<EventSystem>(); eventSystem.AddComponent<InputSystemUIInputModule>();
            EditorSceneManager.SaveScene(scene, RoomScenePath);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void InstallPauseController()
        {
            Scene scene = SceneManager.GetSceneByPath(RunnerScenePath);
            bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(RunnerScenePath, OpenSceneMode.Additive);
            RunnerGameManager manager = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RunnerGameManager>(true)).FirstOrDefault();
            if (manager != null)
            {
                RunnerPauseController pause = manager.GetComponent<RunnerPauseController>();
                if (pause == null) pause = manager.gameObject.AddComponent<RunnerPauseController>();
                Set(pause, "gameManager", manager);
                Set(pause, "chat", Object.FindFirstObjectByType<RunnerChatController>());
                Set(pause, "settings", AssetDatabase.LoadAssetAtPath<RunnerCampaignSettings>(SettingsPath));
                EditorSceneManager.SaveScene(scene, RunnerScenePath);
            }
            if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true);
        }

        private static void UpdateBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(entry => entry.path == RoomScenePath || entry.path == RunnerScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(RoomScenePath, true));
            scenes.Insert(1, new EditorBuildSettingsScene(RunnerScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static bool ValidateSceneComponent<T>(string path) where T : Component
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            bool found = scene.GetRootGameObjects().Any(root => root.GetComponentInChildren<T>(true) != null);
            if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true);
            return found;
        }

        private static GameObject Root(Scene scene, string name) { GameObject go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
        private static GameObject Primitive(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Color color, bool local = false)
        {
            GameObject go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent);
            if (local) go.transform.localPosition = position; else go.transform.position = position;
            go.transform.localScale = scale; SetColor(go, color); return go;
        }
        private static void SetColor(GameObject go, Color color)
        {
            Renderer renderer = go.GetComponent<Renderer>(); if (renderer == null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { color = color }; renderer.sharedMaterial = material;
        }
        private static TextMeshProUGUI Label(string name, Transform parent, string value, TMP_FontAsset font, int size, TextAlignmentOptions alignment, Vector2 dimensions, Vector2 position, Vector2 anchor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.text = value; text.color = Color.white; text.alignment = alignment;
            RectTransform rect = text.rectTransform; rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = anchor; rect.sizeDelta = dimensions; rect.anchoredPosition = position; return text;
        }
        private static void Set(Object target, string property, Object value) { SerializedObject serialized = new SerializedObject(target); serialized.FindProperty(property).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
#endif
