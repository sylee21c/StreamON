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
        private const int SetupVersion = 2;

        static RunnerAnimatorSetup() => EditorApplication.delayCall += SetupAfterUpgrade;

        private static void SetupAfterUpgrade()
        {
            if (EditorPrefs.GetInt(SetupVersionKey, 0) >= SetupVersion) return;
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

            int previewIndex = 0;
            NameAndPlacePreviews(jumps, "Jump Obstacle Preview", ref previewIndex);
            NameAndPlacePreviews(rolls, "Roll Obstacle Preview", ref previewIndex);
            NameAndPlacePreviews(enemies, "Enemy Preview", ref previewIndex);
            if (obstacles.Length > 0 && obstacles[0].transform.parent != null)
                obstacles[0].transform.parent.name = "Obstacle Previews & Pools";
        }

        private static void SetObstacleArray(SerializedProperty property, RunnerObstacle[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void NameAndPlacePreviews(RunnerObstacle[] values, string baseName, ref int previewIndex)
        {
            for (int i = 0; i < values.Length; i++)
            {
                RunnerObstacle obstacle = values[i];
                obstacle.name = values.Length == 1 ? baseName : $"{baseName} {i + 1}";
                Vector3 position = obstacle.transform.position;
                position.x = 13f + previewIndex * 2f;
                obstacle.transform.position = position;
                obstacle.gameObject.SetActive(true);
                previewIndex++;
            }
        }

        private static void ConfigureObstacle(RunnerObstacle obstacle, RunnerObstacleType type, Sprite enemySprite, AnimatorController enemyController)
        {
            SerializedObject serialized = new SerializedObject(obstacle);
            serialized.FindProperty("obstacleType").enumValueIndex = (int)type;
            // Every obstacle now takes its final spawn height from the preview object's scene Y.
            serialized.FindProperty("spawnOffset").vector3Value = Vector3.zero;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SpriteRenderer renderer = obstacle.GetComponent<SpriteRenderer>();
            BoxCollider2D hitbox = obstacle.GetComponent<BoxCollider2D>();
            Animator enemyAnimator = obstacle.GetComponent<Animator>();
            if (type == RunnerObstacleType.Enemy)
            {
                obstacle.transform.localScale = new Vector3(3.2f, 3.2f, 1f);
                renderer.sprite = enemySprite;
                renderer.color = Color.white;
                hitbox.size = new Vector2(0.4f, 0.62f);
                hitbox.offset = new Vector2(0f, 0.08f);
                if (enemyAnimator == null) enemyAnimator = obstacle.gameObject.AddComponent<Animator>();
                enemyAnimator.runtimeAnimatorController = enemyController;
                enemyAnimator.enabled = true;
            }
            else
            {
                obstacle.transform.localScale = type == RunnerObstacleType.Roll
                    ? new Vector3(1.8f, 0.8f, 1f)
                    : new Vector3(1.2f, 2.2f, 1f);
                renderer.color = type == RunnerObstacleType.Roll ? new Color(1f, 0.68f, 0.2f) : new Color(0.92f, 0.31f, 0.32f);
                hitbox.size = Vector2.one;
                hitbox.offset = Vector2.zero;
                if (enemyAnimator != null) enemyAnimator.enabled = false;
            }
            EditorUtility.SetDirty(obstacle.gameObject);
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
