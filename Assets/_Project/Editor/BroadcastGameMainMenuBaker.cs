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
using StreamOn.Broadcast;

namespace StreamOn.EditorTools
{
    public static class BroadcastGameMainMenuBaker
    {
        private const string RunnerMenuPath = "Assets/Scenes/RunnerMainMenu.unity";
        private const string TileMenuPath = "Assets/Scenes/TileArenaMainMenu.unity";
        private const string SettingsPath = "Assets/_Project/Settings/RunnerCampaignSettings.asset";
        private const string SessionKey = "StreamOn.BroadcastGameMenus.2026-08-26.v1";

        [InitializeOnLoadMethod]
        private static void ScheduleCreateMissingMenus()
        {
            if (Application.isBatchMode || SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.delayCall += TryCreateMissingMenus;
        }

        private static void TryCreateMissingMenus()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += TryCreateMissingMenus;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                return;
            }
            CreateMissingMenus();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.delayCall += TryCreateMissingMenus;
        }

        [MenuItem("STREAM ON/Create Missing Runner and Tile Main Menus")]
        public static void CreateMissingMenus()
        {
            SessionState.SetBool(SessionKey, true);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RunnerMenuPath) == null)
                CreateMenuScene(RunnerMenuPath, "INHUMANIA RUNNER", "BroadcastRunner",
                    "W / ↑ / SPACE  점프\nS / ↓  구르기\n\n장애물을 피하고 방송 점수를 획득하세요.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TileMenuPath) == null)
                CreateMenuScene(TileMenuPath, "TILE ARENA", "TileArena",
                    "WASD / 방향키  이동\nSPACE  점프\n\n타일 위에서 살아남아 방송 점수를 획득하세요.");
            EnsureBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("STREAM ON: scene-authored Runner and Tile Arena main menus are ready.");
        }

        private static void CreateMenuScene(string path, string fallbackTitle, string gameScene, string tutorialCopy)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Examples & Extras/Fonts/Galmuri14 SDF.asset");
                Sprite panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

                GameObject canvasObject = new GameObject("Game Main Menu Canvas", typeof(RectTransform),
                    typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = .5f;

                Image background = Image("Title Screen Image", canvasObject.transform, Color.white);
                Stretch(background.rectTransform);
                background.preserveAspect = false;

                Image shade = Image("Readability Shade", canvasObject.transform, new Color(0f, 0f, 0f, .34f));
                Stretch(shade.rectTransform);

                Image logo = Image("Logo Image", canvasObject.transform, Color.white);
                SetRect(logo.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                    new Vector2(0f, -255f), new Vector2(820f, 300f));
                logo.preserveAspect = true;

                TMP_Text fallback = Text("Fallback Game Title", canvasObject.transform, fallbackTitle, font, 72f,
                    new Vector2(1100f, 150f), new Vector2(0f, 255f));
                fallback.fontStyle = FontStyles.Bold;
                fallback.color = Color.white;

                Button play = Button("PLAY", canvasObject.transform, "PLAY", font, panelSprite,
                    new Vector2(0f, -185f));
                Button tutorial = Button("TUTORIAL", canvasObject.transform, "TUTORIAL", font, panelSprite,
                    new Vector2(0f, -315f));

                GameObject tutorialPanel = Panel("Tutorial Panel", canvasObject.transform,
                    new Color(.025f, .035f, .055f, .97f), panelSprite, new Vector2(820f, 500f));
                TMP_Text tutorialTitle = Text("Tutorial Title", tutorialPanel.transform, "TUTORIAL", font, 46f,
                    new Vector2(700f, 80f), new Vector2(0f, 165f));
                tutorialTitle.fontStyle = FontStyles.Bold;
                TMP_Text tutorialText = Text("Tutorial Text", tutorialPanel.transform, tutorialCopy, font, 31f,
                    new Vector2(700f, 240f), new Vector2(0f, 15f));
                tutorialText.lineSpacing = 12f;
                Button closeTutorial = Button("Close Tutorial", tutorialPanel.transform, "BACK", font, panelSprite,
                    new Vector2(0f, -180f), new Vector2(260f, 70f), 27f);
                tutorialPanel.SetActive(false);

                GameObject controllerObject = new GameObject("Game Main Menu Controller",
                    typeof(BroadcastGameMainMenuController));
                SceneManager.MoveGameObjectToScene(controllerObject, scene);
                BroadcastGameMainMenuController controller = controllerObject.GetComponent<BroadcastGameMainMenuController>();
                SerializedObject serialized = new SerializedObject(controller);
                serialized.FindProperty("gameSceneName").stringValue = gameScene;
                serialized.FindProperty("roomSceneName").stringValue = "StreamerRoom";
                serialized.FindProperty("titleScreenImage").objectReferenceValue = background;
                serialized.FindProperty("logoImage").objectReferenceValue = logo;
                serialized.FindProperty("playButton").objectReferenceValue = play;
                serialized.FindProperty("tutorialButton").objectReferenceValue = tutorial;
                serialized.FindProperty("tutorialPanel").objectReferenceValue = tutorialPanel;
                serialized.FindProperty("tutorialCloseButton").objectReferenceValue = closeTutorial;
                serialized.FindProperty("tutorialText").objectReferenceValue = tutorialText;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                // Persistent scene listeners make the intended wiring visible in Inspector.
                UnityEventTools.AddPersistentListener(play.onClick, controller.Play);
                UnityEventTools.AddPersistentListener(tutorial.onClick, controller.OpenTutorial);
                UnityEventTools.AddPersistentListener(closeTutorial.onClick, controller.CloseTutorial);

                GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(eventSystem, scene);
                EditorSceneManager.SaveScene(scene, path);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Image Image(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static GameObject Panel(string name, Transform parent, Color color, Sprite sprite, Vector2 size)
        {
            Image image = Image(name, parent, color);
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            SetRect(image.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, size);
            return image.gameObject;
        }

        private static Button Button(string name, Transform parent, string label, TMP_FontAsset font,
            Sprite sprite, Vector2 position, Vector2? size = null, float fontSize = 34f)
        {
            Image image = Image(name, parent, new Color(.08f, .1f, .15f, .94f));
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            SetRect(image.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), position,
                size ?? new Vector2(440f, 96f));
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.55f, .9f, .82f, 1f);
            colors.pressedColor = new Color(.25f, .7f, .62f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            TMP_Text text = Text("Label", image.transform, label, font, fontSize,
                Vector2.zero, Vector2.zero);
            Stretch(text.rectTransform);
            text.fontStyle = FontStyles.Bold;
            return button;
        }

        private static TMP_Text Text(string name, Transform parent, string value, TMP_FontAsset font,
            float fontSize, Vector2 size, Vector2 position)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            SetRect(text.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), position, size);
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void EnsureBuildScenes()
        {
            string[] required = { RunnerMenuPath, TileMenuPath };
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (string path in required)
                if (!scenes.Any(scene => scene.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
