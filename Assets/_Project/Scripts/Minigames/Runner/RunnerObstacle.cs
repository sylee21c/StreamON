using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    public enum RunnerObstacleType { Jump, Roll, Enemy }

    public sealed class RunnerObstacle : MonoBehaviour
    {
        [SerializeField] private RunnerGameManager gameManager;
        [SerializeField] private RunnerObstacleType obstacleType;
        [Tooltip("Only X/Z are applied at runtime. Spawn height comes directly from this preview object's scene Y position.")]
        [SerializeField] private Vector3 spawnOffset;
        [SerializeField] private float despawnX = -12f;
        private bool _counted;
        private float _sceneSpawnY;
        private bool _hasCapturedSceneSpawnY;

        public bool IsAvailable => !gameObject.activeSelf;
        public RunnerObstacleType ObstacleType => obstacleType;

        public void CaptureSceneSpawnHeight()
        {
            if (_hasCapturedSceneSpawnY) return;
            _sceneSpawnY = transform.position.y;
            _hasCapturedSceneSpawnY = true;
        }

        public void Activate(Vector3 position)
        {
            CaptureSceneSpawnHeight();
            transform.position = new Vector3(
                position.x + spawnOffset.x,
                _sceneSpawnY,
                position.z + spawnOffset.z);
            _counted = false;
            gameObject.SetActive(true);
        }

        public void Deactivate() => gameObject.SetActive(false);

        public bool TryDefeat()
        {
            if (obstacleType != RunnerObstacleType.Enemy || !gameObject.activeSelf) return false;
            _counted = true;
            gameManager.OnEnemyDefeated();
            Deactivate();
            return true;
        }

        private void Update()
        {
            if (gameManager.State != RunnerGameState.Playing) return;
            transform.Translate(Vector3.left * (gameManager.WorldSpeed * Time.deltaTime), Space.World);
            // Jump/Roll obstacles award a clear after passing the player. Enemies
            // must never damage the player from this position check: health damage
            // is handled exclusively by the actual collider contact in
            // RunnerPlayerController.OnTriggerEnter2D.
            if (obstacleType != RunnerObstacleType.Enemy && !_counted && transform.position.x < -5f)
            {
                _counted = true;
                gameManager.OnObstacleCleared(obstacleType);
            }
            if (transform.position.x <= despawnX) Deactivate();
        }
    }
}
