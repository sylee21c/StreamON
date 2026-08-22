#if UNITY_EDITOR
using System.IO;
using System.Linq;
using StreamOn.Minigames.Runner;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StreamOn.EditorTools
{
    [InitializeOnLoad]
    public static class RunnerAnimatorSetup
    {
        private const string ScenePath = "Assets/Scenes/BroadcastRunner.unity";
        private const string OutputFolder = "Assets/_Project/Animations/Runner";
        private const string ControllerPath = OutputFolder + "/MorRunner.controller";
        private const string EnemyControllerPath = OutputFolder + "/RunnerEnemy.controller";
        private const string SetupVersionKey = "StreamOn.RunnerAnimatorSetup.Version";
        private const int SetupVersion = 5;

        static RunnerAnimatorSetup() => EditorApplication.delayCall += SetupAfterUpgrade;

        private static void SetupAfterUpgrade()
        {
            if (EditorPrefs.GetInt(SetupVersionKey, 0) >= SetupVersion) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += SetupAfterUpgrade;
                return;
            }
            SetupOnce();
        }

        [MenuItem("STREAM ON/Setup Runner Animator")]
        public static void SetupOnce()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;

            EnsureFolder("Assets/_Project/Animations");
            EnsureFolder(OutputFolder);

            AnimationClip run = GetOrCreateClip("Mor_Run", "mor_run.png", 12f, true);
            AnimationClip jump = GetOrCreateClip("Mor_Jump", "mor_jump.png", 12f, false);
            AnimationClip hurt = GetOrCreateClip("Mor_Hurt", "mor_hit.png", 10f, false);
            AnimationClip dead = GetOrCreateClip("Mor_Dead", "mor_die.png", 10f, false);
            AnimationClip roll = GetOrCreateClip("Mor_Roll", "mor_rolling.png", 14f, false);
            AnimationClip attack = GetOrCreateClip("Mor_Attack", "mor_attack2.png", 16f, false);
            AnimationClip enemyWalk = GetOrCreateClip("Enemy_Walk", "../Enemies/mob1_walk.png", 10f, true);
            AnimatorController controller = GetOrCreateController(run, jump, hurt, dead, roll, attack);
            AnimatorController enemyController = GetOrCreateEnemyController(enemyWalk);
            AttachToScene(controller, enemyController);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorPrefs.SetInt(SetupVersionKey, SetupVersion);
            Debug.Log("STREAM ON runner Animator setup completed.");
        }

        private static AnimationClip GetOrCreateClip(string clipName, string sourceFile, float fps, bool loop)
        {
            string path = $"{OutputFolder}/{clipName}.anim";
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null) return existing;

            string spritePath = sourceFile.StartsWith("../")
                ? $"Assets/Art/Inhumania_Asset/CharacterSprite/{sourceFile.Substring(3)}"
                : $"Assets/Art/Inhumania_Asset/CharacterSprite/mor/{sourceFile}";
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(spritePath)
                .OfType<Sprite>()
                .OrderBy(FrameIndex)
                .ToArray();

            AnimationClip clip = new AnimationClip { name = clipName, frameRate = fps };
            EditorCurveBinding binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = string.Empty,
                propertyName = "m_Sprite"
            };
            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static AnimatorController GetOrCreateController(AnimationClip run, AnimationClip jump, AnimationClip hurt, AnimationClip dead, AnimationClip roll, AnimationClip attack)
        {
            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            AnimatorController controller = existing != null
                ? existing
                : AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            EnsureParameter(controller, "Grounded", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Jump", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Hurt", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Dead", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Roll", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            bool buildBaseGraph = existing == null;
            AnimatorState runState = FindOrAddState(machine, "Run", new Vector3(280f, 40f));
            AnimatorState jumpState = FindOrAddState(machine, "Jump", new Vector3(520f, -50f));
            AnimatorState hurtState = FindOrAddState(machine, "Hurt", new Vector3(520f, 130f));
            AnimatorState deadState = FindOrAddState(machine, "Dead", new Vector3(760f, 130f));
            bool addRollGraph = FindState(machine, "Roll") == null;
            bool addAttackGraph = FindState(machine, "Attack") == null;
            AnimatorState rollState = FindOrAddState(machine, "Roll", new Vector3(520f, -150f));
            AnimatorState attackState = FindOrAddState(machine, "Attack", new Vector3(520f, 230f));
            runState.motion = run; jumpState.motion = jump; hurtState.motion = hurt; deadState.motion = dead;
            rollState.motion = roll; attackState.motion = attack;
            if (buildBaseGraph) machine.defaultState = runState;

            if (buildBaseGraph)
            {
                AnimatorStateTransition toJump = runState.AddTransition(jumpState);
                Configure(toJump, false, 0f); toJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
                AnimatorStateTransition toRun = jumpState.AddTransition(runState);
                Configure(toRun, false, 0f); toRun.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
                AnimatorStateTransition toHurt = machine.AddAnyStateTransition(hurtState);
                Configure(toHurt, false, 0f); toHurt.AddCondition(AnimatorConditionMode.If, 0f, "Hurt");
                AnimatorStateTransition hurtToRun = hurtState.AddTransition(runState);
                Configure(hurtToRun, true, 0.9f);
                AnimatorStateTransition toDead = machine.AddAnyStateTransition(deadState);
                Configure(toDead, false, 0f); toDead.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            }
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions
                .Where(item => item.destinationState == deadState))
                transition.canTransitionToSelf = false;
            if (addRollGraph) AddActionGraph(machine, rollState, runState, "Roll");
            if (addAttackGraph) AddActionGraph(machine, attackState, runState, "Attack");
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorController GetOrCreateEnemyController(AnimationClip enemyWalk)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(EnemyControllerPath);
            AnimatorState state = FindOrAddState(controller.layers[0].stateMachine, "Walk", new Vector3(280f, 40f));
            state.motion = enemyWalk;
            controller.layers[0].stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AttachToScene(AnimatorController controller, AnimatorController enemyController)
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            RunnerPlayerController player = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RunnerPlayerController>(true))
                .FirstOrDefault();
            if (player == null) return;
            Transform visual = player.transform.Find("Mor Visual");
            if (visual == null) return;

            Animator animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            SerializedObject playerObject = new SerializedObject(player);
            playerObject.FindProperty("animator").objectReferenceValue = animator;
            playerObject.FindProperty("groundLayer").intValue = 1 << 6;
            playerObject.FindProperty("attackHitbox").objectReferenceValue = EnsureAttackHitbox(player.transform);
            playerObject.FindProperty("attackCooldownFrames").intValue = 12;
            playerObject.FindProperty("attackInputBufferFrames").intValue = 4;
            playerObject.FindProperty("attackAnimationFramesPerSecond").floatValue = 16f;
            playerObject.FindProperty("minimumAttackCooldownFrames").intValue = 6;
            playerObject.FindProperty("maxHealth").intValue = 5;
            playerObject.ApplyModifiedPropertiesWithoutUndo();

            Sprite enemySprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Inhumania_Asset/CharacterSprite/Enemies/mob1_walk.png")
                .OfType<Sprite>().OrderBy(FrameIndex).FirstOrDefault();
            RunnerObstacle[] obstacles = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RunnerObstacle>(true))
                .OrderBy(item => item.name).ToArray();
            foreach (RunnerObstacle obstacle in obstacles)
                ConfigureObstacle(obstacle, obstacle.ObstacleType, enemySprite, enemyController);
            EnsureEditableSpawnerPools(scene, obstacles);

            Transform controls = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "Controls");
            TMP_Text guide = controls != null ? controls.GetComponentInChildren<TMP_Text>(true) : null;
            if (controls is RectTransform controlsRect) controlsRect.sizeDelta = new Vector2(700f, 58f);
            if (guide != null)
            {
                guide.text = "SPACE / ↑  점프     C / ↓  구르기     좌클릭  공격";
                guide.rectTransform.sizeDelta = new Vector2(670f, 45f);
            }
            EnsureSceneAuthoredGameplayUi(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }

        private static void EnsureEditableSpawnerPools(Scene scene, RunnerObstacle[] obstacles)
        {
            RunnerObstacleSpawner spawner = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RunnerObstacleSpawner>(true))
                .FirstOrDefault();
            if (spawner == null) return;

            SerializedObject serialized = new SerializedObject(spawner);
            SerializedProperty jumpPool = serialized.FindProperty("jumpObstacles");
            SerializedProperty rollPool = serialized.FindProperty("rollObstacles");
            SerializedProperty enemyPool = serialized.FindProperty("enemyObstacles");
            if (jumpPool == null || rollPool == null || enemyPool == null) return;
            if (jumpPool.arraySize > 0 || rollPool.arraySize > 0 || enemyPool.arraySize > 0) return;

            RunnerObstacle[] jumps = obstacles.Where(item => item.ObstacleType == RunnerObstacleType.Jump).ToArray();
            RunnerObstacle[] rolls = obstacles.Where(item => item.ObstacleType == RunnerObstacleType.Roll).ToArray();
            RunnerObstacle[] enemies = obstacles.Where(item => item.ObstacleType == RunnerObstacleType.Enemy).ToArray();
            SetObstacleArray(jumpPool, jumps);
            SetObstacleArray(rollPool, rolls);
            SetObstacleArray(enemyPool, enemies);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // The objects in these pools are scene-authored previews. Their names,
            // positions, scale, visuals and colliders belong to the designer and must
            // never be normalized by this repair/setup pass.
        }

        private static BoxCollider2D EnsureAttackHitbox(Transform player)
        {
            Transform existing = player.Find("Attack Hitbox");
            GameObject hitboxObject = existing != null ? existing.gameObject : new GameObject("Attack Hitbox");
            if (existing == null) hitboxObject.transform.SetParent(player, false);
            BoxCollider2D hitbox = hitboxObject.GetComponent<BoxCollider2D>();
            if (hitbox == null)
            {
                hitbox = hitboxObject.AddComponent<BoxCollider2D>();
                hitbox.size = new Vector2(3f, 1.4f);
                hitbox.offset = new Vector2(1.8f, 0.15f);
            }
            hitbox.isTrigger = true;
            hitbox.enabled = false;
            return hitbox;
        }

        private static void SetObstacleArray(SerializedProperty property, RunnerObstacle[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void ConfigureObstacle(RunnerObstacle obstacle, RunnerObstacleType type, Sprite enemySprite, AnimatorController enemyController)
        {
            // Existing obstacle objects are deliberately configured in the scene.
            // Only fill missing enemy animation references; never overwrite authored
            // transform, sprite, tint, collider, spawn offset, or animator settings.
            if (type != RunnerObstacleType.Enemy) return;

            SpriteRenderer renderer = obstacle.GetComponent<SpriteRenderer>();
            Animator enemyAnimator = obstacle.GetComponent<Animator>();
            bool changed = false;
            if (renderer != null && renderer.sprite == null && enemySprite != null)
            {
                renderer.sprite = enemySprite;
                changed = true;
            }
            if (enemyAnimator == null)
            {
                enemyAnimator = obstacle.gameObject.AddComponent<Animator>();
                changed = true;
            }
            if (enemyAnimator.runtimeAnimatorController == null && enemyController != null)
            {
                enemyAnimator.runtimeAnimatorController = enemyController;
                enemyAnimator.enabled = true;
                changed = true;
            }
            if (changed) EditorUtility.SetDirty(obstacle.gameObject);
        }

        private static void EnsureSceneAuthoredGameplayUi(Scene scene)
        {
            Canvas canvas = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).FirstOrDefault();
            RunnerHUD hud = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RunnerHUD>(true)).FirstOrDefault();
            RunnerPauseController pause = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RunnerPauseController>(true)).FirstOrDefault();
            if (canvas == null || hud == null || pause == null) return;

            TMP_FontAsset font = canvas.GetComponentsInChildren<TMP_Text>(true)
                .Select(text => text.font).FirstOrDefault(value => value != null);
            TMP_Text speed = hud.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.name == "Speed");
            TMP_Text health = hud.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.name == "Health");
            TMP_Text broadcastTime = hud.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text => text.name == "Broadcast Time");
            if (broadcastTime == null)
            {
                broadcastTime = CreatePauseText(hud.transform, "Broadcast Time", "STREAM  01:30", font, 19f,
                    new Vector2(170f, 50f), new Vector2(220f, 0f));
                if (speed != null)
                {
                    speed.rectTransform.sizeDelta = new Vector2(150f, 50f);
                    speed.rectTransform.anchoredPosition = new Vector2(70f, 0f);
                }
                if (health != null)
                {
                    health.text = "HP  ♥♥♥♥♥";
                    health.rectTransform.sizeDelta = new Vector2(190f, 50f);
                    health.rectTransform.anchoredPosition = new Vector2(365f, 0f);
                }
            }
            SerializedObject hudSerialized = new SerializedObject(hud);
            hudSerialized.FindProperty("broadcastTimeText").objectReferenceValue = broadcastTime;
            hudSerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject pausePanel = EnsurePausePanel(canvas.transform, "Pause Menu", new Color(0.025f, 0.035f, 0.06f, 0.94f));
            if (pausePanel.transform.Find("Title") == null)
            {
                CreatePauseText(pausePanel.transform, "Title", "일시정지", font, 44f, new Vector2(520f, 70f), new Vector2(0f, 155f));
                CreatePauseButton(pausePanel.transform, "Continue Button", "계속하기", font, new Vector2(0f, 55f));
                CreatePauseButton(pausePanel.transform, "Settings Button", "설정", font, new Vector2(0f, -20f));
                CreatePauseButton(pausePanel.transform, "Manual Save Button", "수동 저장", font, new Vector2(0f, -95f));
                CreatePauseButton(pausePanel.transform, "Main Menu Button", "메인 화면", font, new Vector2(0f, -170f));
            }

            GameObject settingsPanel = EnsurePausePanel(canvas.transform, "Settings Menu", new Color(0.025f, 0.035f, 0.06f, 0.97f));
            if (settingsPanel.transform.Find("Title") == null)
            {
                CreatePauseText(settingsPanel.transform, "Title", "설정", font, 42f, new Vector2(520f, 65f), new Vector2(0f, 170f));
                CreatePauseText(settingsPanel.transform, "Volume Label", "전체 음량  100%", font, 24f, new Vector2(520f, 45f), new Vector2(0f, 85f));
                CreatePauseSlider(settingsPanel.transform, new Vector2(0f, 35f));
                CreatePauseText(settingsPanel.transform, "AI Label", "AI 채팅  ON", font, 24f, new Vector2(520f, 45f), new Vector2(0f, -40f));
                CreatePauseButton(settingsPanel.transform, "AI Toggle Button", "AI 채팅 전환", font, new Vector2(0f, -95f));
                CreatePauseButton(settingsPanel.transform, "Back Button", "뒤로", font, new Vector2(0f, -170f));
            }

            TMP_Text countdown = canvas.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text => text.name == "Resume Countdown");
            if (countdown == null)
            {
                countdown = CreatePauseText(canvas.transform, "Resume Countdown", "3", font, 110f,
                    new Vector2(500f, 180f), Vector2.zero);
                countdown.fontStyle = FontStyles.Bold;
            }

            SerializedObject pauseSerialized = new SerializedObject(pause);
            pauseSerialized.FindProperty("pausePanel").objectReferenceValue = pausePanel;
            pauseSerialized.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            pauseSerialized.FindProperty("countdownText").objectReferenceValue = countdown;
            pauseSerialized.FindProperty("volumeLabel").objectReferenceValue = settingsPanel.transform.Find("Volume Label")?.GetComponent<TMP_Text>();
            pauseSerialized.FindProperty("aiLabel").objectReferenceValue = settingsPanel.transform.Find("AI Label")?.GetComponent<TMP_Text>();
            pauseSerialized.FindProperty("volumeSlider").objectReferenceValue = settingsPanel.GetComponentInChildren<Slider>(true);
            pauseSerialized.FindProperty("resumeButton").objectReferenceValue = pausePanel.transform.Find("Continue Button")?.GetComponent<Button>();
            pauseSerialized.FindProperty("settingsButton").objectReferenceValue = pausePanel.transform.Find("Settings Button")?.GetComponent<Button>();
            pauseSerialized.FindProperty("manualSaveButton").objectReferenceValue = pausePanel.transform.Find("Manual Save Button")?.GetComponent<Button>();
            pauseSerialized.FindProperty("mainMenuButton").objectReferenceValue = pausePanel.transform.Find("Main Menu Button")?.GetComponent<Button>();
            pauseSerialized.FindProperty("aiToggleButton").objectReferenceValue = settingsPanel.transform.Find("AI Toggle Button")?.GetComponent<Button>();
            pauseSerialized.FindProperty("settingsBackButton").objectReferenceValue = settingsPanel.transform.Find("Back Button")?.GetComponent<Button>();
            pauseSerialized.ApplyModifiedPropertiesWithoutUndo();

            pausePanel.SetActive(false);
            settingsPanel.SetActive(false);
            countdown.gameObject.SetActive(false);
        }

        private static GameObject EnsurePausePanel(Transform parent, string name, Color color)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static TMP_Text CreatePauseText(Transform parent, string name, string value, TMP_FontAsset font,
            float fontSize, Vector2 size, Vector2 position)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.text = value;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = size;
            text.rectTransform.anchoredPosition = position;
            return text;
        }

        private static Button CreatePauseButton(Transform parent, string name, string label, TMP_FontAsset font, Vector2 position)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(320f, 56f);
            rect.anchoredPosition = position;
            obj.GetComponent<Image>().color = new Color(0.13f, 0.52f, 0.58f, 1f);
            CreatePauseText(obj.transform, "Label", label, font, 25f, rect.sizeDelta, Vector2.zero);
            return obj.GetComponent<Button>();
        }

        private static Slider CreatePauseSlider(Transform parent, Vector2 position)
        {
            GameObject root = new GameObject("Master Volume", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(360f, 24f);
            rect.anchoredPosition = position;

            GameObject background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.transform.SetParent(root.transform, false);
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.35f);
            backgroundRect.anchorMax = new Vector2(1f, 0.65f);
            backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0.2f, 0.22f, 0.27f, 1f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(root.transform, false);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0.35f);
            fillRect.anchorMax = new Vector2(1f, 0.65f);
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.2f, 0.8f, 0.72f, 1f);

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.transform.SetParent(root.transform, false);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(24f, 24f);
            handle.GetComponent<Image>().color = Color.white;

            Slider slider = root.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        private static void AddActionGraph(AnimatorStateMachine machine, AnimatorState action, AnimatorState run, string parameter)
        {
            AnimatorStateTransition enter = machine.AddAnyStateTransition(action);
            Configure(enter, false, 0f);
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, parameter);
            AnimatorStateTransition exit = action.AddTransition(run);
            Configure(exit, true, 0.95f);
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string name) =>
            machine.states.Select(child => child.state).FirstOrDefault(state => state.name == name);

        private static AnimatorState FindOrAddState(AnimatorStateMachine machine, string name, Vector3 position) =>
            FindState(machine, name) ?? machine.AddState(name, position);

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (!controller.parameters.Any(parameter => parameter.name == name)) controller.AddParameter(name, type);
        }

        private static void Configure(AnimatorStateTransition transition, bool exitTime, float normalizedExit)
        {
            transition.hasExitTime = exitTime;
            transition.exitTime = normalizedExit;
            transition.hasFixedDuration = true;
            transition.duration = 0f;
        }

        private static int FrameIndex(Sprite sprite)
        {
            int separator = sprite.name.LastIndexOf('_');
            return separator >= 0 && int.TryParse(sprite.name.Substring(separator + 1), out int index) ? index : 0;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
