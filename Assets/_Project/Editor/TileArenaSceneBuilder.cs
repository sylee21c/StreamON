using System.Collections.Generic;
using System.Linq;
using StreamOn.Minigames.TileArena;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StreamOn.Editor
{
    [InitializeOnLoad]
    public static class TileArenaSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/TileArena.unity";
        private const int Grid = 16;
        private const float BoardSize = 540f;

        static TileArenaSceneBuilder() => EditorApplication.delayCall += BuildMissingScene;

        private static void BuildMissingScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) BuildScene();
        }

        [MenuItem("STREAM ON/Build or Refresh Tile Arena Scene")]
        public static void BuildScene()
        {
            Scene previousScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            scene.name = "TileArena";
            TMP_FontAsset font = FindGalmuriFont();

            GameObject canvasObject = new GameObject("Tile Arena UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            Image background = ImageObject("Background", canvas.transform, new Color32(8, 11, 24, 255));
            Stretch(background.rectTransform);

            GameObject controllerObject = new GameObject("Tile Arena Game Manager");
            TileArenaController controller = controllerObject.AddComponent<TileArenaController>();
            TileArenaAudioController audioController = controllerObject.AddComponent<TileArenaAudioController>();
            ConfigureAudioSources(controllerObject.transform, audioController);
            AssignOriginalAudioClips(audioController);
            TileArenaAudioControls audioControls = controllerObject.AddComponent<TileArenaAudioControls>();
            new GameObject("Audio Listener", typeof(AudioListener)).transform.SetParent(controllerObject.transform, false);

            RectTransform app = RectObject("App", canvas.transform);
            Place(app, new Vector2(0.5f, 0.5f), new Vector2(620f, 710f), Vector2.zero);

            RectTransform hud = RectObject("HUD", app);
            Place(hud, new Vector2(0.5f, 0.5f), new Vector2(620f, 52f), new Vector2(0f, 319f));
            TileArenaCircleGraphic brandMark = Circle("Brand Mark", hud, new Color32(255, 183, 45, 255));
            Place(brandMark.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(15f, 15f), new Vector2(-292f, 0f));
            TMP_Text brand = Text("Brand", hud, "TILE ARENA", font, 24f, TextAlignmentOptions.MidlineLeft, Color.white);
            Place(brand.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(210f, 42f), new Vector2(-175f, 0f));
            TMP_Text scoreCaption = Text("Score Caption", hud, "SCORE", font, 12f, TextAlignmentOptions.MidlineRight, new Color32(150, 159, 190, 255));
            Place(scoreCaption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(55f, 20f), new Vector2(73f, 10f));
            TMP_Text score = Text("Score", hud, "0", font, 17f, TextAlignmentOptions.MidlineRight, Color.white);
            Place(score.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(80f, 24f), new Vector2(135f, -8f));
            TMP_Text bestCaption = Text("Best Caption", hud, "BEST", font, 12f, TextAlignmentOptions.MidlineRight, new Color32(150, 159, 190, 255));
            Place(bestCaption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(55f, 20f), new Vector2(196f, 10f));
            TMP_Text best = Text("Best", hud, "0", font, 17f, TextAlignmentOptions.MidlineRight, Color.white);
            Place(best.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(80f, 24f), new Vector2(255f, -8f));
            TMP_Text lives = Text("Lives", hud, "♥♥♥♥♥", font, 17f, TextAlignmentOptions.MidlineRight, new Color32(255, 71, 105, 255));
            Place(lives.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(115f, 30f), new Vector2(185f, 18f));
            Button soundButton = ButtonObject("Sound Toggle", hud, "SOUND ON", font, new Color32(18, 24, 44, 255), new Vector2(92f, 32f), new Vector2(262f, -1f));
            TMP_Text soundLabel = soundButton.GetComponentInChildren<TMP_Text>();
            UnityEventTools.AddPersistentListener(soundButton.onClick, audioControls.ToggleSound);

            Image boardImage = ImageObject("Board", app, new Color32(4, 174, 85, 255));
            RectTransform board = boardImage.rectTransform;
            Place(board, new Vector2(0.5f, 0.5f), Vector2.one * BoardSize, new Vector2(0f, 20f));
            board.gameObject.AddComponent<RectMask2D>();
            float cellSize = BoardSize / Grid;
            List<Image> tiles = new List<Image>(Grid * Grid);
            RectTransform tileLayer = RectObject("Tiles (Scene Authored 16x16)", board);
            Stretch(tileLayer);
            for (int y = 0; y < Grid; y++)
            for (int x = 0; x < Grid; x++)
            {
                Image tile = ImageObject($"Tile {x:00},{y:00}", tileLayer, (x + y) % 2 == 0 ? new Color32(245, 246, 248, 255) : new Color32(236, 238, 242, 255));
                PlaceTopLeft(tile.rectTransform, new Vector2(cellSize, cellSize), new Vector2(x * cellSize, -y * cellSize));
                tile.raycastTarget = false;
                tiles.Add(tile);
            }

            List<Image> hazards = new List<Image>(Grid * Grid);
            RectTransform hazardLayer = RectObject("Hazards (Scene Authored Pool)", board);
            Stretch(hazardLayer);
            for (int i = 0; i < Grid * Grid; i++)
            {
                Image hazard = ImageObject($"Hazard {i:000}", hazardLayer, new Color32(255, 48, 66, 255));
                PlaceTopLeft(hazard.rectTransform, new Vector2(cellSize, cellSize), Vector2.zero);
                hazard.raycastTarget = false;
                hazard.gameObject.SetActive(false);
                hazards.Add(hazard);
            }

            CreateGridLines(board, hazardLayer.GetSiblingIndex() + 1);

            RectTransform player = RectObject("Player", board);
            PlaceTopLeft(player, Vector2.one * cellSize, new Vector2(0f, -7f * cellSize));
            TileArenaCircleGraphic shadow = Circle("Shadow", player, new Color(0f, 0f, 0f, 0.68f));
            Place(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(cellSize * 0.62f, cellSize * 0.15f), new Vector2(0f, -cellSize * 0.32f));
            RectTransform avatar = RectObject("Avatar", player);
            Place(avatar, new Vector2(0.5f, 0.5f), Vector2.one * cellSize * 0.86f, new Vector2(0f, 1f));
            TileArenaCircleGraphic avatarBorder = Circle("White Border", avatar, new Color32(255, 255, 255, 220));
            Stretch(avatarBorder.rectTransform);
            TileArenaCircleGraphic avatarCore = Circle("Orange Core", avatar, new Color32(255, 157, 34, 255));
            Place(avatarCore.rectTransform, new Vector2(0.5f, 0.5f), Vector2.one * cellSize * 0.73f, Vector2.zero);
            TileArenaCircleGraphic highlight = Circle("Highlight", avatar, new Color32(255, 241, 170, 235));
            Place(highlight.rectTransform, new Vector2(0.5f, 0.5f), Vector2.one * cellSize * 0.20f, new Vector2(-cellSize * 0.17f, cellSize * 0.17f));

            Image startOverlay = ImageObject("Start Overlay", board, new Color(8f / 255f, 11f / 255f, 24f / 255f, 0.94f));
            Stretch(startOverlay.rectTransform);
            Button startButton = ButtonObject("Start Button", startOverlay.transform, "START", font, new Color32(37, 49, 82, 255), new Vector2(180f, 56f), Vector2.zero);
            UnityEventTools.AddPersistentListener(startButton.onClick, controller.StartGame);

            Image overlay = ImageObject("Game Over Overlay", board, new Color(8f / 255f, 11f / 255f, 24f / 255f, 0.94f));
            Stretch(overlay.rectTransform);
            TMP_Text kicker = Text("Kicker", overlay.transform, "GAME OVER", font, 14f, TextAlignmentOptions.Center, new Color32(111, 247, 220, 255));
            Place(kicker.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(300f, 28f), new Vector2(0f, 100f));
            TMP_Text overTitle = Text("Title", overlay.transform, "다시 도전!", font, 42f, TextAlignmentOptions.Center, Color.white);
            Place(overTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(400f, 65f), new Vector2(0f, 48f));
            TMP_Text overDescription = Text("Description", overlay.transform, "점수\n최고 점수", font, 17f, TextAlignmentOptions.Center, new Color32(184, 192, 220, 255));
            Place(overDescription.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(160f, 72f), new Vector2(-52f, -18f));
            TMP_Text overScore = Text("Final Score", overlay.transform, "0", font, 18f, TextAlignmentOptions.Center, Color.white);
            Place(overScore.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(100f, 32f), new Vector2(70f, -2f));
            TMP_Text overBest = Text("Final Best", overlay.transform, "0", font, 18f, TextAlignmentOptions.Center, Color.white);
            Place(overBest.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(100f, 32f), new Vector2(70f, -34f));
            Button restart = ButtonObject("Restart Button", overlay.transform, "다시 시작", font, new Color32(255, 157, 34, 255), new Vector2(175f, 52f), new Vector2(0f, -100f));
            UnityEventTools.AddPersistentListener(restart.onClick, controller.StartGame);
            overlay.gameObject.SetActive(false);

            RectTransform controls = RectObject("Controls", app);
            Place(controls, new Vector2(0.5f, 0.5f), new Vector2(620f, 125f), new Vector2(0f, -292f));
            Button jump = CircleButtonObject("Jump Button", controls, "JUMP", font, new Color32(190, 93, 25, 255), 96f, new Vector2(-145f, 14f));
            TileArenaJumpButton jumpInput = jump.gameObject.AddComponent<TileArenaJumpButton>();
            SetReference(jumpInput, "controller", controller);
            Slider bgmSlider = CreateVolumeSlider("BGM", controls, font, new Vector2(-105f, -50f), out TMP_Text bgmValue);
            Slider sfxSlider = CreateVolumeSlider("SFX", controls, font, new Vector2(145f, -50f), out TMP_Text sfxValue);
            UnityEventTools.AddPersistentListener(bgmSlider.onValueChanged, audioControls.SetMusicVolume);
            UnityEventTools.AddPersistentListener(sfxSlider.onValueChanged, audioControls.SetEffectsVolume);
            SerializedObject audioUiSerialized = new SerializedObject(audioControls);
            audioUiSerialized.FindProperty("audioController").objectReferenceValue = audioController;
            audioUiSerialized.FindProperty("soundButtonLabel").objectReferenceValue = soundLabel;
            audioUiSerialized.FindProperty("musicSlider").objectReferenceValue = bgmSlider;
            audioUiSerialized.FindProperty("musicValue").objectReferenceValue = bgmValue;
            audioUiSerialized.FindProperty("effectsSlider").objectReferenceValue = sfxSlider;
            audioUiSerialized.FindProperty("effectsValue").objectReferenceValue = sfxValue;
            audioUiSerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("board").objectReferenceValue = board;
            AssignArray(serialized.FindProperty("tiles"), tiles);
            AssignArray(serialized.FindProperty("hazards"), hazards);
            serialized.FindProperty("player").objectReferenceValue = player;
            serialized.FindProperty("avatar").objectReferenceValue = avatar;
            serialized.FindProperty("playerShadow").objectReferenceValue = shadow.rectTransform;
            serialized.FindProperty("scoreText").objectReferenceValue = score;
            serialized.FindProperty("bestText").objectReferenceValue = best;
            serialized.FindProperty("livesText").objectReferenceValue = lives;
            serialized.FindProperty("gameOverOverlay").objectReferenceValue = overlay.gameObject;
            serialized.FindProperty("gameOverScore").objectReferenceValue = overScore;
            serialized.FindProperty("gameOverBest").objectReferenceValue = overBest;
            serialized.FindProperty("startOverlay").objectReferenceValue = startOverlay.gameObject;
            serialized.FindProperty("audioController").objectReferenceValue = audioController;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log("TILE ARENA: scene built with 256 editable tiles, 256 hazards, HUD, controls, and all original stage logic.");
            EditorApplication.delayCall += SharedLiveChatPrefabBuilder.RefreshScenes;
        }

        [MenuItem("STREAM ON/Tile Arena/Repair Grid and Audio")]
        public static void UpgradeExistingScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            TileArenaController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TileArenaController>(true)).FirstOrDefault();
            if (controller == null)
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            bool changed = false;
            SerializedObject controllerSerialized = new SerializedObject(controller);
            RectTransform board = controllerSerialized.FindProperty("board").objectReferenceValue as RectTransform;
            if (board != null)
            {
                float cellSize = BoardSize / Grid;
                Transform tileLayer = board.Find("Tiles (Scene Authored 16x16)");
                Transform hazardLayer = board.Find("Hazards (Scene Authored Pool)");
                if (tileLayer != null)
                {
                    foreach (RectTransform tile in tileLayer.Cast<Transform>().Select(item => item as RectTransform).Where(item => item != null))
                    {
                        if (Vector2.Distance(tile.sizeDelta, Vector2.one * cellSize) <= 0.01f) continue;
                        tile.sizeDelta = Vector2.one * cellSize;
                        changed = true;
                    }
                }
                if (hazardLayer != null)
                {
                    foreach (RectTransform hazard in hazardLayer.Cast<Transform>().Select(item => item as RectTransform).Where(item => item != null))
                    {
                        if (Vector2.Distance(hazard.sizeDelta, Vector2.one * cellSize) <= 0.01f) continue;
                        hazard.sizeDelta = Vector2.one * cellSize;
                        changed = true;
                    }
                }
                if (board.Find("Grid Lines") == null)
                {
                    CreateGridLines(board, hazardLayer != null ? hazardLayer.GetSiblingIndex() + 1 : board.childCount);
                    changed = true;
                }
            }

            TileArenaAudioController audioController = controller.GetComponent<TileArenaAudioController>();
            if (audioController == null)
            {
                audioController = controller.gameObject.AddComponent<TileArenaAudioController>();
                changed = true;
            }
            changed |= AssignOriginalAudioClips(audioController);
            if (controller.GetComponentsInChildren<AudioSource>(true).Length < 2)
            {
                ConfigureAudioSources(controller.transform, audioController);
                changed = true;
            }
            if (!scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AudioListener>(true)).Any())
            {
                new GameObject("Audio Listener", typeof(AudioListener)).transform.SetParent(controller.transform, false);
                changed = true;
            }

            TMP_FontAsset font = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true))
                .Select(text => text.font).FirstOrDefault(item => item != null) ?? FindGalmuriFont();
            Transform hud = FindTransform(scene, "HUD");
            Transform controls = FindTransform(scene, "Controls");
            TileArenaAudioControls audioControls = controller.GetComponent<TileArenaAudioControls>();
            if (audioControls == null)
            {
                audioControls = controller.gameObject.AddComponent<TileArenaAudioControls>();
                changed = true;
            }

            GameObject startOverlayObject = board != null ? board.Find("Start Overlay")?.gameObject : null;
            if (board != null && startOverlayObject == null)
            {
                Image startOverlay = ImageObject("Start Overlay", board, new Color(8f / 255f, 11f / 255f, 24f / 255f, 0.94f));
                Stretch(startOverlay.rectTransform);
                Button startButton = ButtonObject("Start Button", startOverlay.transform, "START", font, new Color32(37, 49, 82, 255), new Vector2(180f, 56f), Vector2.zero);
                UnityEventTools.AddPersistentListener(startButton.onClick, controller.StartGame);
                startOverlayObject = startOverlay.gameObject;
                changed = true;
            }

            TMP_Text soundLabel = null;
            if (hud != null)
            {
                Transform soundTransform = hud.Find("Sound Toggle");
                Button soundButton;
                if (soundTransform == null)
                {
                    soundButton = ButtonObject("Sound Toggle", hud, "SOUND ON", font, new Color32(18, 24, 44, 255), new Vector2(92f, 32f), new Vector2(262f, -1f));
                    UnityEventTools.AddPersistentListener(soundButton.onClick, audioControls.ToggleSound);
                    changed = true;
                }
                else soundButton = soundTransform.GetComponent<Button>();
                soundLabel = soundButton != null ? soundButton.GetComponentInChildren<TMP_Text>(true) : null;
            }

            Slider bgmSlider = null;
            Slider sfxSlider = null;
            TMP_Text bgmValue = null;
            TMP_Text sfxValue = null;
            if (controls != null)
            {
                foreach (string oldName in new[] { "Up Button", "Down Button", "Left Button", "Right Button" })
                {
                    Transform old = controls.Find(oldName);
                    if (old != null) { Object.DestroyImmediate(old.gameObject); changed = true; }
                }
                Transform legacyJoystick = controls.Find("Joystick");
                if (legacyJoystick != null)
                {
                    Object.DestroyImmediate(legacyJoystick.gameObject);
                    changed = true;
                }
                Transform bgmRoot = controls.Find("BGM Volume");
                if (bgmRoot == null)
                {
                    bgmSlider = CreateVolumeSlider("BGM", controls, font, new Vector2(-105f, -50f), out bgmValue);
                    UnityEventTools.AddPersistentListener(bgmSlider.onValueChanged, audioControls.SetMusicVolume);
                    changed = true;
                }
                else
                {
                    bool alreadyMouseOnly = bgmRoot.GetComponent<TileArenaMouseDragSlider>() != null;
                    bgmSlider = EnsureMouseOnlySlider(bgmRoot);
                    changed |= !alreadyMouseOnly && bgmSlider != null;
                    bgmValue = bgmRoot.Find("Value")?.GetComponent<TMP_Text>();
                }
                Transform sfxRoot = controls.Find("SFX Volume");
                if (sfxRoot == null)
                {
                    sfxSlider = CreateVolumeSlider("SFX", controls, font, new Vector2(145f, -50f), out sfxValue);
                    UnityEventTools.AddPersistentListener(sfxSlider.onValueChanged, audioControls.SetEffectsVolume);
                    changed = true;
                }
                else
                {
                    bool alreadyMouseOnly = sfxRoot.GetComponent<TileArenaMouseDragSlider>() != null;
                    sfxSlider = EnsureMouseOnlySlider(sfxRoot);
                    changed |= !alreadyMouseOnly && sfxSlider != null;
                    sfxValue = sfxRoot.Find("Value")?.GetComponent<TMP_Text>();
                }
                if (bgmSlider != null && sfxSlider != null)
                {
                    RectTransform bgmHandle = bgmSlider.handleRect;
                    RectTransform sfxHandle = sfxSlider.handleRect;
                    if (bgmHandle != null && sfxHandle != null)
                    {
                        sfxHandle.sizeDelta = bgmHandle.sizeDelta;
                        sfxHandle.localScale = bgmHandle.localScale;
                    }
                    changed |= EnsureSliderHandleArea(bgmSlider);
                    changed |= EnsureSliderHandleArea(sfxSlider);
                }
            }

            SerializedObject audioUi = new SerializedObject(audioControls);
            audioUi.FindProperty("audioController").objectReferenceValue = audioController;
            audioUi.FindProperty("soundButtonLabel").objectReferenceValue = soundLabel;
            audioUi.FindProperty("musicSlider").objectReferenceValue = bgmSlider;
            audioUi.FindProperty("musicValue").objectReferenceValue = bgmValue;
            audioUi.FindProperty("effectsSlider").objectReferenceValue = sfxSlider;
            audioUi.FindProperty("effectsValue").objectReferenceValue = sfxValue;
            audioUi.ApplyModifiedPropertiesWithoutUndo();
            controllerSerialized.Update();
            if (controllerSerialized.FindProperty("audioController").objectReferenceValue != audioController)
            {
                controllerSerialized.FindProperty("audioController").objectReferenceValue = audioController;
                controllerSerialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            if (controllerSerialized.FindProperty("startOverlay").objectReferenceValue != startOverlayObject)
            {
                controllerSerialized.FindProperty("startOverlay").objectReferenceValue = startOverlayObject;
                controllerSerialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("TILE ARENA: replaced green tile gaps with a scene-authored gray grid and added arcade audio sources.");
            }
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            EditorApplication.delayCall += SharedLiveChatPrefabBuilder.RefreshScenes;
        }

        private static void CreateGridLines(Transform board, int siblingIndex)
        {
            GameObject gridObject = new GameObject("Grid Lines", typeof(RectTransform), typeof(CanvasRenderer), typeof(TileArenaGridGraphic));
            gridObject.transform.SetParent(board, false);
            gridObject.transform.SetSiblingIndex(siblingIndex);
            TileArenaGridGraphic grid = gridObject.GetComponent<TileArenaGridGraphic>();
            grid.color = new Color32(174, 179, 187, 255);
            grid.raycastTarget = false;
            Stretch(grid.rectTransform);
        }

        private static void ConfigureAudioSources(Transform parent, TileArenaAudioController audioController)
        {
            Transform oldRoot = parent.Find("Audio");
            if (oldRoot != null) Object.DestroyImmediate(oldRoot.gameObject);
            GameObject audioRoot = new GameObject("Audio");
            audioRoot.transform.SetParent(parent, false);
            GameObject musicObject = new GameObject("BGM Source", typeof(AudioSource));
            musicObject.transform.SetParent(audioRoot.transform, false);
            AudioSource music = musicObject.GetComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0f;
            GameObject effectsObject = new GameObject("SFX Source", typeof(AudioSource));
            effectsObject.transform.SetParent(audioRoot.transform, false);
            AudioSource effects = effectsObject.GetComponent<AudioSource>();
            effects.playOnAwake = false;
            effects.loop = false;
            effects.spatialBlend = 0f;
            SerializedObject audioSerialized = new SerializedObject(audioController);
            audioSerialized.FindProperty("musicSource").objectReferenceValue = music;
            audioSerialized.FindProperty("effectsSource").objectReferenceValue = effects;
            audioSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool AssignOriginalAudioClips(TileArenaAudioController audioController)
        {
            SerializedObject audio = new SerializedObject(audioController);
            bool changed = AssignClip(audio, "customBackgroundMusic", "Assets/tile_arena/assets/audio/bgm.mp3");
            changed |= AssignClip(audio, "customJump", "Assets/tile_arena/assets/audio/jump.mp3");
            changed |= AssignClip(audio, "customPickup", "Assets/tile_arena/assets/audio/blue-pickup.mp3");
            changed |= AssignClip(audio, "customHit", "Assets/tile_arena/assets/audio/red-hit.mp3");
            changed |= AssignClip(audio, "customStageClear", "Assets/tile_arena/assets/audio/stage-clear.mp3");
            changed |= AssignClip(audio, "customGameOver", "Assets/tile_arena/assets/audio/game-over.mp3");
            if (changed) audio.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static bool AssignClip(SerializedObject target, string propertyName, string path)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (property.objectReferenceValue == clip) return false;
            property.objectReferenceValue = clip;
            return true;
        }

        private static Transform FindTransform(Scene scene, string objectName) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).FirstOrDefault(item => item.name == objectName);

        private static Slider CreateVolumeSlider(string labelValue, Transform parent, TMP_FontAsset font, Vector2 position, out TMP_Text output)
        {
            RectTransform root = RectObject(labelValue + " Volume", parent);
            Place(root, new Vector2(0.5f, 0.5f), new Vector2(215f, 28f), position);
            TMP_Text label = Text("Label", root, labelValue, font, 12f, TextAlignmentOptions.MidlineRight, new Color32(150, 159, 190, 255));
            Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(42f, 24f), new Vector2(-84f, 0f));
            Image sliderBackground = ImageObject("Background", root, new Color32(48, 58, 96, 255));
            Place(sliderBackground.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(110f, 8f), new Vector2(0f, 0f));
            Image fill = ImageObject("Fill", sliderBackground.transform, new Color32(255, 211, 78, 255));
            Stretch(fill.rectTransform);
            RectTransform handleArea = RectObject("Handle Slide Area", root);
            Place(handleArea, new Vector2(0.5f, 0.5f), new Vector2(94f, 28f), Vector2.zero);
            TileArenaCircleGraphic handle = Circle("Handle", handleArea, new Color32(255, 230, 109, 255));
            Place(handle.rectTransform, new Vector2(0.5f, 0.5f), Vector2.one * 16f, Vector2.zero);
            handle.raycastTarget = true;
            Slider slider = root.gameObject.AddComponent<TileArenaMouseDragSlider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = labelValue == "BGM" ? 0.35f : 1f;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            Navigation navigation = slider.navigation;
            navigation.mode = Navigation.Mode.None;
            slider.navigation = navigation;
            ConfigureSliderHandleLayout(root.gameObject, sliderBackground.rectTransform, handleArea, handle.rectTransform);
            output = Text("Value", root, labelValue == "BGM" ? "35%" : "100%", font, 12f, TextAlignmentOptions.MidlineRight, Color.white);
            Place(output.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(42f, 24f), new Vector2(86f, 0f));
            return slider;
        }

        private static bool EnsureSliderHandleArea(Slider slider)
        {
            if (slider == null || slider.handleRect == null) return false;
            RectTransform root = slider.transform as RectTransform;
            RectTransform track = slider.transform.Find("Background") as RectTransform;
            if (root == null || track == null) return false;
            RectTransform handle = slider.handleRect;
            RectTransform area = slider.transform.Find("Handle Slide Area") as RectTransform;
            bool changed = false;
            if (area == null)
            {
                area = RectObject("Handle Slide Area", slider.transform);
                Place(area, new Vector2(0.5f, 0.5f), new Vector2(94f, root.rect.height), new Vector2(track.anchoredPosition.x, 0f));
                handle.SetParent(area, false);
                slider.handleRect = handle;
                changed = true;
            }
            TileArenaSliderHandleLayout layout = slider.GetComponent<TileArenaSliderHandleLayout>();
            if (layout == null) { layout = slider.gameObject.AddComponent<TileArenaSliderHandleLayout>(); changed = true; }
            ConfigureSliderHandleLayout(slider.gameObject, track, area, handle);
            Navigation navigation = slider.navigation;
            navigation.mode = Navigation.Mode.None;
            slider.navigation = navigation;
            return changed;
        }

        private static Slider EnsureMouseOnlySlider(Transform root)
        {
            Slider existing = root != null ? root.GetComponent<Slider>() : null;
            if (existing == null || existing is TileArenaMouseDragSlider) return existing;
            RectTransform fill = existing.fillRect;
            RectTransform handle = existing.handleRect;
            Graphic target = existing.targetGraphic;
            float minimum = existing.minValue;
            float maximum = existing.maxValue;
            float value = existing.value;
            bool wholeNumbers = existing.wholeNumbers;
            Slider.Direction direction = existing.direction;
            Slider.SliderEvent callbacks = existing.onValueChanged;
            Object.DestroyImmediate(existing);
            TileArenaMouseDragSlider replacement = root.gameObject.AddComponent<TileArenaMouseDragSlider>();
            replacement.fillRect = fill;
            replacement.handleRect = handle;
            replacement.targetGraphic = target;
            replacement.minValue = minimum;
            replacement.maxValue = maximum;
            replacement.wholeNumbers = wholeNumbers;
            replacement.direction = direction;
            replacement.SetValueWithoutNotify(value);
            replacement.onValueChanged = callbacks;
            Navigation navigation = replacement.navigation;
            navigation.mode = Navigation.Mode.None;
            replacement.navigation = navigation;
            return replacement;
        }

        private static void ConfigureSliderHandleLayout(GameObject owner, RectTransform track, RectTransform area, RectTransform handle)
        {
            TileArenaSliderHandleLayout layout = owner.GetComponent<TileArenaSliderHandleLayout>();
            if (layout == null) layout = owner.AddComponent<TileArenaSliderHandleLayout>();
            SerializedObject serialized = new SerializedObject(layout);
            serialized.FindProperty("track").objectReferenceValue = track;
            serialized.FindProperty("handleArea").objectReferenceValue = area;
            serialized.FindProperty("handle").objectReferenceValue = handle;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateDirectionButton(string name, string symbol, TileArenaDirection direction, TileArenaController controller, Transform parent, Vector2 position, TMP_FontAsset font)
        {
            Button button = ButtonObject(name + " Button", parent, symbol, font, new Color32(21, 28, 51, 255), new Vector2(50f, 50f), position);
            TileArenaHoldButton input = button.gameObject.AddComponent<TileArenaHoldButton>();
            SerializedObject serialized = new SerializedObject(input);
            serialized.FindProperty("controller").objectReferenceValue = controller;
            serialized.FindProperty("direction").enumValueIndex = (int)direction;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetReference(Object target, string property, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignArray<T>(SerializedProperty property, IReadOnlyList<T> values) where T : Object
        {
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static TMP_FontAsset FindGalmuriFont()
        {
            string guid = AssetDatabase.FindAssets("Galmuri14 SDF t:TMP_FontAsset").FirstOrDefault();
            return string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
        }

        private static RectTransform RectObject(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj.GetComponent<RectTransform>();
        }

        private static Image ImageObject(string name, Transform parent, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TileArenaCircleGraphic Circle(string name, Transform parent, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TileArenaCircleGraphic));
            obj.transform.SetParent(parent, false);
            TileArenaCircleGraphic circle = obj.GetComponent<TileArenaCircleGraphic>();
            circle.color = color;
            circle.raycastTarget = false;
            return circle;
        }

        private static TMP_Text Text(string name, Transform parent, string value, TMP_FontAsset font, float size, TextAlignmentOptions alignment, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.text = value;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static Button ButtonObject(string name, Transform parent, string label, TMP_FontAsset font, Color color, Vector2 size, Vector2 position)
        {
            Image image = ImageObject(name, parent, color);
            Place(image.rectTransform, new Vector2(0.5f, 0.5f), size, position);
            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
            button.colors = colors;
            TMP_Text text = Text("Label", image.transform, label, font, label.Length <= 2 ? 22f : 16f, TextAlignmentOptions.Center, Color.white);
            Stretch(text.rectTransform);
            return button;
        }

        private static Button CircleButtonObject(string name, Transform parent, string label, TMP_FontAsset font, Color color, float diameter, Vector2 position)
        {
            TileArenaCircleGraphic graphic = Circle(name, parent, color);
            graphic.raycastTarget = true;
            Place(graphic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.one * diameter, position);
            Button button = graphic.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            TMP_Text text = Text("Label", graphic.transform, label, font, 16f, TextAlignmentOptions.Center, new Color32(255, 247, 220, 255));
            Stretch(text.rectTransform);
            return button;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void PlaceTopLeft(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AddToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(item => item.path != ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
