#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StreamOn.MainMenu;
using StreamOn.Minigames.Runner;

namespace StreamOn.EditorTools
{
    public static class StreamOnMainMenuBaker
    {
        private const string ScenePath = "Assets/Scenes/StreamON_Mainmenu.unity";
        private const string SettingsPath = "Assets/_Project/Settings/RunnerCampaignSettings.asset";
        private const string SessionKey = "StreamOn.MainMenuBake.2026-08-26.v2";

        private static void ScheduleBake()
        {
            if (Application.isBatchMode || SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.delayCall += TryBake;
        }

        private static void TryBake()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += TryBake;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                return;
            }
            Bake();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.delayCall += TryBake;
        }

        [MenuItem("STREAM ON/Bake StreamON Main Menu")]
        public static void Bake()
        {
            SessionState.SetBool(SessionKey, true);
            SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (asset == null)
            {
                Debug.LogError($"STREAM ON main menu scene not found: {ScenePath}");
                return;
            }
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedForBake = !scene.IsValid() || !scene.isLoaded;
            if (openedForBake) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Transform[] transforms = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                Transform oldMenu = transforms.FirstOrDefault(item => item.name == "Stream ON Main Menu Canvas");
                if (oldMenu != null) Object.DestroyImmediate(oldMenu.gameObject);
                StreamOnMainMenuController oldController = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<StreamOnMainMenuController>(true)).FirstOrDefault();
                if (oldController != null) Object.DestroyImmediate(oldController.gameObject);

