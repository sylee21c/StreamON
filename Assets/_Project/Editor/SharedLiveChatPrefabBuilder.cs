using System.Collections.Generic;
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
    public static class SharedLiveChatPrefabBuilder
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs";
        private const string PrefabPath = PrefabFolder + "/SharedLiveChat.prefab";
        private const string DonationPrefabPath = PrefabFolder + "/SharedDonationPopup.prefab";
        private const string WitPrefabPath = PrefabFolder + "/SharedWitInteraction.prefab";
        private const string WitSettingsPath = "Assets/_Project/Settings/RunnerWitInteractionSettings.asset";
        private const string RunnerScenePath = "Assets/Scenes/BroadcastRunner.unity";
        private const string TileArenaScenePath = "Assets/Scenes/TileArena.unity";

        static SharedLiveChatPrefabBuilder() => EditorApplication.delayCall += RefreshScenes;

        [MenuItem("STREAM ON/Shared UI/Refresh Live Chat Prefab")]
        public static void RefreshScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            GameObject prefab = EnsurePrefab();
            GameObject donationPrefab = EnsureDonationPrefab();
            RunnerWitInteractionSettings witSettings = EnsureWitSettings();
            GameObject witPrefab = EnsureWitPrefab(witSettings);
            if (prefab == null || donationPrefab == null || witPrefab == null) return;
            UpgradeRunnerScene(prefab, donationPrefab, witPrefab);
            UpgradeTileArenaScene(prefab, donationPrefab, witPrefab);
            AssetDatabase.SaveAssets();
        }

        private static RunnerWitInteractionSettings EnsureWitSettings()
        {
            RunnerWitInteractionSettings existing = AssetDatabase.LoadAssetAtPath<RunnerWitInteractionSettings>(WitSettingsPath);
            if (existing != null) return existing;
            RunnerWitInteractionSettings asset = ScriptableObject.CreateInstance<RunnerWitInteractionSettings>();
            asset.prompts = new List<RunnerWitPrompt>
            {
                Prompt("아까 그거 일부러 맞은 거죠?", Choice("방송각을 위한 큰 그림이지", 2), Choice("아니 진짜 실수였어", 1), Choice("보는 눈이 그것밖에 안 돼?", 0, 2)),
                Prompt("오늘도 엔딩 못 보는 거 아님?", Choice("엔딩이 나를 아직 못 본 거지", 2), Choice("오늘은 진짜 가능함", 1), Choice("못 보면 네 탓으로 하자", 0, 2)),
                Prompt("실력 방송 맞나요?", Choice("웃기는 것도 실력입니다", 2), Choice("연습 중인 실력 방송", 1), Choice("나가면 실력 올라감", 0, 2)),
                Prompt("방금 플레이 해명 부탁드립니다", Choice("해명은 다음 판의 나에게 맡긴다", 2), Choice("손이 잠깐 미끄러졌어", 1), Choice("채팅 때문에 집중 못 했잖아", 0, 2)),
                Prompt("이거 클립 따도 됨?", Choice("제목은 전설의 시작으로 부탁", 2), Choice("잘 나온 부분만 따줘", 1), Choice("그걸 왜 따", 0, 2)),
                Prompt("오늘 왜 이렇게 잘함?", Choice("오늘만 잘한다는 전제가 이상한데?", 2), Choice("컨디션이 좀 좋네", 1), Choice("원래 잘했거든", 0, 2))
            };
            AssetDatabase.CreateAsset(asset, WitSettingsPath);
            return asset;
        }

        private static RunnerWitPrompt Prompt(string message, params RunnerWitChoice[] choices) => new RunnerWitPrompt
        {
            viewerMessage = message,
            choices = choices.ToList()
        };

        private static RunnerWitChoice Choice(string text, int quality, int minimumLevel = 1) => new RunnerWitChoice
        {
            text = text,
            quality = quality,
            minimumTalkingLevel = minimumLevel
        };

        private static GameObject EnsureWitPrefab(RunnerWitInteractionSettings settings)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(WitPrefabPath);
            if (existing != null)
            {
                UpgradeExistingWitPrefab(settings);
                return AssetDatabase.LoadAssetAtPath<GameObject>(WitPrefabPath);
            }
            TMP_FontAsset font = FindGalmuriFont();
            Sprite panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            GameObject root = new GameObject("Wit Interaction", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(440f, 170f);
            Image panel = root.GetComponent<Image>();
            panel.sprite = panelSprite; panel.type = Image.Type.Sliced; panel.color = new Color(0.035f, 0.045f, 0.075f, 0.97f);
            panel.raycastTarget = false;
            TMP_Text viewer = Label("Viewer Message", root.transform, "시청자  질문이 들어옵니다", font, 16f,
                TextAlignmentOptions.MidlineLeft, new Vector2(360f, 34f), new Vector2(-18f, 58f));
            viewer.color = new Color(0.58f, 0.92f, 1f);
            Image timer = CreateTimerRing(root.transform, new Vector2(190f, 58f));
            Button[] buttons = new Button[3];
            TMP_Text[] labels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject buttonObject = new GameObject($"Choice {i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(root.transform, false);
                RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
                buttonRect.sizeDelta = new Vector2(400f, 30f); buttonRect.anchoredPosition = new Vector2(0f, 20f - i * 35f);
                Image image = buttonObject.GetComponent<Image>();
                image.sprite = panelSprite; image.type = Image.Type.Sliced; image.color = new Color(0.11f, 0.14f, 0.22f, 1f);
                buttons[i] = buttonObject.GetComponent<Button>();
                ColorBlock colors = buttons[i].colors;
                colors.normalColor = Color.white; colors.highlightedColor = new Color(0.72f, 1f, 0.92f);
                colors.pressedColor = new Color(0.45f, 0.85f, 0.76f); buttons[i].colors = colors;
                labels[i] = Label("Label", buttonObject.transform, $"{i + 1}. 답변", font, 14f,
                    TextAlignmentOptions.MidlineLeft, new Vector2(370f, 28f), Vector2.zero);
            }
            TMP_Text feedback = Label("Feedback", root.transform, string.Empty, font, 14f,
                TextAlignmentOptions.Midline, new Vector2(400f, 22f), new Vector2(0f, -75f));
            feedback.color = new Color(1f, 0.84f, 0.30f);
            RunnerWitInteractionController controller = root.AddComponent<RunnerWitInteractionController>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("settings").objectReferenceValue = settings;
            serialized.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            serialized.FindProperty("viewerText").objectReferenceValue = viewer;
            serialized.FindProperty("timerFill").objectReferenceValue = timer;
            serialized.FindProperty("feedbackText").objectReferenceValue = feedback;
            SetArray(serialized.FindProperty("choiceButtons"), buttons);
            SetArray(serialized.FindProperty("choiceLabels"), labels);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<CanvasGroup>().alpha = 0f;
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, WitPrefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void UpgradeExistingWitPrefab(RunnerWitInteractionSettings settings)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(WitPrefabPath);
            try
            {
                RunnerWitInteractionController controller = root.GetComponent<RunnerWitInteractionController>();
                if (controller == null) return;
                RectTransform rootRect = root.GetComponent<RectTransform>();
                bool legacyLayout = Vector2.Distance(rootRect.sizeDelta, new Vector2(620f, 230f)) < 1f;
                Transform oldTimer = root.transform.Find("Timer");
                if (oldTimer != null) Object.DestroyImmediate(oldTimer.gameObject);
                Image timer = root.transform.Find("Timer Ring Fill")?.GetComponent<Image>();
                if (timer == null) timer = CreateTimerRing(root.transform, legacyLayout ? new Vector2(190f, 58f) : new Vector2(rootRect.rect.width * 0.43f, rootRect.rect.height * 0.34f));
                timer.type = Image.Type.Filled;
                timer.fillMethod = Image.FillMethod.Radial360;
                timer.fillOrigin = 2;
                timer.fillClockwise = false;
                if (legacyLayout)
                {
                    rootRect.sizeDelta = new Vector2(440f, 170f);
                    SetRect(root.transform.Find("Viewer Message") as RectTransform, new Vector2(360f, 34f), new Vector2(-18f, 58f));
                    TMP_Text viewer = root.transform.Find("Viewer Message")?.GetComponent<TMP_Text>();
                    if (viewer != null) viewer.fontSize = 16f;
                    for (int i = 0; i < 3; i++)
                    {
                        Transform choice = root.transform.Find($"Choice {i + 1}");
                        SetRect(choice as RectTransform, new Vector2(400f, 30f), new Vector2(0f, 20f - i * 35f));
                        TMP_Text label = choice?.Find("Label")?.GetComponent<TMP_Text>();
                        if (label != null) { label.fontSize = 14f; SetRect(label.rectTransform, new Vector2(370f, 28f), Vector2.zero); }
                    }
                    Transform feedback = root.transform.Find("Feedback");
                    SetRect(feedback as RectTransform, new Vector2(400f, 22f), new Vector2(0f, -75f));
                    TMP_Text feedbackText = feedback?.GetComponent<TMP_Text>();
                    if (feedbackText != null) feedbackText.fontSize = 14f;
                }
                Image panel = root.GetComponent<Image>();
                if (panel != null) panel.raycastTarget = false;
                SerializedObject serialized = new SerializedObject(controller);
                serialized.FindProperty("settings").objectReferenceValue = settings;
                serialized.FindProperty("timerFill").objectReferenceValue = timer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, WitPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Image CreateTimerRing(Transform parent, Vector2 position)
        {
            Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            Image background = new GameObject("Timer Ring Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            background.transform.SetParent(parent, false);
            background.sprite = circle; background.color = new Color(0.18f, 0.22f, 0.30f, 0.9f); background.raycastTarget = false;
            SetRect(background.rectTransform, new Vector2(28f, 28f), position);
            Image fill = new GameObject("Timer Ring Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            fill.transform.SetParent(parent, false);
            fill.sprite = circle; fill.color = new Color(0.20f, 0.92f, 0.78f, 1f); fill.raycastTarget = false;
            // Counter-clockwise fill means decreasing fillAmount erases clockwise: 12 -> 3 -> 6 -> 9 -> 12.
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Radial360; fill.fillOrigin = 2; fill.fillClockwise = false; fill.fillAmount = 1f;
            SetRect(fill.rectTransform, new Vector2(24f, 24f), position);
            return fill;
        }

        private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            if (rect == null) return;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void SetArray<T>(SerializedProperty property, T[] values) where T : Object
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static GameObject EnsureDonationPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DonationPrefabPath);
            if (existing != null) return existing;
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            TMP_FontAsset font = FindGalmuriFont();
            Sprite panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            GameObject root = new GameObject("Donation Popup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(560f, 150f);
            Image background = root.GetComponent<Image>();
            background.sprite = panelSprite;
            background.type = Image.Type.Sliced;
            background.color = new Color(0.035f, 0.045f, 0.075f, 0.97f);
            Image accent = new GameObject("Accent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            accent.transform.SetParent(root.transform, false);
            accent.color = new Color(0.15f, 0.86f, 0.76f, 1f);
            RectTransform accentRect = accent.rectTransform;
            accentRect.anchorMin = new Vector2(0f, 0f); accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f); accentRect.sizeDelta = new Vector2(8f, 0f);
            accentRect.anchoredPosition = Vector2.zero;

            TMP_Text donor = Label("Donor", root.transform, "시청자님이", font, 18f,
                TextAlignmentOptions.MidlineLeft, new Vector2(490f, 30f), new Vector2(18f, 46f));
            donor.color = new Color(0.58f, 0.92f, 1f);
            TMP_Text amount = Label("Amount", root.transform, "1,000원을 후원해 주셨어요!", font, 25f,
                TextAlignmentOptions.MidlineLeft, new Vector2(490f, 38f), new Vector2(18f, 12f));
            amount.color = new Color(1f, 0.84f, 0.30f);
            TMP_Text message = Label("Message", root.transform, "플레이 좋다!", font, 18f,
                TextAlignmentOptions.MidlineLeft, new Vector2(490f, 38f), new Vector2(18f, -35f));

            RunnerDonationPopupController popup = root.AddComponent<RunnerDonationPopupController>();
            SerializedObject serialized = new SerializedObject(popup);
            serialized.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            serialized.FindProperty("donorText").objectReferenceValue = donor;
            serialized.FindProperty("amountText").objectReferenceValue = amount;
            serialized.FindProperty("messageText").objectReferenceValue = message;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<CanvasGroup>().alpha = 0f;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, DonationPrefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject EnsurePrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");

            TMP_FontAsset font = FindGalmuriFont();
            Sprite panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            GameObject root = new GameObject("Live Chat Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(300f, 720f);
            Image panel = root.GetComponent<Image>();
            panel.sprite = panelSprite;
            panel.type = Image.Type.Sliced;
            panel.color = new Color(0.04f, 0.05f, 0.08f, 0.93f);

            TMP_Text title = Label("Title", root.transform, "LIVE CHAT [LOCAL]\n현재 시청자 0명", font, 18f,
                TextAlignmentOptions.MidlineLeft, new Vector2(250f, 68f), new Vector2(0f, 314f));
            title.color = new Color(0.4f, 0.9f, 0.82f);
            TMP_Text[] slots = new TMP_Text[8];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = Label($"Message {i + 1}", root.transform, string.Empty, font, 18f,
                    TextAlignmentOptions.MidlineLeft, new Vector2(250f, 56f), new Vector2(0f, 235f - i * 70f));

            RunnerChatController chat = root.AddComponent<RunnerChatController>();
            SerializedObject chatSerialized = new SerializedObject(chat);
            SerializedProperty messages = chatSerialized.FindProperty("messageSlots");
            messages.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++) messages.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            chatSerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void UpgradeRunnerScene(GameObject prefab, GameObject donationPrefab, GameObject witPrefab)
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(RunnerScenePath);
            if (sceneAsset == null) return;
            Scene scene = SceneManager.GetSceneByPath(RunnerScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(RunnerScenePath, OpenSceneMode.Additive);
            RunnerGameManager manager = FindInScene<RunnerGameManager>(scene);
            Canvas canvas = FindInScene<Canvas>(scene);
            if (manager == null || canvas == null) { if (openedHere) EditorSceneManager.CloseScene(scene, true); return; }

            RunnerChatController chat = FindInScene<RunnerChatController>(scene);
            if (chat == null || !PrefabUtility.IsPartOfPrefabInstance(chat))
            {
                if (chat != null) Object.DestroyImmediate(chat.gameObject);
                chat = InstantiateChat(prefab, scene, canvas.transform);
            }
            EnsureDonationPopup(donationPrefab, scene, canvas.transform);
            EnsureWitInteraction(witPrefab, scene, canvas.transform);
            SerializedObject managerSerialized = new SerializedObject(manager);
            managerSerialized.FindProperty("chat").objectReferenceValue = chat;
            managerSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }

        private static void UpgradeTileArenaScene(GameObject prefab, GameObject donationPrefab, GameObject witPrefab)
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(TileArenaScenePath);
            if (sceneAsset == null) return;
            Scene scene = SceneManager.GetSceneByPath(TileArenaScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(TileArenaScenePath, OpenSceneMode.Additive);
            TileArenaController controller = FindInScene<TileArenaController>(scene);
            Canvas canvas = FindInScene<Canvas>(scene);
            if (controller == null || canvas == null) { if (openedHere) EditorSceneManager.CloseScene(scene, true); return; }

            RunnerChatController chat = FindInScene<RunnerChatController>(scene);
            if (chat == null) chat = InstantiateChat(prefab, scene, canvas.transform);
            EnsureDonationPopup(donationPrefab, scene, canvas.transform);
            EnsureWitInteraction(witPrefab, scene, canvas.transform);
            TileArenaChatAdapter adapter = controller.GetComponent<TileArenaChatAdapter>();
            if (adapter == null) adapter = controller.gameObject.AddComponent<TileArenaChatAdapter>();
            SerializedObject adapterSerialized = new SerializedObject(adapter);
            adapterSerialized.FindProperty("gameController").objectReferenceValue = controller;
            adapterSerialized.FindProperty("chatController").objectReferenceValue = chat;
            adapterSerialized.FindProperty("growthSettings").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<RunnerBroadcastGrowthSettings>("Assets/_Project/Settings/RunnerBroadcastGrowthSettings.asset");
            adapterSerialized.FindProperty("campaignSettings").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<RunnerCampaignSettings>("Assets/_Project/Settings/RunnerCampaignSettings.asset");
            adapterSerialized.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("chatAdapter").objectReferenceValue = adapter;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            Transform app = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "App");
            if (app is RectTransform appRect) appRect.anchoredPosition = new Vector2(-150f, appRect.anchoredPosition.y);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }

        private static RunnerWitInteractionController EnsureWitInteraction(GameObject prefab, Scene scene, Transform parent)
        {
            RunnerWitInteractionController existing = FindInScene<RunnerWitInteractionController>(scene);
            if (existing != null) return existing;
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            instance.transform.SetParent(parent, false);
            RectTransform rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(-150f, -225f);
            rect.SetAsLastSibling();
            return instance.GetComponent<RunnerWitInteractionController>();
        }

        private static RunnerDonationPopupController EnsureDonationPopup(GameObject prefab, Scene scene, Transform parent)
        {
            RunnerDonationPopupController existing = FindInScene<RunnerDonationPopupController>(scene);
            if (existing != null) return existing;
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            instance.transform.SetParent(parent, false);
            RectTransform rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(-150f, -82f);
            rect.SetAsLastSibling();
            return instance.GetComponent<RunnerDonationPopupController>();
        }

        private static RunnerChatController InstantiateChat(GameObject prefab, Scene scene, Transform parent)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            instance.transform.SetParent(parent, false);
            RectTransform rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(300f, 720f);
            rect.anchoredPosition = new Vector2(-150f, 0f);
            return instance.GetComponent<RunnerChatController>();
        }

        private static T FindInScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();

        private static TMP_Text Label(string name, Transform parent, string value, TMP_FontAsset font, float size,
            TextAlignmentOptions alignment, Vector2 dimensions, Vector2 position)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            TextMeshProUGUI label = gameObject.GetComponent<TextMeshProUGUI>();
            label.text = value;
            label.font = font;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.Normal;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = dimensions;
            rect.anchoredPosition = position;
            return label;
        }

        private static TMP_FontAsset FindGalmuriFont()
        {
            string guid = AssetDatabase.FindAssets("Galmuri14 SDF t:TMP_FontAsset").FirstOrDefault();
            return string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
