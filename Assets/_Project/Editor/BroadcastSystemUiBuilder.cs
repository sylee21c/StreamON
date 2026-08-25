using System.Linq;
using StreamOn.Minigames.Runner;
using StreamOn.Minigames.TileArena;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StreamOn.Editor
{
    [InitializeOnLoad]
    public static class BroadcastSystemUiBuilder
    {
        private const string Folder = "Assets/_Project/Prefabs";
        private const string SettlementPath = Folder + "/SharedBroadcastSettlement.prefab";
        private const string ShopPath = Folder + "/RoomEquipmentShop.prefab";
        private const string DashboardPath = "Assets/Prefabs/Growth And Leaderboard UI.prefab";
        private const string PausePath = Folder + "/SharedBroadcastPause.prefab";
        private const string ExplorerPath = Folder + "/BroadcastGameExplorer.prefab";
        private const string SettingsPath = "Assets/_Project/Settings/RunnerCampaignSettings.asset";
        private const string RunnerScene = "Assets/Scenes/BroadcastRunner.unity";
        private const string TileScene = "Assets/Scenes/TileArena.unity";
        private const string PlasticScene = "Assets/Scenes/MainScene.unity";
        private const string RoomScene = "Assets/Scenes/StreamerRoom.unity";

        static BroadcastSystemUiBuilder() => EditorApplication.delayCall += Refresh;

        [MenuItem("STREAM ON/Shared UI/Refresh Broadcast Systems")]
        public static void Refresh()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            RunnerCampaignSettings settings = AssetDatabase.LoadAssetAtPath<RunnerCampaignSettings>(SettingsPath);
            if (settings == null) return;
            GameObject settlement = EnsureSettlementPrefab(settings);
            GameObject shop = EnsureShopPrefab(settings);
            EnsureDashboardShopButton();
            EnsurePauseAudioSliders();
            EnsureGameLaunchConfirmation();
            EnsureExplorerCloseButton();
            PlaceSettlement(RunnerScene, settlement);
            PlaceSettlement(TileScene, settlement);
            PlaceSettlement(PlasticScene, settlement);
            UpgradeTile(settings);
            UpgradeRoom(shop);
            EnsureRoomAudioController();
            AssetDatabase.SaveAssets();
        }

        private static GameObject EnsureSettlementPrefab(RunnerCampaignSettings settings)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SettlementPath);
            if (existing != null)
            {
                UpgradeSettlementPrefab(settings);
                return AssetDatabase.LoadAssetAtPath<GameObject>(SettlementPath);
            }
            TMP_FontAsset font = SettlementFont(); Sprite sprite = PanelSprite();
            GameObject root = Panel("Broadcast Settlement Dashboard", new Vector2(680, 530), new Color(.025f, .035f, .06f, .985f), sprite);
            root.AddComponent<CanvasGroup>();
            TMP_Text title = Text("Title", root.transform, "방송 결과", font, 31, new Vector2(610, 52), new Vector2(0, 215));
            TMP_Text game = ResultCard("Game Result", root.transform, "최고 점수", font, new Vector2(0, 130), sprite);
            TMP_Text audience = ResultCard("Audience Result", root.transform, "최고 시청자 수  0\n총 시청자 수  0", font, new Vector2(0, 30), sprite);
            TMP_Text rating = ResultCard("Rating Result", root.transform, "방송 평점", font, new Vector2(0, -70), sprite);
            TMP_Text growth = ResultCard("Growth Result", root.transform, "성장 및 수익", font, new Vector2(0, -170), sprite);
            Button next = Button("Continue Button", root.transform, "다음 날", font, new Vector2(260, 52), new Vector2(0, -236), new Color(.16f, .68f, .58f), sprite, out TMP_Text nextLabel);
            RunnerBroadcastSettlementView view = root.AddComponent<RunnerBroadcastSettlementView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            serialized.FindProperty("titleText").objectReferenceValue = title;
            serialized.FindProperty("gameResultText").objectReferenceValue = game;
            serialized.FindProperty("audienceText").objectReferenceValue = audience;
            serialized.FindProperty("ratingText").objectReferenceValue = rating;
            serialized.FindProperty("growthText").objectReferenceValue = growth;
            serialized.FindProperty("continueButton").objectReferenceValue = next;
            serialized.FindProperty("continueLabel").objectReferenceValue = nextLabel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Save(root, SettlementPath);
            UpgradeSettlementPrefab(settings);
            return AssetDatabase.LoadAssetAtPath<GameObject>(SettlementPath);
        }

        private static void UpgradeSettlementPrefab(RunnerCampaignSettings settings)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(SettlementPath);
            try
            {
                TMP_FontAsset settlementFont = SettlementFont();
                if (settlementFont != null)
                    foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                        text.font = settlementFont;

                RectTransform rootRect = root.GetComponent<RectTransform>();
                // Scene instances historically use 680x530. Keep the authored prefab at that
                // exact size and fit every child inside it so no scene override can crop a card.
                rootRect.sizeDelta = new Vector2(680f, 530f);

                TMP_Text title = root.transform.Find("Title")?.GetComponent<TMP_Text>();
                SetRect(title?.rectTransform, new Vector2(600f, 40f), new Vector2(0f, 235f));

                TMP_Text game = ConfigureSettlementCard(root.transform, "Game Result", new Vector2(620f, 100f),
                    new Vector2(0f, 160f), new Vector2(574f, 88f), Vector2.zero);
                TMP_Text audience = ConfigureSettlementCard(root.transform, "Audience Result", new Vector2(620f, 68f),
                    new Vector2(0f, 75f), new Vector2(574f, 62f), Vector2.zero);
                if (audience != null) audience.text = "최고 시청자 수  0\n총 시청자 수  0";
                TMP_Text growth = ConfigureSettlementCard(root.transform, "Growth Result", new Vector2(620f, 135f),
                    new Vector2(0f, -30f), new Vector2(574f, 118f), new Vector2(0f, 5f));
                TMP_Text rating = ConfigureSettlementCard(root.transform, "Rating Result", new Vector2(620f, 75f),
                    new Vector2(0f, -137f), new Vector2(574f, 62f), new Vector2(0f, 7f));

                RectTransform continueRect = root.transform.Find("Continue Button")?.GetComponent<RectTransform>();
                SetRect(continueRect, new Vector2(260f, 44f), new Vector2(0f, -225f));

                Image experienceFill = EnsureGauge(growth.transform.parent, "Experience Gauge",
                    new Vector2(118f, -48f), new Vector2(310f, 16f));
                Image ratingFill = EnsureGauge(rating.transform.parent, "Rating Gauge",
                    new Vector2(0f, -23f), new Vector2(340f, 16f));

                RunnerBroadcastSettlementView view = root.GetComponent<RunnerBroadcastSettlementView>();
                SerializedObject serialized = new SerializedObject(view);
                serialized.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
                serialized.FindProperty("titleText").objectReferenceValue = title;
                serialized.FindProperty("gameResultText").objectReferenceValue = game;
                serialized.FindProperty("audienceText").objectReferenceValue = audience;
                serialized.FindProperty("growthText").objectReferenceValue = growth;
                serialized.FindProperty("ratingText").objectReferenceValue = rating;
                serialized.FindProperty("experienceFill").objectReferenceValue = experienceFill;
                serialized.FindProperty("ratingFill").objectReferenceValue = ratingFill;
                serialized.FindProperty("campaignSettings").objectReferenceValue = settings;
                serialized.FindProperty("continueButton").objectReferenceValue = root.transform.Find("Continue Button")?.GetComponent<Button>();
                serialized.FindProperty("continueLabel").objectReferenceValue = root.transform.Find("Continue Button/Label")?.GetComponent<TMP_Text>();
                serialized.FindProperty("sectionRevealDelay").floatValue = 0.24f;
                serialized.FindProperty("labelRevealDelay").floatValue = 0.10f;
                serialized.FindProperty("numberCountDuration").floatValue = 0.48f;
                serialized.FindProperty("gaugeFillDuration").floatValue = 0.90f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, SettlementPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static TMP_Text ConfigureSettlementCard(Transform root, string name, Vector2 cardSize,
            Vector2 cardPosition, Vector2 textSize, Vector2 textPosition)
        {
            RectTransform card = root.Find(name)?.GetComponent<RectTransform>();
            SetRect(card, cardSize, cardPosition);
            TMP_Text text = card != null ? card.Find("Text")?.GetComponent<TMP_Text>() : null;
            if (text != null)
            {
                SetRect(text.rectTransform, textSize, textPosition);
                text.fontSize = 18f;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.richText = true;
                text.textWrappingMode = TextWrappingModes.NoWrap;
            }
            return text;
        }

        private static Image EnsureGauge(Transform parent, string name, Vector2 position, Vector2 size)
        {
            Transform existing = parent.Find(name);
            GameObject background = existing != null ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            if (existing == null) background.transform.SetParent(parent, false);
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            SetRect(backgroundRect, size, position);
            Image backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color(0.025f, 0.035f, 0.055f, 1f);
            backgroundImage.raycastTarget = false;

            Transform fillTransform = background.transform.Find("Fill");
            GameObject fillObject = fillTransform != null ? fillTransform.gameObject
                : new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            if (fillTransform == null) fillObject.transform.SetParent(background.transform, false);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.65f, 1f);
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            Image fill = fillObject.GetComponent<Image>();
            // Match StatusUI/Streamer Level Fill exactly.
            fill.color = new Color(0.05f, 0.82f, 0.48f, 1f);
            fill.raycastTarget = false;
            return fill;
        }

        private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static GameObject EnsureShopPrefab(RunnerCampaignSettings settings)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPath);
            if (existing != null) return existing;
            TMP_FontAsset font = Font(); Sprite sprite = PanelSprite();
            GameObject root = new GameObject("Equipment Shop UI", typeof(RectTransform));
            Button open = Button("Open Shop Button", root.transform, "장비 업그레이드", font, new Vector2(220, 48), new Vector2(0, 0), new Color(.16f, .55f, .62f), sprite, out _);
            RectTransform openRect = open.GetComponent<RectTransform>(); openRect.anchorMin = openRect.anchorMax = openRect.pivot = new Vector2(1, 1); openRect.anchoredPosition = new Vector2(-28, -28);
            GameObject panel = Panel("Equipment Shop Panel", new Vector2(650, 500), new Color(.025f, .035f, .06f, .985f), sprite);
            panel.transform.SetParent(root.transform, false);
            Text("Title", panel.transform, "방송 장비 업그레이드", font, 30, new Vector2(580, 50), new Vector2(0, 205));
            TMP_Text cash = Text("Cash", panel.transform, "보유금 0원", font, 21, new Vector2(560, 38), new Vector2(0, 160));
            TMP_Text[] labels = new TMP_Text[4]; Button[] buttons = new Button[4];
            string[] names = { "PC", "마이크", "집중 장비", "방 인테리어" };
            for (int i = 0; i < 4; i++)
            {
                float y = 105 - i * 78;
                labels[i] = Text(names[i], panel.transform, names[i] + "  Lv.1", font, 17, new Vector2(390, 60), new Vector2(-80, y));
                labels[i].alignment = TextAlignmentOptions.MidlineLeft;
                buttons[i] = Button(names[i] + " Upgrade", panel.transform, "업그레이드", font, new Vector2(150, 48), new Vector2(220, y), new Color(.18f, .65f, .54f), sprite, out _);
            }
            TMP_Text notice = Text("Notice", panel.transform, string.Empty, font, 16, new Vector2(560, 30), new Vector2(0, -184));
            notice.color = new Color(1f, .82f, .35f);
            Button close = Button("Close", panel.transform, "닫기", font, new Vector2(160, 46), new Vector2(0, -220), new Color(.25f, .30f, .40f), sprite, out _);
            RunnerEquipmentShopController shop = root.AddComponent<RunnerEquipmentShopController>();
            SerializedObject serialized = new SerializedObject(shop);
            serialized.FindProperty("settings").objectReferenceValue = settings;
            serialized.FindProperty("shopPanel").objectReferenceValue = panel;
            serialized.FindProperty("openButton").objectReferenceValue = open;
            serialized.FindProperty("closeButton").objectReferenceValue = close;
            serialized.FindProperty("cashText").objectReferenceValue = cash;
            serialized.FindProperty("pcButton").objectReferenceValue = buttons[0]; serialized.FindProperty("pcText").objectReferenceValue = labels[0];
            serialized.FindProperty("microphoneButton").objectReferenceValue = buttons[1]; serialized.FindProperty("microphoneText").objectReferenceValue = labels[1];
            serialized.FindProperty("fitnessButton").objectReferenceValue = buttons[2]; serialized.FindProperty("fitnessText").objectReferenceValue = labels[2];
            serialized.FindProperty("interiorButton").objectReferenceValue = buttons[3]; serialized.FindProperty("interiorText").objectReferenceValue = labels[3];
            serialized.FindProperty("noticeText").objectReferenceValue = notice;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return Save(root, ShopPath);
        }

        private static void EnsureDashboardShopButton()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(DashboardPath);
            if (asset == null) return;
            GameObject root = PrefabUtility.LoadPrefabContents(DashboardPath);
            try
            {
                Transform dashboard = root.transform.Find("Dashboard");
                if (dashboard == null || dashboard.Find("Shop") != null) return;
                Button close = dashboard.Find("Close")?.GetComponent<Button>();
                TMP_FontAsset font = close != null
                    ? close.GetComponentInChildren<TMP_Text>(true)?.font
                    : root.GetComponentInChildren<TMP_Text>(true)?.font;
                Button shop = Button("Shop", dashboard, "상점", font, new Vector2(120f, 40f),
                    new Vector2(-445f, 305f), new Color(.32f, .34f, .42f), PanelSprite(), out _);
                if (close != null)
                {
                    Image closeImage = close.GetComponent<Image>();
                    Image shopImage = shop.GetComponent<Image>();
                    if (closeImage != null && shopImage != null)
                    {
                        shopImage.sprite = closeImage.sprite;
                        shopImage.type = closeImage.type;
                        shopImage.color = closeImage.color;
                    }
                    shop.colors = close.colors;
                }
                PrefabUtility.SaveAsPrefabAsset(root, DashboardPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsurePauseAudioSliders()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PausePath) == null) return;
            GameObject root = PrefabUtility.LoadPrefabContents(PausePath);
            try
            {
                Transform settings = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == "Settings Menu");
                Slider master = settings?.GetComponentsInChildren<Slider>(true).FirstOrDefault(item => item.name == "Master Volume");
                TMP_Text masterLabel = settings?.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(item => item.name == "Volume Label");
                if (settings == null || master == null || masterLabel == null) return;
                EnsureAudioSlider(settings, master, masterLabel, "BGM Volume", "BGM Volume Label", "BGM", -65f);
                EnsureAudioSlider(settings, master, masterLabel, "SFX Volume", "SFX Volume Label", "SFX", -130f);
                PrefabUtility.SaveAsPrefabAsset(root, PausePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void EnsureGameLaunchConfirmation()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ExplorerPath) == null) return;
            GameObject root = PrefabUtility.LoadPrefabContents(ExplorerPath);
            try
            {
                if (root.transform.Find("Launch Confirmation") != null) return;
                TMP_FontAsset font = root.GetComponentInChildren<TMP_Text>(true)?.font;
                Sprite sprite = PanelSprite();
                GameObject panel = Panel("Launch Confirmation", new Vector2(480f, 230f),
                    new Color(.055f, .065f, .085f, 1f), sprite);
                panel.transform.SetParent(root.transform, false);
                RectTransform rect = panel.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                Text("Dialog Title", panel.transform, "Broadcast Game Explorer", font, 18f,
                    new Vector2(430f, 36f), new Vector2(0f, 82f));
                TMP_Text message = Text("Message", panel.transform, "TileArena.exe를 실행하시겠습니까?", font, 21f,
                    new Vector2(420f, 70f), new Vector2(0f, 22f));
                message.alignment = TextAlignmentOptions.Center;
                Button("Confirm Launch", panel.transform, "확인", font, new Vector2(150f, 42f),
                    new Vector2(-88f, -70f), new Color(.18f, .55f, .62f), sprite, out _);
                Button("Cancel Launch", panel.transform, "취소", font, new Vector2(150f, 42f),
                    new Vector2(88f, -70f), new Color(.30f, .32f, .38f), sprite, out _);
                panel.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, ExplorerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void EnsureExplorerCloseButton()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ExplorerPath) == null) return;
            GameObject root = PrefabUtility.LoadPrefabContents(ExplorerPath);
            try
            {
                Transform titleBar = root.transform.Find("Window/Title Bar");
                if (titleBar == null || titleBar.Find("Close Button") != null) return;
                TMP_FontAsset font = root.GetComponentInChildren<TMP_Text>(true)?.font;
                Button close = Button("Close Button", titleBar, "X", font, new Vector2(44f, 38f),
                    Vector2.zero, new Color32(245, 246, 248, 255), PanelSprite(), out TMP_Text label);
                RectTransform rect = close.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(1f, .5f);
                rect.pivot = new Vector2(1f, .5f);
                rect.anchoredPosition = new Vector2(-8f, 0f);
                label.fontStyle = FontStyles.Bold;
                label.color = new Color32(32, 35, 40, 255);
                ColorBlock colors = close.colors;
                colors.highlightedColor = new Color32(232, 17, 35, 255);
                colors.pressedColor = new Color32(196, 15, 28, 255);
                close.colors = colors;
                PrefabUtility.SaveAsPrefabAsset(root, ExplorerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void EnsureAudioSlider(Transform parent, Slider template, TMP_Text labelTemplate,
            string sliderName, string labelName, string label, float yOffset)
        {
            if (parent.Find(sliderName) == null)
            {
                Slider slider = Object.Instantiate(template, parent);
                slider.name = sliderName;
                slider.GetComponent<RectTransform>().anchoredPosition += new Vector2(0f, yOffset);
            }
            if (parent.Find(labelName) == null)
            {
                TMP_Text text = Object.Instantiate(labelTemplate, parent);
                text.name = labelName;
                text.text = label + "  100%";
                text.rectTransform.anchoredPosition += new Vector2(0f, yOffset);
            }
        }

        private static void EnsureRoomAudioController()
        {
            Scene scene = Open(RoomScene, out bool opened);
            if (!scene.IsValid()) return;
            RunnerRoomAudioController audio = Find<RunnerRoomAudioController>(scene);
            if (audio == null)
            {
                GameObject obj = new GameObject("Streamer Room Audio", typeof(AudioSource), typeof(AudioSource),
                    typeof(AudioSource), typeof(RunnerRoomAudioController));
                SceneManager.MoveGameObjectToScene(obj, scene);
                audio = obj.GetComponent<RunnerRoomAudioController>();
                SerializedObject serialized = new SerializedObject(audio);
                AudioSource[] sources = obj.GetComponents<AudioSource>();
                serialized.FindProperty("backgroundMusicSource").objectReferenceValue = sources[0];
                serialized.FindProperty("effectsSource").objectReferenceValue = sources[1];
                serialized.FindProperty("footstepSource").objectReferenceValue = sources[2];
                serialized.FindProperty("player").objectReferenceValue = Find<RunnerRoomPlayerController>(scene);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
            }
            SaveClose(scene, opened);
        }

        private static void PlaceSettlement(string path, GameObject prefab)
        {
            Scene scene = Open(path, out bool opened); if (!scene.IsValid()) return;
            Canvas canvas = Find<Canvas>(scene); RunnerBroadcastSettlementView view = Find<RunnerBroadcastSettlementView>(scene);
            if (canvas != null && view == null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                instance.transform.SetParent(canvas.transform, false);
                RectTransform rect = instance.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = new Vector2(-150, 0); rect.SetAsLastSibling();
                EditorSceneManager.MarkSceneDirty(scene);
            }
            else if (canvas != null && view != null)
            {
                GameObject instance = PrefabUtility.GetNearestPrefabInstanceRoot(view.gameObject);
                if (instance != null && PrefabUtility.IsPartOfPrefabInstance(instance)
                    && PrefabUtility.GetCorrespondingObjectFromSource(instance) == prefab)
                {
                    PrefabUtility.RevertPrefabInstance(instance, InteractionMode.AutomatedAction);
                    instance.transform.SetParent(canvas.transform, false);
                    RectTransform rect = instance.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(680f, 530f);
                    rect.localScale = Vector3.one;
                    rect.SetAsLastSibling();
                    instance.SetActive(false);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }
            SaveClose(scene, opened);
        }

        private static void UpgradeTile(RunnerCampaignSettings settings)
        {
            Scene scene = Open(TileScene, out bool opened); if (!scene.IsValid()) return;
            TileArenaController game = Find<TileArenaController>(scene);
            if (game == null) { SaveClose(scene, opened); return; }
            TileArenaBroadcastSessionController session = game.GetComponent<TileArenaBroadcastSessionController>();
            if (session == null) session = game.gameObject.AddComponent<TileArenaBroadcastSessionController>();

            // This HUD is obsolete. Older versions searched for the spaced name while the
            // saved prefab instances were named without spaces, so every refresh added one more.
            GameObject[] legacyHuds = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(item => item.name == "TileBroadcastSessionHUD" || item.name == "Tile Broadcast Session HUD")
                .Select(item => PrefabUtility.GetNearestPrefabInstanceRoot(item.gameObject) ?? item.gameObject)
                .Distinct()
                .ToArray();
            foreach (GameObject legacyHud in legacyHuds)
            {
                Object.DestroyImmediate(legacyHud);
            }

            SerializedObject serialized = new SerializedObject(session);
            serialized.FindProperty("gameController").objectReferenceValue = game;
            serialized.FindProperty("audience").objectReferenceValue = game.GetComponent<TileArenaChatAdapter>();
            serialized.FindProperty("settings").objectReferenceValue = settings;
            serialized.FindProperty("settlementView").objectReferenceValue = Find<RunnerBroadcastSettlementView>(scene);
            serialized.FindProperty("remainingTimeText").objectReferenceValue = null;
            serialized.FindProperty("attemptText").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject gameSerialized = new SerializedObject(game);
            gameSerialized.FindProperty("broadcastSession").objectReferenceValue = session;
            gameSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); SaveClose(scene, opened);
        }

        private static void UpgradeRoom(GameObject prefab)
        {
            Scene scene = Open(RoomScene, out bool opened); if (!scene.IsValid()) return;
            Canvas canvas = Find<Canvas>(scene);
            if (canvas != null && Find<RunnerEquipmentShopController>(scene) == null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject; instance.transform.SetParent(canvas.transform, false);
                RectTransform rect = instance.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; rect.SetAsLastSibling();
                EditorSceneManager.MarkSceneDirty(scene);
            }
            SaveClose(scene, opened);
        }

        private static TMP_Text ResultCard(string name, Transform parent, string value, TMP_FontAsset font, Vector2 position, Sprite sprite)
        {
            GameObject card = Panel(name, new Vector2(590, 88), new Color(.07f, .09f, .14f, .95f), sprite); card.transform.SetParent(parent, false); card.GetComponent<RectTransform>().anchoredPosition = position;
            return Text("Text", card.transform, value, font, 17, new Vector2(550, 76), Vector2.zero);
        }

        private static GameObject Panel(string name, Vector2 size, Color color, Sprite sprite)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); obj.GetComponent<RectTransform>().sizeDelta = size;
            Image image = obj.GetComponent<Image>(); image.sprite = sprite; image.type = Image.Type.Sliced; image.color = color; return obj;
        }

        private static TMP_Text Text(string name, Transform parent, string value, TMP_FontAsset font, float size, Vector2 dimensions, Vector2 position)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); obj.transform.SetParent(parent, false);
            TMP_Text text = obj.GetComponent<TMP_Text>(); text.text = value; text.font = font; text.fontSize = size; text.color = Color.white; text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.Normal;
            RectTransform rect = text.rectTransform; rect.sizeDelta = dimensions; rect.anchoredPosition = position; return text;
        }

        private static Button Button(string name, Transform parent, string label, TMP_FontAsset font, Vector2 size, Vector2 position, Color color, Sprite sprite, out TMP_Text text)
        {
            GameObject obj = Panel(name, size, color, sprite); obj.transform.SetParent(parent, false); obj.GetComponent<RectTransform>().anchoredPosition = position;
            Button button = obj.AddComponent<Button>(); text = Text("Label", obj.transform, label, font, 17, size - new Vector2(12, 8), Vector2.zero); return button;
        }

        private static GameObject Save(GameObject root, string path) { GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root); return saved; }
        private static Sprite PanelSprite() => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        private static TMP_FontAsset Font()
        {
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Examples & Extras/Fonts/Galmuri14 SDF.asset");
        }
        private static TMP_FontAsset SettlementFont()
        {
            TMP_FontAsset galmuri = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Examples & Extras/Fonts/Galmuri14 SDF.asset");
            return galmuri != null ? galmuri : Font();
        }
        private static Scene Open(string path, out bool opened) { Scene scene = SceneManager.GetSceneByPath(path); opened = !scene.IsValid() || !scene.isLoaded; return opened && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : scene; }
        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();
        private static void SaveClose(Scene scene, bool opened) { if (scene.IsValid() && scene.isLoaded && scene.isDirty) EditorSceneManager.SaveScene(scene); if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
    }
}
