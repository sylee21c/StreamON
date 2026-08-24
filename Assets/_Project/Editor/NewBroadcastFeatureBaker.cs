using System.Linq;
using StreamOn.Minigames.Runner;
using StreamOn.Minigames.TileArena;
using StreamOn.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StreamOn.Editor
{
    public static class NewBroadcastFeatureBaker
    {
        private const string SettingsPath = "Assets/_Project/Settings/RunnerCampaignSettings.asset";
        private static readonly string[] GameScenes =
        {
            "Assets/Scenes/BroadcastRunner.unity",
            "Assets/Scenes/TileArena.unity",
            "Assets/Scenes/MainScene.unity"
        };

        [MenuItem("STREAM ON/UI/Bake New Broadcast Features")]
        public static void Bake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            RunnerCampaignSettings settings = AssetDatabase.LoadAssetAtPath<RunnerCampaignSettings>(SettingsPath);
            if (settings == null) return;
            EditorUtility.SetDirty(settings);
            if (settings.broadcastGrowthSettings != null) EditorUtility.SetDirty(settings.broadcastGrowthSettings);
            foreach (string path in GameScenes) BakeGameScene(path, settings);
            BakeMainMenu();
            BakeRoom(settings);
            AssetDatabase.SaveAssets();
        }

        private static void BakeGameScene(string path, RunnerCampaignSettings settings)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            try
            {
                Canvas canvas = Find<Canvas>(scene);
                if (canvas == null) return;
                TMP_FontAsset font = canvas.GetComponentsInChildren<TMP_Text>(true).Select(text => text.font).FirstOrDefault(value => value != null);
                if (path.EndsWith("MainScene.unity")) BakePlasticSceneUi(scene, canvas, font);
                Transform old = canvas.transform.Find("Broadcast Heat Scene UI");
                if (old != null)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    return;
                }
                GameObject root = Panel("Broadcast Heat Scene UI", canvas.transform, new Vector2(360, 116), new Vector2(0, -18), new Color(.025f, .035f, .06f, .92f));
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(.5f, 1f);
                TMP_Text heatLabel = Text("Heat Label", root.transform, "방송 열기 50%", font, 17, new Vector2(330, 24), new Vector2(0, -16));
                Image heatFill = Bar(root.transform, "Heat", new Vector2(320, 14), new Vector2(0, -42), Color.white);
                TMP_Text focusLabel = Text("Focus Label", root.transform, "집중력 100  TAB", font, 15, new Vector2(330, 22), new Vector2(0, -65));
                Image focusFill = Bar(root.transform, "Focus", new Vector2(320, 12), new Vector2(0, -89), new Color(.35f, .76f, 1f));
                TMP_Text alert = Text("Time Bonus Alert", root.transform, string.Empty, font, 19, new Vector2(500, 34), new Vector2(0, -126));
                alert.color = new Color(.35f, 1f, .68f);
                alert.gameObject.SetActive(false);
                RunnerBroadcastHeatGauge gauge = root.AddComponent<RunnerBroadcastHeatGauge>();
                gauge.settings = settings;
                gauge.heatFill = heatFill.rectTransform;
                gauge.heatFillImage = heatFill;
                gauge.heatLabel = heatLabel;
                gauge.focusFill = focusFill.rectTransform;
                gauge.focusFillImage = focusFill;
                gauge.focusLabel = focusLabel;
                BuildBanTooltip(canvas.transform, font);

                RunnerHUD runnerHud = Find<RunnerHUD>(scene);
                if (runnerHud != null) runnerHud.timeBonusText = alert;
                TileArenaBroadcastSessionController tile = Find<TileArenaBroadcastSessionController>(scene);
                if (tile != null) SetObject(tile, "timeBonusText", alert);
                PlasticKnightmareBroadcastController plastic = Find<PlasticKnightmareBroadcastController>(scene);
                if (plastic != null)
                {
                    plastic.settings = settings;
                    plastic.growthSettings = settings.broadcastGrowthSettings;
                    plastic.scoreText = Text("Plastic Score", root.transform, "게임 점수 0", font, 16, new Vector2(220, 24), new Vector2(-250, -20));
                    plastic.nightText = Text("Plastic Night", root.transform, "NIGHT 1", font, 18, new Vector2(180, 24), new Vector2(-250, -48));
                    if (plastic.phaseTimeText == null)
                        plastic.phaseTimeText = Text("Plastic Phase Time", root.transform, "낮 정비 01:00", font, 17, new Vector2(220, 26), new Vector2(-250, -76));
                    plastic.startNightButton = MakeButton("Start Night Early", root.transform, "정비 완료 / 밤 시작", font,
                        new Vector2(190, 34), new Vector2(-250, -110), new Color(.42f, .28f, .54f));
                }
                foreach (RunnerWitInteractionController wit in All<RunnerWitInteractionController>(scene))
                    SetObject(wit, "campaignSettings", settings);
                foreach (RunnerChatController chat in All<RunnerChatController>(scene))
                {
                    SetObject(chat, "campaignSettings", settings);
                    TMP_Text manager = Text("Manager Status", root.transform, "매니저 없음", font, 14, new Vector2(250, 22), new Vector2(250, -98));
                    SetObject(chat, "managerStatusText", manager);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void BakePlasticSceneUi(Scene scene, Canvas canvas, TMP_FontAsset font)
        {
            DayOneTutorialHints tutorial = Find<DayOneTutorialHints>(scene);
            if (tutorial != null && canvas.transform.Find("Tutorial Hint Scene UI") == null)
            {
                GameObject tutorialPanel = Panel("Tutorial Hint Scene UI", canvas.transform, new Vector2(780, 300), new Vector2(0, -95), new Color(0, 0, 0, .52f));
                RectTransform tutorialRect = tutorialPanel.GetComponent<RectTransform>();
                tutorialRect.anchorMin = tutorialRect.anchorMax = tutorialRect.pivot = new Vector2(.5f, 1f);
                Outline outline = tutorialPanel.AddComponent<Outline>();
                CanvasGroup group = tutorialPanel.AddComponent<CanvasGroup>();
                TMP_Text tutorialText = Text("Tutorial Hint Text", tutorialPanel.transform, string.Empty, font, 40,
                    new Vector2(696, 244), Vector2.zero);
                SetObject(tutorial, "canvasGroup", group);
                SetObject(tutorial, "panelRect", tutorialRect);
                SetObject(tutorial, "panelImage", tutorialPanel.GetComponent<Image>());
                SetObject(tutorial, "panelOutline", outline);
                SetObject(tutorial, "hintText", tutorialText);
            }
            BedHealthUI bedHealth = Find<BedHealthUI>(scene);
            if (bedHealth != null && canvas.transform.Find("Bed Health Scene UI") == null)
            {
                GameObject root = Panel("Bed Health Scene UI", canvas.transform, new Vector2(468, 62), new Vector2(0, 96), new Color(.05f, .06f, .08f, .88f));
                RectTransform rect = root.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0f);
                TMP_Text label = Text("Label", root.transform, "침대 HP", font, 18, new Vector2(440, 24), new Vector2(0, 17));
                Image fill = Bar(root.transform, "Bed Health", new Vector2(440, 24), new Vector2(0, -14), new Color(.35f, .9f, .4f));
                TMP_Text value = Text("Value", fill.transform.parent, "100%", font, 14, new Vector2(436, 22), Vector2.zero);
                SetObject(bedHealth, "displayRoot", root);
                SetObject(bedHealth, "fillImage", fill);
                SetObject(bedHealth, "valueText", value);
                root.SetActive(false);
            }

            GameOverUIController gameOver = Find<GameOverUIController>(scene);
            if (gameOver != null)
            {
                SerializedObject serialized = new SerializedObject(gameOver);
                SerializedProperty background = serialized.FindProperty("backgroundImage");
                if (background.objectReferenceValue == null)
                {
                    CanvasGroup authoredRoot = serialized.FindProperty("rootGroup").objectReferenceValue as CanvasGroup;
                    Transform parent = authoredRoot != null ? authoredRoot.transform : canvas.transform;
                    GameObject blackout = Panel("Game Over Blackout Scene UI", parent, Vector2.zero, Vector2.zero, Color.black);
                    Stretch(blackout.GetComponent<RectTransform>());
                    blackout.transform.SetAsFirstSibling();
                    CanvasGroup group = blackout.AddComponent<CanvasGroup>();
                    group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;
                    background.objectReferenceValue = blackout.GetComponent<Image>();
                    serialized.FindProperty("backgroundGroup").objectReferenceValue = group;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void BakeMainMenu()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Additive);
            MainMenuController controller = Find<MainMenuController>(scene);
            if (controller == null) { EditorSceneManager.CloseScene(scene, true); return; }
            Transform existing = scene.GetRootGameObjects().Select(root => root.transform)
                .FirstOrDefault(root => root.name == "Scene Fade Canvas");
            Image fade;
            if (existing != null) fade = existing.GetComponentInChildren<Image>(true);
            else
            {
                GameObject root = new GameObject("Scene Fade Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(root, scene);
                Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = short.MaxValue;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
                GameObject overlay = Panel("Fade Overlay", root.transform, Vector2.zero, Vector2.zero, Color.clear);
                Stretch(overlay.GetComponent<RectTransform>());
                fade = overlay.GetComponent<Image>(); fade.raycastTarget = false;
            }
            SetObject(controller, "fadeImage", fade);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void BuildBanTooltip(Transform parent, TMP_FontAsset font)
        {
            Transform old = parent.Find("Chat Ban Tooltip Scene UI");
            if (old != null) return;
            GameObject host = new GameObject("Chat Ban Tooltip Scene UI", typeof(RectTransform), typeof(RunnerChatBanTooltip));
            host.transform.SetParent(parent, false);
            Stretch(host.GetComponent<RectTransform>());
            GameObject panel = Panel("Tooltip", host.transform, new Vector2(82, 26), Vector2.zero, new Color(.05f, .06f, .08f, .94f));
            panel.GetComponent<RectTransform>().pivot = Vector2.zero;
            Text("Label", panel.transform, "차단하기", font, 13, new Vector2(78, 24), Vector2.zero).color = new Color(1f, .72f, .68f);
            RunnerChatBanTooltip tooltip = host.GetComponent<RunnerChatBanTooltip>();
            tooltip.tooltipObject = panel;
            tooltip.tooltipRect = panel.GetComponent<RectTransform>();
            panel.SetActive(false);
            host.transform.SetAsLastSibling();
        }

        private static void BakeRoom(RunnerCampaignSettings settings)
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/StreamerRoom.unity", OpenSceneMode.Additive);
            Canvas canvas = Find<Canvas>(scene);
            if (canvas == null) { EditorSceneManager.CloseScene(scene, true); return; }
            TMP_FontAsset font = canvas.GetComponentsInChildren<TMP_Text>(true).Select(text => text.font).FirstOrDefault(value => value != null);
            RunnerRoomController roomController = Find<RunnerRoomController>(scene);
            if (roomController != null) BuildSaveSlots(canvas.transform, settings, font, roomController);
            UgsNotificationPresenter notificationPresenter = BuildUgsNotification(canvas.transform, font);
            BroadcastLeaderboardProvider existingProvider = Find<BroadcastLeaderboardProvider>(scene);
            if (existingProvider != null) existingProvider.notificationPresenter = notificationPresenter;
            Transform old = canvas.transform.Find("Growth And Leaderboard UI");
            if (old != null)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                EditorSceneManager.CloseScene(scene, true);
                return;
            }
            GameObject root = new GameObject("Growth And Leaderboard UI", typeof(RectTransform), typeof(ScenePanelToggle));
            root.transform.SetParent(canvas.transform, false);
            Stretch(root.GetComponent<RectTransform>());
            Button open = MakeButton("Open Growth", root.transform, "성장 / 매니저 / 순위", font, new Vector2(240, 46), new Vector2(145, -32), new Color(.16f, .58f, .62f));
            RectTransform openRect = open.GetComponent<RectTransform>();
            openRect.anchorMin = openRect.anchorMax = openRect.pivot = new Vector2(0, 1);
            GameObject panel = Panel("Dashboard", root.transform, new Vector2(1040, 680), Vector2.zero, new Color(.02f, .03f, .055f, .985f));
            panel.GetComponent<RectTransform>().anchorMin = panel.GetComponent<RectTransform>().anchorMax = panel.GetComponent<RectTransform>().pivot = new Vector2(.5f, .5f);
            Text("Title", panel.transform, "방송인 성장 / 매니저 / 리더보드", font, 28, new Vector2(800, 45), new Vector2(0, 305));
            Button close = MakeButton("Close", panel.transform, "닫기", font, new Vector2(120, 40), new Vector2(445, 305), new Color(.32f, .34f, .42f));
            StreamerProfilePanel profile = panel.AddComponent<StreamerProfilePanel>();
            profile.settings = settings;
            profile.nameInput = MakeInput("Streamer Name", panel.transform, settings.defaultStreamerName, font,
                new Vector2(210, 36), new Vector2(-390, 305));
            profile.saveButton = MakeButton("Save Name", panel.transform, "이름 저장", font, new Vector2(90, 36), new Vector2(-235, 305), new Color(.20f, .44f, .62f));
            profile.feedbackText = Text("Name Feedback", panel.transform, string.Empty, font, 11, new Vector2(210, 24), new Vector2(-390, 278));
            ScenePanelToggle toggle = root.GetComponent<ScenePanelToggle>();
            toggle.openButton = open; toggle.closeButton = close; toggle.panel = panel; toggle.startOpen = false;
            BuildProgression(panel.transform, settings, font);
            BuildManager(panel.transform, settings, font);
            BuildLeaderboards(panel.transform, settings, font);
            BroadcastLeaderboardProvider createdProvider = Find<BroadcastLeaderboardProvider>(scene);
            if (createdProvider != null) createdProvider.notificationPresenter = notificationPresenter;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static UgsNotificationPresenter BuildUgsNotification(Transform parent, TMP_FontAsset font)
        {
            Transform existing = parent.Find("UGS Service Notification UI");
            if (existing != null) return existing.GetComponent<UgsNotificationPresenter>();
            GameObject overlay = Panel("UGS Service Notification UI", parent, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, .82f));
            Stretch(overlay.GetComponent<RectTransform>());
            GameObject card = Panel("Notification", overlay.transform, new Vector2(720, 390), Vector2.zero, new Color(.045f, .055f, .085f, 1f));
            Text("Title", card.transform, "서비스 알림", font, 28, new Vector2(600, 48), new Vector2(0, 145));
            TMP_Text message = Text("Message", card.transform, string.Empty, font, 17, new Vector2(630, 230), new Vector2(0, 10));
            message.alignment = TextAlignmentOptions.TopLeft;
            message.textWrappingMode = TextWrappingModes.Normal;
            Button close = MakeButton("Close", card.transform, "확인", font, new Vector2(150, 42), new Vector2(0, -155), new Color(.18f, .55f, .58f));
            UgsNotificationPresenter presenter = overlay.AddComponent<UgsNotificationPresenter>();
            presenter.panel = overlay;
            presenter.messageText = message;
            presenter.closeButton = close;
            overlay.SetActive(false);
            overlay.transform.SetAsLastSibling();
            return presenter;
        }

        private static void BuildSaveSlots(Transform parent, RunnerCampaignSettings settings, TMP_FontAsset font, RunnerRoomController controller)
        {
            Transform old = parent.Find("Save Slot Menu");
            if (old != null) return;
            GameObject panel = Panel("Save Slot Menu", parent, new Vector2(1280, 720), Vector2.zero, new Color(.025f, .035f, .06f, .985f));
            Stretch(panel.GetComponent<RectTransform>());
            Text("Title", panel.transform, "게임 시작", font, 42, new Vector2(650, 65), new Vector2(0, 260));
            TMP_Text notice = Text("Notice", panel.transform, "이어할 저장을 선택하거나 빈 슬롯에서 새 게임을 시작하세요.", font, 18, new Vector2(900, 48), new Vector2(0, 210));
            const int maximumRows = 8;
            GameObject[] rows = new GameObject[maximumRows];
            TMP_Text[] labels = new TMP_Text[maximumRows];
            Button[] select = new Button[maximumRows];
            Button[] delete = new Button[maximumRows];
            for (int index = 0; index < maximumRows; index++)
            {
                float y = 140 - index * 62;
                rows[index] = new GameObject($"Slot Row {index + 1}", typeof(RectTransform));
                rows[index].transform.SetParent(panel.transform, false);
                RectTransform rowRect = rows[index].GetComponent<RectTransform>(); rowRect.sizeDelta = new Vector2(850, 54); rowRect.anchoredPosition = new Vector2(0, y);
                select[index] = MakeButton("Select", rows[index].transform, $"슬롯 {index + 1}", font, new Vector2(690, 50), new Vector2(-75, 0), new Color(.13f, .52f, .58f));
                labels[index] = select[index].GetComponentInChildren<TMP_Text>(true);
                labels[index].fontSize = 17;
                delete[index] = MakeButton("Delete", rows[index].transform, "삭제", font, new Vector2(130, 50), new Vector2(350, 0), new Color(.62f, .22f, .28f));
                rows[index].SetActive(index < settings.saveSlotCount);
            }
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("slotPanel").objectReferenceValue = panel;
            serialized.FindProperty("slotNotice").objectReferenceValue = notice;
            SetArray(serialized.FindProperty("slotRows"), rows);
            SetArray(serialized.FindProperty("slotLabels"), labels);
            SetArray(serialized.FindProperty("slotSelectButtons"), select);
            SetArray(serialized.FindProperty("slotDeleteButtons"), delete);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            panel.SetActive(false);
            panel.transform.SetAsLastSibling();
        }

        private static void BuildProgression(Transform parent, RunnerCampaignSettings settings, TMP_FontAsset font)
        {
            GameObject card = Panel("Progression", parent, new Vector2(480, 270), new Vector2(-250, 145), new Color(.07f, .09f, .14f, 1f));
            BroadcasterProgressionPanel view = card.AddComponent<BroadcasterProgressionPanel>();
            view.settings = settings;
            view.levelText = Text("Level", card.transform, "방송인 Lv.1/20", font, 21, new Vector2(250, 30), new Vector2(-85, 105));
            view.experienceText = Text("Experience", card.transform, "EXP 0", font, 16, new Vector2(160, 28), new Vector2(130, 105));
            view.pointText = Text("Points", card.transform, "남은 포인트 1", font, 17, new Vector2(220, 28), new Vector2(0, 68));
            view.witText = Text("Wit", card.transform, "재치 0/5", font, 18, new Vector2(170, 34), new Vector2(-90, 23));
            view.composureText = Text("Composure", card.transform, "평정심 0/5", font, 18, new Vector2(170, 34), new Vector2(-90, -22));
            view.controlText = Text("Control", card.transform, "통제력 0/5", font, 18, new Vector2(170, 34), new Vector2(-90, -67));
            view.witButton = MakeButton("Wit Up", card.transform, "+", font, new Vector2(64, 34), new Vector2(95, 23), new Color(.18f, .65f, .54f));
            view.composureButton = MakeButton("Composure Up", card.transform, "+", font, new Vector2(64, 34), new Vector2(95, -22), new Color(.18f, .65f, .54f));
            view.controlButton = MakeButton("Control Up", card.transform, "+", font, new Vector2(64, 34), new Vector2(95, -67), new Color(.18f, .65f, .54f));
            view.cashText = Text("Cash", card.transform, "보유금 0원", font, 15, new Vector2(170, 28), new Vector2(-115, -110));
            view.respecButton = MakeButton("Respec", card.transform, "스탯 초기화", font, new Vector2(150, 34), new Vector2(125, -110), new Color(.48f, .30f, .34f));
        }

        private static void BuildManager(Transform parent, RunnerCampaignSettings settings, TMP_FontAsset font)
        {
            GameObject card = Panel("Manager", parent, new Vector2(480, 270), new Vector2(250, 145), new Color(.07f, .09f, .14f, 1f));
            ManagerHiringPanel view = card.AddComponent<ManagerHiringPanel>();
            view.settings = settings;
            view.titleText = Text("Title", card.transform, "이번 방송 매니저 선택", font, 20, new Vector2(280, 30), new Vector2(-55, 108));
            view.cashText = Text("Cash", card.transform, "보유금 0원", font, 15, new Vector2(150, 26), new Vector2(150, 108));
            view.tierDescriptions = new TMP_Text[3]; view.unlockButtons = new Button[3]; view.hireButtons = new Button[3];
            for (int index = 0; index < 3; index++)
            {
                float y = 55 - index * 76;
                view.tierDescriptions[index] = Text($"Tier {index + 1}", card.transform, $"매니저 Lv.{index + 1}", font, 14, new Vector2(270, 65), new Vector2(-95, y));
                view.tierDescriptions[index].alignment = TextAlignmentOptions.MidlineLeft;
                view.unlockButtons[index] = MakeButton($"Unlock {index + 1}", card.transform, "해금", font, new Vector2(72, 34), new Vector2(90, y), new Color(.40f, .38f, .22f));
                view.hireButtons[index] = MakeButton($"Hire {index + 1}", card.transform, "고용", font, new Vector2(72, 34), new Vector2(175, y), new Color(.18f, .58f, .52f));
            }
        }

        private static void BuildLeaderboards(Transform parent, RunnerCampaignSettings settings, TMP_FontAsset font)
        {
            BroadcastLeaderboardProvider provider = parent.gameObject.AddComponent<BroadcastLeaderboardProvider>();
            provider.settings = settings;
            BroadcastGameId[] ids = { BroadcastGameId.Runner, BroadcastGameId.TileArena, BroadcastGameId.PlasticKnightmare, BroadcastGameId.Runner };
            string[] names = { "러너", "타일 아레나", "Plastic Knightmare", "팔로워 순위" };
            for (int cardIndex = 0; cardIndex < 4; cardIndex++)
            {
                float x = -375 + cardIndex * 250;
                GameObject card = Panel($"Leaderboard {cardIndex}", parent, new Vector2(235, 280), new Vector2(x, -155), new Color(.06f, .08f, .125f, 1f));
                BroadcastLeaderboardPanel board = card.AddComponent<BroadcastLeaderboardPanel>();
                board.settings = settings; board.provider = provider; board.gameId = ids[cardIndex]; board.followerLeaderboard = cardIndex == 3;
                board.titleText = Text("Title", card.transform, names[cardIndex], font, 17, new Vector2(215, 30), new Vector2(0, 116));
                board.rowTexts = new TMP_Text[5];
                for (int row = 0; row < board.rowTexts.Length; row++)
                {
                    board.rowTexts[row] = Text($"Row {row}", card.transform, string.Empty, font, 13, new Vector2(210, 27), new Vector2(0, 78 - row * 31));
                    board.rowTexts[row].alignment = TextAlignmentOptions.MidlineLeft;
                }
                board.statusText = Text("Status", card.transform, string.Empty, font, 11, new Vector2(210, 42), new Vector2(0, -92));
                board.submitButton = MakeButton("Submit", card.transform, "기록 등록/갱신", font, new Vector2(150, 30), new Vector2(0, -122), new Color(.20f, .44f, .62f));
            }
        }

        private static Image Bar(Transform parent, string name, Vector2 size, Vector2 position, Color color)
        {
            GameObject background = Panel(name + " Background", parent, size, position, new Color(.10f, .12f, .16f, 1f));
            GameObject fill = Panel(name + " Fill", background.transform, size - new Vector2(4, 4), Vector2.zero, color);
            RectTransform rect = fill.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(0, .5f);
            rect.offsetMin = new Vector2(2, 2); rect.offsetMax = new Vector2(-2, -2);
            return fill.GetComponent<Image>();
        }

        private static GameObject Panel(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>(); rect.sizeDelta = size; rect.anchoredPosition = position;
            obj.GetComponent<Image>().color = color;
            return obj;
        }

        private static TMP_Text Text(string name, Transform parent, string value, TMP_FontAsset font, float fontSize, Vector2 size, Vector2 position)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            TMP_Text text = obj.GetComponent<TMP_Text>(); text.text = value; text.font = font; text.fontSize = fontSize; text.color = Color.white; text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = size; text.rectTransform.anchoredPosition = position;
            return text;
        }

        private static Button MakeButton(string name, Transform parent, string label, TMP_FontAsset font, Vector2 size, Vector2 position, Color color)
        {
            GameObject obj = Panel(name, parent, size, position, color);
            Button button = obj.AddComponent<Button>();
            Text("Label", obj.transform, label, font, 14, size - new Vector2(8, 6), Vector2.zero);
            return button;
        }

        private static TMP_InputField MakeInput(string name, Transform parent, string value, TMP_FontAsset font, Vector2 size, Vector2 position)
        {
            GameObject root = Panel(name, parent, size, position, new Color(.12f, .14f, .19f, 1f));
            TMP_Text text = Text("Text", root.transform, value, font, 14, size - new Vector2(18, 8), Vector2.zero);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            TMP_InputField input = root.AddComponent<TMP_InputField>();
            input.textComponent = text;
            input.textViewport = root.GetComponent<RectTransform>();
            input.characterLimit = 16;
            return input;
        }

        private static void SetObject(Object target, string property, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty field = serialized.FindProperty(property);
            if (field != null) { field.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetArray<T>(SerializedProperty property, T[] values) where T : Object
        {
            if (property == null) return;
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static T Find<T>(Scene scene) where T : Component => All<T>(scene).FirstOrDefault();
        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
