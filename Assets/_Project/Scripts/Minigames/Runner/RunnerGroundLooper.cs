using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    public sealed class RunnerGroundLooper : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private RunnerGameManager gameManager;
        [SerializeField] private Transform[] tiles;

        [Header("Loop Settings")]
        [SerializeField, Min(0.1f)] private float tileWidth = 16f;
        // Recycle threshold measured against a tile's RIGHT edge in world space.
        // When the right edge is fully off-screen to the left, the tile snaps
        // to the right of the current rightmost tile.
        [SerializeField] private float recycleRightEdgeX = -14f;

        private Vector3[] _startPositions;
        private float[] _widths;

        private void Awake()
        {
            CacheStartPositions();
            CacheWidths();
        }

        private void Update()
        {
            if (gameManager == null || gameManager.State != RunnerGameState.Playing || tiles == null || tiles.Length == 0)
                return;

            float movement = gameManager.WorldSpeed * Time.deltaTime;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] != null)
                    tiles[i].position += Vector3.left * movement;
            }

            // Snap-to-rightmost with the *actual* tile width to prevent floating-point
            // drift from opening a seam between chunks. Repeat as needed within a frame
            // in case a very high WorldSpeed pushed a tile past the threshold by more
            // than its own width (defensive; shouldn't happen at normal speeds).
            for (int i = 0; i < tiles.Length; i++)
            {
                Transform tile = tiles[i];
                if (tile == null) continue;

                float width = GetWidth(i);
                float rightEdge = tile.position.x + width * 0.5f;
                if (rightEdge > recycleRightEdgeX) continue;

                float rightmostCenter = GetRightmostCenter(i);
                float rightmostWidth = GetRightmostWidth(i);
                float newCenterX = rightmostCenter + (rightmostWidth + width) * 0.5f;
                tile.position = new Vector3(newCenterX, tile.position.y, tile.position.z);
            }
        }

        public void ResetTiles()
        {
            if (tiles == null || tiles.Length == 0)
                return;

            if (_startPositions == null || _startPositions.Length != tiles.Length)
                CacheStartPositions();
            if (_widths == null || _widths.Length != tiles.Length)
                CacheWidths();

            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] != null)
                    tiles[i].position = _startPositions[i];
            }

        }

        private void CacheStartPositions()
        {
            int count = tiles?.Length ?? 0;
            _startPositions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                if (tiles[i] != null)
                    _startPositions[i] = tiles[i].position;
            }
        }

        private void CacheWidths()
        {
            int count = tiles?.Length ?? 0;
            _widths = new float[count];
            for (int i = 0; i < count; i++)
                _widths[i] = MeasureWidth(tiles[i]);
        }

        private float MeasureWidth(Transform tile)
        {
            if (tile == null) return tileWidth;
            // Prefer BoxCollider2D.size — the collider defines the PHYSICAL ground width, which
            // is what recycling must preserve. Using TilemapRenderer.bounds would leak visual
            // over-paint (e.g., users painting extra scenery) into the recycle spacing and open
            // physical gaps between chunks.
            BoxCollider2D box = tile.GetComponent<BoxCollider2D>();
            if (box != null && box.size.x > 0.01f) return box.size.x * tile.lossyScale.x;
            SpriteRenderer sprite = tile.GetComponent<SpriteRenderer>();
            if (sprite != null && sprite.size.x > 0.01f) return sprite.size.x * tile.lossyScale.x;
            return tileWidth;
        }

        private float GetWidth(int index) => _widths != null && index < _widths.Length && _widths[index] > 0.01f ? _widths[index] : tileWidth;

        private float GetRightmostCenter(int excludeIndex)
        {
            float rightmostX = float.NegativeInfinity;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (i == excludeIndex || tiles[i] == null) continue;
                if (tiles[i].position.x > rightmostX)
                    rightmostX = tiles[i].position.x;
            }
            return float.IsNegativeInfinity(rightmostX) ? tiles[excludeIndex].position.x : rightmostX;
        }

        private float GetRightmostWidth(int excludeIndex)
        {
            int rightmostIndex = excludeIndex;
            float rightmostX = float.NegativeInfinity;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (i == excludeIndex || tiles[i] == null) continue;
                if (tiles[i].position.x > rightmostX)
                {
                    rightmostX = tiles[i].position.x;
                    rightmostIndex = i;
                }
            }
            return GetWidth(rightmostIndex);
        }
    }
}
