#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StreamOn.Minigames.Runner;

namespace StreamOn.EditorTools
{
    public static class BroadcastMultiGameIntegrationBaker
    {
        private const string SessionBakeKey = "StreamOn.MultiGameBroadcastBake.2026-08-23.v2";
        private const string ExplorerSessionKey = "StreamOn.CleanGameExplorer.2026-08-23.v1";
        private const string RunnerScene = "Assets/Scenes/BroadcastRunner.unity";
        private const string RoomScene = "Assets/Scenes/StreamerRoom.unity";
        private const string TileScene = "Assets/Scenes/TileArena.unity";
        private const string PlasticScene = "Assets/Scenes/MainScene.unity";
        private const string PausePrefab = "Assets/_Project/Prefabs/SharedBroadcastPause.prefab";
        private const string SettingsPath = "Assets/_Project/Settings/RunnerCampaignSettings.asset";
        private const string ChatPrefab = "Assets/_Project/Prefabs/SharedLiveChat.prefab";
        private const string DonationPrefab = "Assets/_Project/Prefabs/SharedDonationPopup.prefab";
        private const string WitPrefab = "Assets/_Project/Prefabs/SharedWitInteraction.prefab";
        private const string SettlementPrefab = "Assets/_Project/Prefabs/SharedBroadcastSettlement.prefab";
        private const string ExplorerPrefab = "Assets/_Project/Prefabs/BroadcastGameExplorer.prefab";

