using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    public sealed class RunnerObstacleSpawner : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private RunnerGameManager gameManager;
        [SerializeField] private Transform spawnPoint;
        [Tooltip("씬에 복제해 둔 RunnerObstacle을 종류별 풀에 자동으로 포함합니다.")]
        [SerializeField] private bool autoIncludeSceneObstacles = true;

        [Header("Jump Obstacles (Space / Up)")]
        [SerializeField] private RunnerObstacle[] jumpObstacles;
        [SerializeField, Min(0f)] private float jumpSpawnWeight = 1f;
        [Tooltip("이 점수 전에는 등장하지 않는 점프 장애물 풀입니다.")]
        [SerializeField] private RunnerObstacle[] scoreGatedJumpObstacles;
        [SerializeField, Min(0)] private int scoreGatedJumpUnlockScore = 1100;

        [Header("Roll Obstacles (C / Down)")]
        [SerializeField] private RunnerObstacle[] rollObstacles;
        [SerializeField, Min(0f)] private float rollSpawnWeight = 1f;

        [Header("Enemies (Left Click)")]
        [SerializeField] private RunnerObstacle[] enemyObstacles;
        [SerializeField, Min(0f)] private float enemySpawnWeight = 1f;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float initialSpawnDelay = 1.5f;
        [Tooltip("첫 장애물도 매번 같은 박자로 나오지 않도록 초기 지연에 더하거나 빼는 범위입니다.")]
        [SerializeField, Min(0f)] private float initialSpawnDelayRandomness = 0.65f;
        [SerializeField] private float minimumSpawnDelay = 1.35f;
        [SerializeField] private float maximumSpawnDelay = 3.4f;

        [Header("Campaign Difficulty")]
        [SerializeField, Range(0.25f, 1f)] private float minimumSpawnDelayMultiplier = 0.7f;

        private float _timer;
        private float _spawnDelayMultiplier = 1f;

        public bool HasActiveEnemy
        {
            get
            {
                if (enemyObstacles == null) return false;
                foreach (RunnerObstacle obstacle in enemyObstacles)
                    if (obstacle != null && !obstacle.IsAvailable) return true;
                return false;
            }
        }

        private void Awake()
        {
            if (autoIncludeSceneObstacles)
                IncludeSceneObstacles();

            CaptureSceneSpawnHeights(jumpObstacles);
            CaptureSceneSpawnHeights(rollObstacles);
            CaptureSceneSpawnHeights(enemyObstacles);

            // Preview objects stay visible while editing the scene, but every
            // obstacle must begin inactive at runtime and be released only by
            // the spawn timer. This also prevents newly duplicated previews
            // from rushing the player together on the first frame.
            DeactivatePool(jumpObstacles);
            DeactivatePool(rollObstacles);
            DeactivatePool(enemyObstacles);
        }

        private void Update()
        {
            if (gameManager.State != RunnerGameState.Playing) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            SpawnAvailable();
            _timer = Random.Range(minimumSpawnDelay, maximumSpawnDelay) * _spawnDelayMultiplier;
        }

        public void ResetRun()
        {
            DeactivatePool(jumpObstacles);
            DeactivatePool(rollObstacles);
            DeactivatePool(enemyObstacles);
            float initialMinimum = Mathf.Max(0.25f, initialSpawnDelay - initialSpawnDelayRandomness);
            float initialMaximum = Mathf.Max(initialMinimum, initialSpawnDelay + initialSpawnDelayRandomness);
            _timer = Random.Range(initialMinimum, initialMaximum) * _spawnDelayMultiplier;
        }

        public void ConfigureDifficulty(float normalizedDifficulty)
        {
            _spawnDelayMultiplier = Mathf.Lerp(1f, minimumSpawnDelayMultiplier, Mathf.Clamp01(normalizedDifficulty));
        }

        private void SpawnAvailable()
        {
            float jumpWeight = HasAvailable(jumpObstacles, RunnerObstacleType.Jump) ? jumpSpawnWeight : 0f;
            float rollWeight = HasAvailable(rollObstacles, RunnerObstacleType.Roll) ? rollSpawnWeight : 0f;
            float enemyWeight = HasAvailable(enemyObstacles, RunnerObstacleType.Enemy) ? enemySpawnWeight : 0f;
            float totalWeight = jumpWeight + rollWeight + enemyWeight;
            if (totalWeight <= 0f) return;

            float choice = Random.value * totalWeight;
            if (choice < jumpWeight)
                SpawnFrom(jumpObstacles, RunnerObstacleType.Jump);
            else if (choice < jumpWeight + rollWeight)
                SpawnFrom(rollObstacles, RunnerObstacleType.Roll);
            else
                SpawnFrom(enemyObstacles, RunnerObstacleType.Enemy);
        }

        private void SpawnFrom(RunnerObstacle[] pool, RunnerObstacleType expectedType)
        {
            if (pool == null || pool.Length == 0) return;
            int start = Random.Range(0, pool.Length);
            for (int i = 0; i < pool.Length; i++)
            {
                RunnerObstacle obstacle = pool[(start + i) % pool.Length];
                if (!IsAvailableForSpawn(obstacle, expectedType)) continue;
                obstacle.Activate(spawnPoint.position);
                return;
            }
        }

        private bool HasAvailable(RunnerObstacle[] pool, RunnerObstacleType expectedType)
        {
            if (pool == null) return false;
            foreach (RunnerObstacle obstacle in pool)
                if (IsAvailableForSpawn(obstacle, expectedType))
                    return true;
            return false;
        }

        private bool IsAvailableForSpawn(RunnerObstacle obstacle, RunnerObstacleType expectedType)
        {
            if (obstacle == null || obstacle.ObstacleType != expectedType || !obstacle.IsAvailable) return false;
            if (gameManager == null || gameManager.Score >= scoreGatedJumpUnlockScore
                || scoreGatedJumpObstacles == null) return true;

            foreach (RunnerObstacle gatedObstacle in scoreGatedJumpObstacles)
                if (gatedObstacle == obstacle)
                    return false;
            return true;
        }

        private static void DeactivatePool(RunnerObstacle[] pool)
        {
            if (pool == null) return;
            foreach (RunnerObstacle obstacle in pool)
                if (obstacle != null)
                    obstacle.Deactivate();
        }

        private static void CaptureSceneSpawnHeights(RunnerObstacle[] pool)
        {
            if (pool == null) return;
            foreach (RunnerObstacle obstacle in pool)
                if (obstacle != null)
                    obstacle.CaptureSceneSpawnHeight();
        }

        private void IncludeSceneObstacles()
        {
            RunnerObstacle[] sceneObstacles = FindObjectsByType<RunnerObstacle>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (RunnerObstacle obstacle in sceneObstacles)
            {
                if (obstacle == null || obstacle.gameObject.scene != gameObject.scene) continue;

                switch (obstacle.ObstacleType)
                {
                    case RunnerObstacleType.Jump:
                        AddUnique(ref jumpObstacles, obstacle);
                        break;
                    case RunnerObstacleType.Roll:
                        AddUnique(ref rollObstacles, obstacle);
                        break;
                    case RunnerObstacleType.Enemy:
                        AddUnique(ref enemyObstacles, obstacle);
                        break;
                }
            }
        }

        private static void AddUnique(ref RunnerObstacle[] pool, RunnerObstacle obstacle)
        {
            if (pool != null)
            {
                foreach (RunnerObstacle existing in pool)
                    if (existing == obstacle)
                        return;
            }

            int oldLength = pool != null ? pool.Length : 0;
            System.Array.Resize(ref pool, oldLength + 1);
            pool[oldLength] = obstacle;
        }

        private void OnValidate()
        {
            minimumSpawnDelay = Mathf.Max(0.05f, minimumSpawnDelay);
            maximumSpawnDelay = Mathf.Max(minimumSpawnDelay, maximumSpawnDelay);
            initialSpawnDelay = Mathf.Max(0f, initialSpawnDelay);
            initialSpawnDelayRandomness = Mathf.Max(0f, initialSpawnDelayRandomness);
            scoreGatedJumpUnlockScore = Mathf.Max(0, scoreGatedJumpUnlockScore);
        }
    }
}
