using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    public sealed class RunnerObstacleSpawner : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private RunnerGameManager gameManager;
        [SerializeField] private Transform spawnPoint;

        [Header("Jump Obstacles (Space / Up)")]
        [SerializeField] private RunnerObstacle[] jumpObstacles;
        [SerializeField, Min(0f)] private float jumpSpawnWeight = 1f;

        [Header("Roll Obstacles (C / Down)")]
        [SerializeField] private RunnerObstacle[] rollObstacles;
        [SerializeField, Min(0f)] private float rollSpawnWeight = 1f;

        [Header("Enemies (Left Click)")]
        [SerializeField] private RunnerObstacle[] enemyObstacles;
        [SerializeField, Min(0f)] private float enemySpawnWeight = 1f;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float initialSpawnDelay = 1.5f;
        [SerializeField] private float minimumSpawnDelay = 1.4f;
        [SerializeField] private float maximumSpawnDelay = 2.6f;

        private float _timer;

        private void Awake()
        {
            CaptureSceneSpawnHeights(jumpObstacles);
            CaptureSceneSpawnHeights(rollObstacles);
            CaptureSceneSpawnHeights(enemyObstacles);
        }

        private void Update()
        {
            if (gameManager.State != RunnerGameState.Playing) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            SpawnAvailable();
            _timer = Random.Range(minimumSpawnDelay, maximumSpawnDelay);
        }

        public void ResetRun()
        {
            DeactivatePool(jumpObstacles);
            DeactivatePool(rollObstacles);
            DeactivatePool(enemyObstacles);
            _timer = initialSpawnDelay;
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
                if (obstacle == null || obstacle.ObstacleType != expectedType || !obstacle.IsAvailable) continue;
                obstacle.Activate(spawnPoint.position);
                return;
            }
        }

        private static bool HasAvailable(RunnerObstacle[] pool, RunnerObstacleType expectedType)
        {
            if (pool == null) return false;
            foreach (RunnerObstacle obstacle in pool)
                if (obstacle != null && obstacle.ObstacleType == expectedType && obstacle.IsAvailable)
                    return true;
            return false;
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

        private void OnValidate()
        {
            minimumSpawnDelay = Mathf.Max(0.05f, minimumSpawnDelay);
            maximumSpawnDelay = Mathf.Max(minimumSpawnDelay, maximumSpawnDelay);
        }
    }
}
