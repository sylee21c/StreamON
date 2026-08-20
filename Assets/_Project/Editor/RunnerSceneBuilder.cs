#if UNITY_EDITOR
using System.Linq;
using StreamOn.Minigames.Runner;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StreamOn.EditorTools
{
    [InitializeOnLoad]
    public static class RunnerSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/BroadcastRunner.unity";
        private const string RunSpritePath = "Assets/Art/Inhumania_Asset/CharacterSprite/mor/mor_run.png";
        private const string JumpSpritePath = "Assets/Art/Inhumania_Asset/CharacterSprite/mor/mor_jump.png";

        static RunnerSceneBuilder()
        {
            EditorApplication.delayCall += BuildOnce;
        }

        [MenuItem("STREAM ON/Build Runner Prototype Scene")]
        public static void BuildOnce()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;
            BuildScene();
        }

        private static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            scene.name = "BroadcastRunner";

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Examples & Extras/Fonts/Galmuri14 SDF.asset");
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Sprite[] runFrames = LoadSprites(RunSpritePath);
            Sprite[] jumpFrames = LoadSprites(JumpSpritePath);

            GameObject environment = Root(scene, "Environment");
            CreateSprite("Sky Background", environment.transform, uiSprite, new Color(0.08f, 0.11f, 0.18f), new Vector3(0f, 1f, 5f), new Vector3(24f, 12f, 1f), -10);
            GameObject ground = CreateSprite("Ground", environment.transform, uiSprite, new Color(0.20f, 0.24f, 0.28f), new Vector3(0f, -3.35f, 0f), new Vector3(24f, 1.5f, 1f), 0);
            ground.layer = 6;
            BoxCollider2D groundCollider = ground.AddComponent<BoxCollider2D>();
            groundCollider.size = Vector2.one;

            GameObject systems = Root(scene, "Systems");
            RunnerGameManager manager = new GameObject("Runner Game Manager").AddComponent<RunnerGameManager>();
            SceneManager.MoveGameObjectToScene(manager.gameObject, scene);
            manager.transform.SetParent(systems.transform);
            RunnerObstacleSpawner spawner = new GameObject("Obstacle Spawner").AddComponent<RunnerObstacleSpawner>();
            SceneManager.MoveGameObjectToScene(spawner.gameObject, scene);
            spawner.transform.SetParent(systems.transform);
            Transform spawnPoint = Child(spawner.transform, "Spawn Point", new Vector3(11f, -2.25f, 0f)).transform;

            GameObject playerRoot = Root(scene, "Player");
            playerRoot.transform.position = new Vector3(-5f, -2.25f, 0f);
            Rigidbody2D body = playerRoot.AddComponent<Rigidbody2D>();
            body.gravityScale = 3.3f;
            body.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
            BoxCollider2D playerCollider = playerRoot.AddComponent<BoxCollider2D>();
            playerCollider.size = new Vector2(1.25f, 2.4f);
            playerCollider.offset = new Vector2(0f, 0.75f);
            RunnerPlayerController player = playerRoot.AddComponent<RunnerPlayerController>();

            GameObject visual = Child(playerRoot.transform, "Mor Visual", Vector3.zero);
            SpriteRenderer playerRenderer = visual.AddComponent<SpriteRenderer>();
            playerRenderer.sprite = runFrames.FirstOrDefault();
            playerRenderer.sortingOrder = 10;
            Animator animator = visual.AddComponent<Animator>();
            GameObject groundCheck = Child(playerRoot.transform, "Ground Check", new Vector3(0f, -1.25f, 0f));

            GameObject poolRoot = Root(scene, "Obstacle Previews & Pools");
            RunnerObstacle[] obstacles = new RunnerObstacle[5];
            RunnerObstacleType[] obstacleTypes =
            {
                RunnerObstacleType.Jump,
                RunnerObstacleType.Jump,
                RunnerObstacleType.Roll,
                RunnerObstacleType.Enemy,
                RunnerObstacleType.Enemy
            };
            string[] obstacleNames =
            {
                "Jump Obstacle Preview 1",
                "Jump Obstacle Preview 2",
                "Roll Obstacle Preview",
                "Enemy Preview 1",
                "Enemy Preview 2"
            };
            for (int i = 0; i < obstacles.Length; i++)
            {
                GameObject obstacle = CreateSprite(obstacleNames[i], poolRoot.transform, uiSprite,
                    new Color(0.92f, 0.31f, 0.32f), new Vector3(13f + i * 2f, -2.25f, 0f), new Vector3(1.2f, 2.2f, 1f), 5);
                obstacle.tag = "Respawn";
                BoxCollider2D hitbox = obstacle.AddComponent<BoxCollider2D>();
                hitbox.isTrigger = true;
                obstacles[i] = obstacle.AddComponent<RunnerObstacle>();
                Set(obstacles[i], "gameManager", manager);
                SetEnum(obstacles[i], "obstacleType", (int)obstacleTypes[i]);
            }

            GameObject uiRoot = Root(scene, "UI");
            Canvas canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = uiRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            uiRoot.AddComponent<GraphicRaycaster>();

            RectTransform topHud = Panel("HUD", uiRoot.transform, new Color(0.04f, 0.05f, 0.08f, 0.88f), new Vector2(0.5f, 1f), new Vector2(900f, 90f), new Vector2(-150f, -55f), uiSprite);
            TextMeshProUGUI score = Label("Score", topHud, "SCORE  000000", font, 24, TextAlignmentOptions.MidlineLeft, new Vector2(210f, 50f), new Vector2(-320f, 0f));
            TextMeshProUGUI high = Label("Best", topHud, "BEST  000000", font, 24, TextAlignmentOptions.MidlineLeft, new Vector2(210f, 50f), new Vector2(-95f, 0f));
            TextMeshProUGUI speed = Label("Speed", topHud, "SPEED  5.5", font, 22, TextAlignmentOptions.MidlineLeft, new Vector2(170f, 50f), new Vector2(120f, 0f));
            TextMeshProUGUI health = Label("Health", topHud, "HP  ♥♥♥", font, 24, TextAlignmentOptions.MidlineLeft, new Vector2(190f, 50f), new Vector2(310f, 0f));
            health.color = new Color(1f, 0.38f, 0.43f);

            RectTransform chatPanel = Panel("Live Chat Panel", uiRoot.transform, new Color(0.04f, 0.05f, 0.08f, 0.93f), new Vector2(1f, 0.5f), new Vector2(300f, 720f), new Vector2(-150f, 0f), uiSprite);
            Label("Title", chatPanel, "LIVE CHAT", font, 24, TextAlignmentOptions.MidlineLeft, new Vector2(250f, 55f), new Vector2(0f, 320f)).color = new Color(0.4f, 0.9f, 0.82f);
            TextMeshProUGUI[] chatSlots = new TextMeshProUGUI[8];
            for (int i = 0; i < chatSlots.Length; i++)
                chatSlots[i] = Label($"Message {i + 1}", chatPanel, string.Empty, font, 18, TextAlignmentOptions.MidlineLeft, new Vector2(250f, 56f), new Vector2(0f, 245f - i * 70f));
            RunnerChatController chat = chatPanel.gameObject.AddComponent<RunnerChatController>();
            Set(chat, "messageSlots", chatSlots);

            RectTransform help = Panel("Controls", uiRoot.transform, new Color(0.04f, 0.05f, 0.08f, 0.80f), new Vector2(0f, 0f), new Vector2(360f, 58f), new Vector2(200f, 42f), uiSprite);
            Label("Text", help, "SPACE / ↑  점프", font, 21, TextAlignmentOptions.Center, new Vector2(330f, 45f), Vector2.zero);

            RectTransform gameOver = Panel("Game Over Panel", uiRoot.transform, new Color(0.03f, 0.04f, 0.07f, 0.96f), new Vector2(0.5f, 0.5f), new Vector2(520f, 280f), new Vector2(-150f, 0f), uiSprite);
            Label("Title", gameOver, "GAME OVER", font, 42, TextAlignmentOptions.Center, new Vector2(440f, 65f), new Vector2(0f, 70f));
            Label("Guide", gameOver, "R 키 또는 버튼으로 재도전", font, 20, TextAlignmentOptions.Center, new Vector2(440f, 45f), new Vector2(0f, 15f));
            Button retry = ButtonObject("Retry Button", gameOver, "다시 달리기", font, uiSprite, new Vector2(250f, 62f), new Vector2(0f, -70f));
            UnityEventTools.AddPersistentListener(retry.onClick, manager.RestartRun);

            RunnerHUD hud = topHud.gameObject.AddComponent<RunnerHUD>();
            Set(hud, "scoreText", score); Set(hud, "highScoreText", high); Set(hud, "speedText", speed); Set(hud, "healthText", health); Set(hud, "gameOverPanel", gameOver.gameObject);

            GameObject cameraRoot = Root(scene, "Main Camera");
            cameraRoot.tag = "MainCamera";
            cameraRoot.transform.position = new Vector3(-1.5f, 0f, -10f);
            Camera camera = cameraRoot.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.11f, 0.18f);
            cameraRoot.AddComponent<AudioListener>();

            GameObject eventSystem = Root(scene, "EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            Set(manager, "player", player); Set(manager, "spawner", spawner); Set(manager, "chat", chat); Set(manager, "hud", hud);
            Set(player, "gameManager", manager); Set(player, "animator", animator); Set(player, "groundCheck", groundCheck.transform); Set(player, "groundLayer", (LayerMask)(1 << 6));
            Set(spawner, "gameManager", manager);
            Set(spawner, "spawnPoint", spawnPoint);
            Set(spawner, "jumpObstacles", obstacles.Take(2).Cast<Object>().ToArray());
            Set(spawner, "rollObstacles", new Object[] { obstacles[2] });
            Set(spawner, "enemyObstacles", obstacles.Skip(3).Cast<Object>().ToArray());

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"STREAM ON runner scene created: {ScenePath}");
        }

        private static Sprite[] LoadSprites(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(FrameIndex).ToArray();
        private static int FrameIndex(Sprite sprite)
        {
            int separator = sprite.name.LastIndexOf('_');
            return separator >= 0 && int.TryParse(sprite.name.Substring(separator + 1), out int index) ? index : 0;
        }
        private static GameObject Root(Scene scene, string name) { GameObject go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
        private static GameObject Child(Transform parent, string name, Vector3 localPosition) { GameObject go = new GameObject(name); go.transform.SetParent(parent); go.transform.localPosition = localPosition; return go; }
        private static GameObject CreateSprite(string name, Transform parent, Sprite sprite, Color color, Vector3 position, Vector3 scale, int order)
        { GameObject go = Child(parent, name, position); SpriteRenderer sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.color = color; sr.sortingOrder = order; go.transform.localScale = scale; return go; }

        private static RectTransform Panel(string name, Transform parent, Color color, Vector2 anchor, Vector2 size, Vector2 pos, Sprite sprite)
        { GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); Image image = go.GetComponent<Image>(); image.sprite = sprite; image.color = color; RectTransform rt = go.GetComponent<RectTransform>(); Place(rt, anchor, size, pos); return rt; }
        private static TextMeshProUGUI Label(string name, Transform parent, string value, TMP_FontAsset font, int size, TextAlignmentOptions align, Vector2 dimensions, Vector2 pos)
        { GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.text = value; text.color = Color.white; text.alignment = align; text.richText = true; text.textWrappingMode = TextWrappingModes.Normal; Place(text.rectTransform, new Vector2(0.5f, 0.5f), dimensions, pos); return text; }
        private static Button ButtonObject(string name, Transform parent, string label, TMP_FontAsset font, Sprite sprite, Vector2 size, Vector2 pos)
        { RectTransform rt = Panel(name, parent, new Color(0.15f, 0.72f, 0.63f), new Vector2(0.5f, 0.5f), size, pos, sprite); Button button = rt.gameObject.AddComponent<Button>(); Label("Label", rt, label, font, 22, TextAlignmentOptions.Center, size, Vector2.zero); return button; }
        private static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos) { rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size; rt.anchoredPosition = pos; }
        private static void Set(Object target, string property, Object value) { SerializedObject so = new SerializedObject(target); so.FindProperty(property).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Set(Object target, string property, Object[] values) { SerializedObject so = new SerializedObject(target); SerializedProperty p = so.FindProperty(property); p.arraySize = values.Length; for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Set(Object target, string property, LayerMask value) { SerializedObject so = new SerializedObject(target); so.FindProperty(property).intValue = value.value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetEnum(Object target, string property, int value) { SerializedObject so = new SerializedObject(target); so.FindProperty(property).enumValueIndex = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void AddToBuildSettings()
        { var scenes = EditorBuildSettings.scenes.ToList(); scenes.RemoveAll(s => s.path == ScenePath); scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
    }
}
#endif