        [InitializeOnLoadMethod]
        private static void ScheduleBakeAfterCompilation()
        {
            if (Application.isBatchMode || SessionState.GetBool(SessionBakeKey, false)) return;
            SessionState.SetBool(SessionBakeKey, true);
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling)
                    Bake();
            };
        }

        [InitializeOnLoadMethod]
        private static void ScheduleCleanExplorerAfterCompilation()
        {
            if (Application.isBatchMode || SessionState.GetBool(ExplorerSessionKey, false)) return;
            SessionState.SetBool(ExplorerSessionKey, true);
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                BuildCleanExplorerPrefab();
                BakeExplorerSelection();
                AssetDatabase.SaveAssets();
                Debug.Log("STREAM ON: clean Windows-style broadcast game explorer baked.");
            };
        }

        [MenuItem("STREAM ON/Bake Multi-Game Broadcast Integration")]
        public static void Bake()
        {
            RunnerCampaignSettings settings = AssetDatabase.LoadAssetAtPath<RunnerCampaignSettings>(SettingsPath);
            BuildPausePrefab(settings);
            BakePauseIntoScene(TileScene, settings);
            BakePauseIntoScene(PlasticScene, settings);
            BakePlasticScene(settings);
            BuildCleanExplorerPrefab();
            BakeExplorerSelection();
            EnsureBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("STREAM ON: three-game broadcast integration baked into editable scenes.");
        }

        [MenuItem("STREAM ON/Rebuild Clean Broadcast Game Explorer")]
        public static void RebuildCleanExplorer()
        {
            BuildCleanExplorerPrefab();
            BakeExplorerSelection();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildCleanExplorerPrefab()
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            GameObject root = new GameObject("Broadcast Game Explorer", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0.025f, 0.035f, 0.05f, 0.72f);

            GameObject window = CreateImage(root.transform, "Window", new Color32(248, 249, 251, 255));
            Image windowImage = window.GetComponent<Image>(); windowImage.sprite = uiSprite; windowImage.type = Image.Type.Sliced;
            RectTransform windowRect = window.GetComponent<RectTransform>();
            windowRect.anchorMin = windowRect.anchorMax = new Vector2(.5f, .5f);
            windowRect.sizeDelta = new Vector2(1120, 640); windowRect.anchoredPosition = Vector2.zero;

            GameObject titleBar = CreateImage(window.transform, "Title Bar", new Color32(245, 246, 248, 255));
            SetRect(titleBar.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -28), new Vector2(0, 56));
            TMP_Text title = CreateText(titleBar.transform, "Window Title", "방송 게임 선택", 22);
            title.color = new Color32(32, 35, 40, 255); title.alignment = TextAlignmentOptions.MidlineLeft;
            SetRect(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(30, 0), new Vector2(-60, 0));

            GameObject address = CreateImage(window.transform, "Address Bar", new Color32(255, 255, 255, 255));
            Image addressImage = address.GetComponent<Image>(); addressImage.sprite = uiSprite; addressImage.type = Image.Type.Sliced;
            SetRect(address.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -86), new Vector2(-56, 38));
            TMP_Text addressText = CreateText(address.transform, "Path", "내 PC   >   STREAM ON   >   Games", 18);
            addressText.color = new Color32(74, 78, 85, 255); addressText.alignment = TextAlignmentOptions.MidlineLeft;
            addressText.rectTransform.anchorMin = Vector2.zero; addressText.rectTransform.anchorMax = Vector2.one;
            addressText.rectTransform.offsetMin = new Vector2(16, 0); addressText.rectTransform.offsetMax = new Vector2(-16, 0);

            GameObject divider = CreateImage(window.transform, "Divider", new Color32(224, 226, 230, 255));
            SetRect(divider.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -116), new Vector2(0, 1));

            Button runner = CreateExecutableTile(window.transform, "Runner Game Button", "RUNNER.exe", "액션 러너", new Vector2(-320, -10), uiSprite);
            Button tile = CreateExecutableTile(window.transform, "Tile Arena Game Button", "TILE_ARENA.exe", "아케이드", new Vector2(0, -10), uiSprite);
            Button plastic = CreateExecutableTile(window.transform, "Plastic Knightmare Game Button", "PLASTIC_KNIGHTMARE.exe", "3D 디펜스", new Vector2(320, -10), uiSprite);

            TMP_Text footer = CreateText(window.transform, "Status Bar", "3개 항목", 17);
            footer.color = new Color32(95, 99, 106, 255); footer.alignment = TextAlignmentOptions.MidlineLeft;
            SetRect(footer.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(28, 22), new Vector2(-56, 44));

            // Stable names let RunnerRoomController and designers find the three references easily.
            runner.name = "Runner Game Button"; tile.name = "Tile Arena Game Button"; plastic.name = "Plastic Knightmare Game Button";
            PrefabUtility.SaveAsPrefabAsset(root, ExplorerPrefab);
            Object.DestroyImmediate(root);
        }

        private static Button CreateExecutableTile(Transform parent, string name, string fileName,
            string typeLabel, Vector2 position, Sprite uiSprite)
        {
            GameObject go = CreateImage(parent, name, new Color32(248, 249, 251, 0));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(250, 300); rect.anchoredPosition = position;
            Image background = go.GetComponent<Image>(); background.sprite = uiSprite; background.type = Image.Type.Sliced;
            Button button = go.AddComponent<Button>(); button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color32(248, 249, 251, 0);
            colors.highlightedColor = new Color32(222, 236, 252, 255);
            colors.pressedColor = new Color32(198, 220, 246, 255);
            colors.selectedColor = colors.highlightedColor; colors.fadeDuration = .08f;
            button.colors = colors;

            GameObject icon = CreateImage(go.transform, "Application Icon", new Color32(37, 99, 173, 255));
            Image iconImage = icon.GetComponent<Image>(); iconImage.sprite = uiSprite; iconImage.type = Image.Type.Sliced;
            RectTransform iconRect = icon.GetComponent<RectTransform>(); iconRect.anchorMin = iconRect.anchorMax = new Vector2(.5f, 1f);
            iconRect.pivot = new Vector2(.5f, 1f); iconRect.anchoredPosition = new Vector2(0, -34); iconRect.sizeDelta = new Vector2(112, 112);
            TMP_Text iconMark = CreateText(icon.transform, "Mark", ">_", 38);
            iconMark.fontStyle = FontStyles.Bold; iconMark.rectTransform.anchorMin = Vector2.zero; iconMark.rectTransform.anchorMax = Vector2.one;
            iconMark.rectTransform.offsetMin = iconMark.rectTransform.offsetMax = Vector2.zero;

            TMP_Text file = CreateText(go.transform, "File Name", fileName, 21);
            file.color = new Color32(31, 34, 39, 255); file.fontStyle = FontStyles.Bold;
            file.enableWordWrapping = false;
            file.rectTransform.anchorMin = new Vector2(0, 0); file.rectTransform.anchorMax = new Vector2(1, 0);
            file.rectTransform.pivot = new Vector2(.5f, 0); file.rectTransform.anchoredPosition = new Vector2(0, 68); file.rectTransform.sizeDelta = new Vector2(-12, 38);
            TMP_Text kind = CreateText(go.transform, "File Type", typeLabel, 17);
            kind.color = new Color32(112, 116, 123, 255);
            kind.rectTransform.anchorMin = new Vector2(0, 0); kind.rectTransform.anchorMax = new Vector2(1, 0);
            kind.rectTransform.pivot = new Vector2(.5f, 0); kind.rectTransform.anchoredPosition = new Vector2(0, 36); kind.rectTransform.sizeDelta = new Vector2(-12, 30);
            return button;
        }

        private static void BuildPausePrefab(RunnerCampaignSettings settings)
        {
            Scene source = EditorSceneManager.OpenScene(RunnerScene, OpenSceneMode.Additive);
            RunnerPauseController sourceController = source.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RunnerPauseController>(true)).FirstOrDefault();
            if (sourceController == null) { EditorSceneManager.CloseScene(source, true); return; }
            SerializedObject sourceSo = new SerializedObject(sourceController);
            GameObject sourcePause = sourceSo.FindProperty("pausePanel").objectReferenceValue as GameObject;
            GameObject sourceSettings = sourceSo.FindProperty("settingsPanel").objectReferenceValue as GameObject;
            TMP_Text sourceCountdown = sourceSo.FindProperty("countdownText").objectReferenceValue as TMP_Text;

            GameObject root = new GameObject("Shared Broadcast Pause", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(RunnerPauseController));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GameObject pause = sourcePause != null ? Object.Instantiate(sourcePause, root.transform) : CreateFallbackPanel(root.transform, "Pause Menu");
            GameObject settingsPanel = sourceSettings != null ? Object.Instantiate(sourceSettings, root.transform) : CreateFallbackPanel(root.transform, "Settings Menu");
            TMP_Text countdown = sourceCountdown != null
                ? Object.Instantiate(sourceCountdown, root.transform)
                : CreateText(root.transform, "Resume Countdown", "3", 160);
            pause.name = "Pause Menu";
            settingsPanel.name = "Settings Menu";
            countdown.name = "Resume Countdown";
            EnsurePauseButtons(pause.transform);
            EnsureSettingsUi(settingsPanel.transform);

            SerializedObject so = new SerializedObject(root.GetComponent<RunnerPauseController>());
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("pausePanel").objectReferenceValue = pause;
            so.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            so.FindProperty("countdownText").objectReferenceValue = countdown;
            so.ApplyModifiedPropertiesWithoutUndo();
            pause.SetActive(false);
            settingsPanel.SetActive(false);
            countdown.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PausePrefab);
            Object.DestroyImmediate(root);
            EditorSceneManager.CloseScene(source, true);
        }

        private static void BakePauseIntoScene(string path, RunnerCampaignSettings settings)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            foreach (RunnerPauseController existing in scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RunnerPauseController>(true)))
                Object.DestroyImmediate(existing.gameObject);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PausePrefab);
            if (prefab != null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance != null) instance.name = "Shared Broadcast Pause";
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void BakePlasticScene(RunnerCampaignSettings settings)
        {
            Scene scene = EditorSceneManager.OpenScene(PlasticScene, OpenSceneMode.Additive);
            foreach (PauseMenuController oldPause in scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PauseMenuController>(true)))
                Object.DestroyImmediate(oldPause);

            PlasticKnightmareBroadcastController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PlasticKnightmareBroadcastController>(true)).FirstOrDefault();
            if (controller == null)
            {
                GameObject go = new GameObject("STREAM ON Plastic Knightmare Broadcast");
                SceneManager.MoveGameObjectToScene(go, scene);
                controller = go.AddComponent<PlasticKnightmareBroadcastController>();
            }
            EnsurePrefab(scene, ChatPrefab, "Shared Live Chat");
            EnsurePrefab(scene, DonationPrefab, "Shared Donation Popup");
            EnsurePrefab(scene, WitPrefab, "Shared Wit Interaction");
            EnsurePrefab(scene, SettlementPrefab, "Shared Broadcast Settlement");

            TMP_Text timer = EnsureTimerHud(scene);
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("chat").objectReferenceValue = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RunnerChatController>(true)).FirstOrDefault();
            so.FindProperty("settlementView").objectReferenceValue = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RunnerBroadcastSettlementView>(true)).FirstOrDefault();
            so.FindProperty("remainingTimeText").objectReferenceValue = timer;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void BakeExplorerSelection()
        {
            Scene scene = EditorSceneManager.OpenScene(RoomScene, OpenSceneMode.Additive);
            RunnerRoomController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RunnerRoomController>(true)).FirstOrDefault();
            if (controller == null) { EditorSceneManager.CloseScene(scene, true); return; }
            SerializedObject so = new SerializedObject(controller);
            GameObject oldPanel = so.FindProperty("gameSelectionPanel").objectReferenceValue as GameObject;
            Transform parent = oldPanel != null ? oldPanel.transform.parent : controller.transform;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExplorerPrefab);
            if (prefab == null) { EditorSceneManager.CloseScene(scene, true); return; }
            GameObject panel = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            panel.transform.SetParent(parent, false);
            panel.name = "Broadcast Game Explorer";
            Button runner = panel.GetComponentsInChildren<Button>(true).First(button => button.name == "Runner Game Button");
            Button tile = panel.GetComponentsInChildren<Button>(true).First(button => button.name == "Tile Arena Game Button");
            Button plastic = panel.GetComponentsInChildren<Button>(true).First(button => button.name == "Plastic Knightmare Game Button");
            so.FindProperty("gameSelectionPanel").objectReferenceValue = panel;
            so.FindProperty("runnerGameButton").objectReferenceValue = runner;
            so.FindProperty("tileArenaGameButton").objectReferenceValue = tile;
            so.FindProperty("plasticKnightmareGameButton").objectReferenceValue = plastic;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (oldPanel != null) Object.DestroyImmediate(oldPanel);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void EnsureExplorerChrome(Transform panel)
        {
            Transform title = panel.Find("Explorer Title Bar") ?? CreateImage(panel, "Explorer Title Bar", new Color32(25, 25, 25, 255)).transform;
            SetRect(title.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -36), new Vector2(0, 72));
            TMP_Text titleText = title.GetComponentInChildren<TMP_Text>(true) ?? CreateText(title, "Title", "내 게임 > 방송용 게임", 25);
            titleText.text = "내 게임  >  방송용 게임";
            Transform address = panel.Find("Explorer Address Bar") ?? CreateImage(panel, "Explorer Address Bar", new Color32(225, 225, 225, 255)).transform;
            SetRect(address.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -108), new Vector2(-40, 46));
            TMP_Text path = address.GetComponentInChildren<TMP_Text>(true) ?? CreateText(address, "Path", "C:\\STREAM_ON\\Games\\", 21);
            path.text = "C:\\STREAM_ON\\Games\\";
        }

        private static void StyleExecutable(Button button, Vector2 position, string fileName, string description)
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(290, 360);
            Image image = button.GetComponent<Image>();
            if (image != null) image.color = new Color32(224, 235, 247, 255);
            TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
            TMP_Text label = labels.FirstOrDefault() ?? CreateText(button.transform, "File Name", fileName, 25);
            label.text = $"<b>{fileName}</b>\n<size=65%>{description}</size>\n\n<size=60%>응용 프로그램</size>";
            label.alignment = TextAlignmentOptions.Center;
            RectTransform textRect = label.rectTransform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14, 16); textRect.offsetMax = new Vector2(-14, -16);
        }

        private static void EnsurePauseButtons(Transform panel)
        {
            string[] names = { "Continue Button", "Settings Button", "Manual Save Button", "Main Menu Button" };
            string[] labels = { "게임 재개", "설정", "수동 저장", "종료하기\n<size=55%>게임 선택 화면으로 돌아가기</size>" };
            for (int i = 0; i < names.Length; i++)
            {
                Button button = panel.GetComponentsInChildren<Button>(true).FirstOrDefault(item => item.name == names[i]);
                if (button == null) button = CreateButton(panel, names[i], labels[i], new Vector2(0, 150 - i * 110));
                button.name = names[i];
                TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
                if (text != null) text.text = labels[i];
            }
        }

        private static void EnsureSettingsUi(Transform panel)
        {
            if (panel.GetComponentInChildren<Slider>(true) == null)
            {
                GameObject sliderGo = new GameObject("Volume Slider", typeof(RectTransform), typeof(Slider));
                sliderGo.transform.SetParent(panel, false);
                sliderGo.GetComponent<RectTransform>().sizeDelta = new Vector2(500, 55);
            }
            if (panel.GetComponentsInChildren<TMP_Text>(true).All(t => t.name != "Volume Label"))
                CreateText(panel, "Volume Label", "전체 음량", 30);
            if (panel.GetComponentsInChildren<Button>(true).All(b => b.name != "AI Toggle Button"))
                CreateButton(panel, "AI Toggle Button", "AI 채팅", new Vector2(0, -40));
            if (panel.GetComponentsInChildren<Button>(true).All(b => b.name != "Back Button"))
                CreateButton(panel, "Back Button", "뒤로", new Vector2(0, -180));
        }

        private static GameObject CreateFallbackPanel(Transform parent, string name)
        {
            GameObject panel = CreateImage(parent, name, new Color(0, 0, 0, .82f));
            SetRect(panel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return panel;
        }

        private static TMP_Text EnsureTimerHud(Scene scene)
        {
            TMP_Text existing = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true))
                .FirstOrDefault(text => text.name == "Shared Broadcast Time");
            if (existing != null) return existing;
            GameObject root = new GameObject("Shared Broadcast HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(root, scene);
            Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 25000;
            TMP_Text text = CreateText(root.transform, "Shared Broadcast Time", "방송 00:00", 32);
            RectTransform rect = text.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f); rect.anchoredPosition = new Vector2(0, -25); rect.sizeDelta = new Vector2(500, 60);
            return text;
        }

        private static void EnsurePrefab(Scene scene, string path, string instanceName)
        {
            if (scene.GetRootGameObjects().Any(root => root.name == instanceName)) return;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance != null) instance.name = instanceName;
        }

        private static GameObject CreateImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false); go.GetComponent<Image>().color = color; return go;
        }

        private static TMP_Text CreateText(Transform parent, string name, string value, float size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.text = value; text.fontSize = size; text.alignment = TextAlignmentOptions.Center; text.color = Color.white;
            string[] fonts = AssetDatabase.FindAssets("Galmuri14 SDF t:TMP_FontAsset");
            if (fonts.Length > 0) text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(fonts[0]));
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position)
        {
            GameObject go = CreateImage(parent, name, new Color32(48, 94, 145, 255));
            RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = new Vector2(460, 85);
            Button button = go.AddComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
            TMP_Text text = CreateText(go.transform, "Label", label, 28);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        { rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; }

        private static void EnsureBuildScenes()
        {
            string[] required = { RoomScene, RunnerScene, TileScene, "Assets/Scenes/MainMenu.unity", PlasticScene };
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (string path in required)
                if (!scenes.Any(scene => scene.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