                RunnerRoomController roomController = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<RunnerRoomController>(true)).FirstOrDefault();
                RunnerRoomPlayerController playerController = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<RunnerRoomPlayerController>(true)).FirstOrDefault();
                Camera mainCamera = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .FirstOrDefault(camera => camera.name == "Main Camera");
                if (roomController != null) roomController.enabled = false;
                if (playerController != null) playerController.enabled = false;
                if (mainCamera != null) mainCamera.transform.position = new Vector3(-1.273f, 1.293f, -.225f);

                foreach (Canvas oldCanvas in scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Canvas>(true)))
                    oldCanvas.gameObject.SetActive(false);

                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Examples & Extras/Fonts/Galmuri14 SDF.asset");
                Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                RunnerCampaignSettings settings = AssetDatabase.LoadAssetAtPath<RunnerCampaignSettings>(SettingsPath);

                GameObject canvasObject = new GameObject("Stream ON Main Menu Canvas", typeof(RectTransform),
                    typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5000;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = .5f;

                Image readability = CreateImage("Menu Readability Shade", canvasObject.transform,
                    new Color(0f, 0f, 0f, .23f));
                Stretch(readability.rectTransform);
                readability.raycastTarget = false;

                Image logo = CreateImage("Game Logo Image", canvasObject.transform, Color.white);
                SetRect(logo.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                    new Vector2(0f, -185f), new Vector2(760f, 230f));
                logo.preserveAspect = true;
                logo.raycastTarget = false;

                TMP_Text prompt = CreateText("Streamer Name Prompt", canvasObject.transform,
                    "스트리머의 이름을 입력하세요:", font, 30f, new Vector2(640f, 55f), new Vector2(0f, 115f));
                prompt.alignment = TextAlignmentOptions.Center;

                TMP_InputField nameInput = CreateInput("Streamer Name Input", canvasObject.transform, font, sprite,
                    new Vector2(560f, 76f), new Vector2(0f, 35f));
                Button start = CreateButton("Start", canvasObject.transform, "시작하기", font, sprite,
                    new Vector2(0f, -85f));
                start.interactable = false;
                Button option = CreateButton("Option", canvasObject.transform, "옵션", font, sprite,
                    new Vector2(0f, -195f));

                GameObject optionPanel = CreateImage("Option Panel", canvasObject.transform,
                    new Color(0f, 0f, 0f, .62f)).gameObject;
                Stretch(optionPanel.GetComponent<RectTransform>());
                GameObject optionWindow = CreatePanel("Option Window", optionPanel.transform,
                    new Vector2(720f, 570f), Vector2.zero, new Color(.025f, .035f, .06f, .98f), sprite);
                TMP_Text optionTitle = CreateText("Title", optionWindow.transform, "옵션", font, 42f,
                    new Vector2(600f, 70f), new Vector2(0f, 220f));
                optionTitle.fontStyle = FontStyles.Bold;
                Slider master = CreateSlider("Master Volume", optionWindow.transform, sprite, new Vector2(0f, 100f));
                Slider bgm = CreateSlider("BGM Volume", optionWindow.transform, sprite, new Vector2(0f, 0f));
                Slider sfx = CreateSlider("SFX Volume", optionWindow.transform, sprite, new Vector2(0f, -100f));
                TMP_Text masterLabel = CreateText("Master Volume Label", optionWindow.transform, "전체 음량  100%",
                    font, 23f, new Vector2(540f, 36f), new Vector2(0f, 145f));
                TMP_Text bgmLabel = CreateText("BGM Volume Label", optionWindow.transform, "BGM  100%",
                    font, 23f, new Vector2(540f, 36f), new Vector2(0f, 45f));
                TMP_Text sfxLabel = CreateText("SFX Volume Label", optionWindow.transform, "SFX  100%",
                    font, 23f, new Vector2(540f, 36f), new Vector2(0f, -55f));
                Button closeOption = CreateButton("Close Option", optionWindow.transform, "닫기", font, sprite,
                    new Vector2(0f, -220f), new Vector2(260f, 64f), 25f);
                optionPanel.SetActive(false);

                GameObject controllerObject = new GameObject("Stream ON Main Menu Controller",
                    typeof(StreamOnMainMenuController));
                SceneManager.MoveGameObjectToScene(controllerObject, scene);
                StreamOnMainMenuController controller = controllerObject.GetComponent<StreamOnMainMenuController>();
                Animator animator = playerController != null
                    ? playerController.GetComponentInChildren<Animator>(true)
                    : scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Animator>(true)).FirstOrDefault();
                SerializedObject serialized = new SerializedObject(controller);
                serialized.FindProperty("settings").objectReferenceValue = settings;
                serialized.FindProperty("roomSceneName").stringValue = settings != null ? settings.roomSceneName : "StreamerRoom";
                serialized.FindProperty("mainCameraTransform").objectReferenceValue = mainCamera != null ? mainCamera.transform : null;
                serialized.FindProperty("playerTransform").objectReferenceValue = playerController != null ? playerController.transform : null;
                serialized.FindProperty("playerAnimator").objectReferenceValue = animator;
                serialized.FindProperty("cameraStartPosition").vector3Value = new Vector3(-1.273f, 1.293f, -.225f);
                serialized.FindProperty("cameraEndPosition").vector3Value = new Vector3(-2.445f, 1.293f, -1.397f);
                serialized.FindProperty("cameraTravelSeconds").floatValue = 18f;
                serialized.FindProperty("gameLogoImage").objectReferenceValue = logo;
                serialized.FindProperty("streamerNameInput").objectReferenceValue = nameInput;
                serialized.FindProperty("startButton").objectReferenceValue = start;
                serialized.FindProperty("optionButton").objectReferenceValue = option;
                serialized.FindProperty("optionPanel").objectReferenceValue = optionPanel;
                serialized.FindProperty("optionCloseButton").objectReferenceValue = closeOption;
                serialized.FindProperty("masterVolumeSlider").objectReferenceValue = master;
                serialized.FindProperty("bgmVolumeSlider").objectReferenceValue = bgm;
                serialized.FindProperty("sfxVolumeSlider").objectReferenceValue = sfx;
                serialized.FindProperty("masterVolumeLabel").objectReferenceValue = masterLabel;
                serialized.FindProperty("bgmVolumeLabel").objectReferenceValue = bgmLabel;
                serialized.FindProperty("sfxVolumeLabel").objectReferenceValue = sfxLabel;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                UnityEventTools.AddPersistentListener(nameInput.onValueChanged, controller.HandleNameChanged);
                UnityEventTools.AddPersistentListener(start.onClick, controller.StartGame);
                UnityEventTools.AddPersistentListener(option.onClick, controller.OpenOptions);
                UnityEventTools.AddPersistentListener(closeOption.onClick, controller.CloseOptions);
                UnityEventTools.AddPersistentListener(master.onValueChanged, controller.SetMasterVolume);
                UnityEventTools.AddPersistentListener(bgm.onValueChanged, controller.SetBgmVolume);
                UnityEventTools.AddPersistentListener(sfx.onValueChanged, controller.SetSfxVolume);

                EventSystem eventSystem = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).FirstOrDefault();
                if (eventSystem == null)
                {
                    GameObject eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    SceneManager.MoveGameObjectToScene(eventObject, scene);
                }
                else eventSystem.gameObject.SetActive(true);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                EnsureFirstBuildScene();
                AssetDatabase.SaveAssets();
                Debug.Log("STREAM ON: scene-authored main menu baked into StreamON_Mainmenu.");
            }
            finally
            {
                if (openedForBake && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static TMP_InputField CreateInput(string name, Transform parent, TMP_FontAsset font,
            Sprite sprite, Vector2 size, Vector2 position)
        {
            Image background = CreateImage(name, parent, new Color(.94f, .95f, .98f, .98f));
            background.sprite = sprite;
            background.type = UnityEngine.UI.Image.Type.Sliced;
            SetRect(background.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), position, size);
            TMP_InputField input = background.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.characterLimit = 16;
            input.lineType = TMP_InputField.LineType.SingleLine;
            TMP_Text value = CreateText("Text", background.transform, string.Empty, font, 27f,
                Vector2.zero, Vector2.zero);
            Stretch(value.rectTransform);
            value.rectTransform.offsetMin = new Vector2(22f, 7f);
            value.rectTransform.offsetMax = new Vector2(-22f, -7f);
            value.color = new Color(.07f, .08f, .11f, 1f);
            value.alignment = TextAlignmentOptions.MidlineLeft;
            TMP_Text placeholder = CreateText("Placeholder", background.transform, "최대 16자", font, 25f,
                Vector2.zero, Vector2.zero);
            Stretch(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(22f, 7f);
            placeholder.rectTransform.offsetMax = new Vector2(-22f, -7f);
            placeholder.color = new Color(.35f, .37f, .42f, .72f);
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            input.textComponent = value;
            input.placeholder = placeholder;
            return input;
        }

        private static Slider CreateSlider(string name, Transform parent, Sprite sprite, Vector2 position)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            SetRect(root.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                position, new Vector2(540f, 36f));
            Image background = CreateImage("Background", root.transform, new Color(.16f, .18f, .23f, 1f));
            background.sprite = sprite;
            background.type = UnityEngine.UI.Image.Type.Sliced;
            Stretch(background.rectTransform);
            RectTransform fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(root.transform, false);
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(6f, 6f);
            fillArea.offsetMax = new Vector2(-18f, -6f);
            Image fill = CreateImage("Fill", fillArea, new Color(.16f, .82f, .48f, 1f));
            Stretch(fill.rectTransform);
            RectTransform handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(root.transform, false);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(8f, 0f);
            handleArea.offsetMax = new Vector2(-8f, 0f);
            Image handle = CreateImage("Handle", handleArea, Color.white);
            handle.sprite = sprite;
            handle.type = UnityEngine.UI.Image.Type.Sliced;
            SetRect(handle.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(24f, 46f));
            Slider slider = root.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
        }

        private static Button CreateButton(string name, Transform parent, string label, TMP_FontAsset font,
            Sprite sprite, Vector2 position, Vector2? size = null, float fontSize = 29f)
        {
            Image image = CreateImage(name, parent, new Color(.08f, .1f, .14f, .96f));
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            SetRect(image.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), position,
                size ?? new Vector2(400f, 82f));
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.52f, .9f, .8f, 1f);
            colors.pressedColor = new Color(.24f, .68f, .56f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(.25f, .26f, .29f, .72f);
            button.colors = colors;
            TMP_Text text = CreateText("Label", image.transform, label, font, fontSize, Vector2.zero, Vector2.zero);
            Stretch(text.rectTransform);
            text.fontStyle = FontStyles.Bold;
            return button;
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 size, Vector2 position,
            Color color, Sprite sprite)
        {
            Image image = CreateImage(name, parent, color);
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            SetRect(image.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), position, size);
            return image.gameObject;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, TMP_FontAsset font,
            float size, Vector2 rectSize, Vector2 position)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            SetRect(text.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), position, rectSize);
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void EnsureFirstBuildScene()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(scene => scene.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
