using UnityEngine;
using UnityEngine.Tilemaps;

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
        private Transform[] _visualColumns;
        private Vector3[] _visualStartPositions;
        private float _visualColumnWidth = 1f;
        private float _visualWrapWidth;
        private float _visualLeftThreshold;
        private const int VisualColumnCount = 48;
        private const string VisualRootName = "__Continuous Ground Visuals";

        private void Awake()
        {
            CacheStartPositions();
            CacheWidths();
            BuildContinuousVisualStrip();
        }

        private void Update()
        {
            if (gameManager == null || gameManager.State != RunnerGameState.Playing || tiles == null || tiles.Length == 0)
                return;

            float movement = gameManager.WorldSpeed * Time.deltaTime;
            MoveVisualStrip(movement);
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

        private void BuildContinuousVisualStrip()
        {
            if (tiles == null) return;

            Tilemap sourceTilemap = null;
            TilemapRenderer sourceRenderer = null;
            Sprite surfaceSprite = null;
            Sprite fillSprite = null;
            Color surfaceColor = Color.white;
            Color fillColor = Color.white;
            Vector3 surfaceWorldCenter = Vector3.zero;
            Vector3 fillWorldCenter = Vector3.zero;
            int highestCellY = int.MinValue;
            int fillCellY = int.MinValue;

            foreach (Transform chunk in tiles)
            {
                if (chunk == null) continue;
                Tilemap tilemap = chunk.GetComponentInChildren<Tilemap>(true);
                TilemapRenderer tilemapRenderer = chunk.GetComponentInChildren<TilemapRenderer>(true);
                if (tilemap == null || tilemapRenderer == null) continue;

                sourceTilemap ??= tilemap;
                sourceRenderer ??= tilemapRenderer;
                foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
                {
                    Sprite sprite = tilemap.GetSprite(cell);
                    if (sprite == null) continue;

                    if (cell.y > highestCellY)
                    {
                        if (surfaceSprite != null)
                        {
                            fillSprite = surfaceSprite;
                            fillColor = surfaceColor;
                            fillWorldCenter = surfaceWorldCenter;
                            fillCellY = highestCellY;
                        }
                        highestCellY = cell.y;
                        surfaceSprite = sprite;
                        surfaceColor = tilemap.GetColor(cell);
                        surfaceWorldCenter = tilemap.transform.TransformPoint(tilemap.GetCellCenterLocal(cell));
                    }
                    else if (cell.y < highestCellY && cell.y > fillCellY)
                    {
                        fillCellY = cell.y;
                        fillSprite = sprite;
                        fillColor = tilemap.GetColor(cell);
                        fillWorldCenter = tilemap.transform.TransformPoint(tilemap.GetCellCenterLocal(cell));
                    }
                }

            }

            if (sourceTilemap == null || sourceRenderer == null || surfaceSprite == null)
                return;

            foreach (Transform chunk in tiles)
            {
                if (chunk == null) continue;
                TilemapRenderer renderer = chunk.GetComponentInChildren<TilemapRenderer>(true);
                if (renderer != null) renderer.enabled = false;
            }

            if (fillSprite == null)
            {
                fillSprite = surfaceSprite;
                fillColor = surfaceColor;
                fillWorldCenter = surfaceWorldCenter + Vector3.down;
            }

            Transform oldRoot = transform.Find(VisualRootName);
            if (oldRoot != null) Destroy(oldRoot.gameObject);

            GameObject rootObject = new GameObject(VisualRootName);
            Transform visualRoot = rootObject.transform;
            visualRoot.SetParent(transform, false);

            Vector3 cellZero = sourceTilemap.CellToWorld(Vector3Int.zero);
            Vector3 cellRight = sourceTilemap.CellToWorld(Vector3Int.right);
            _visualColumnWidth = Mathf.Max(0.01f, Mathf.Abs(cellRight.x - cellZero.x));
            _visualWrapWidth = VisualColumnCount * _visualColumnWidth;
            float firstCenterX = -_visualWrapWidth * 0.5f + _visualColumnWidth * 0.5f;
            _visualLeftThreshold = firstCenterX - _visualColumnWidth;

            float surfaceLocalY = transform.InverseTransformPoint(surfaceWorldCenter).y;
            float fillLocalY = transform.InverseTransformPoint(fillWorldCenter).y;
            _visualColumns = new Transform[VisualColumnCount];
            _visualStartPositions = new Vector3[VisualColumnCount];

            for (int i = 0; i < VisualColumnCount; i++)
            {
                GameObject columnObject = new GameObject($"Ground Column {i + 1}");
                Transform column = columnObject.transform;
                column.SetParent(visualRoot, false);
                column.localPosition = new Vector3(firstCenterX + i * _visualColumnWidth, surfaceLocalY, 0f);
                _visualColumns[i] = column;
                _visualStartPositions[i] = column.localPosition;

                CreateVisualSprite("Surface", column, surfaceSprite, surfaceColor, 0f, sourceRenderer);
                CreateVisualSprite("Fill", column, fillSprite, fillColor, fillLocalY - surfaceLocalY, sourceRenderer);
            }

            Debug.Log($"STREAM ON: continuous ground visual strip ready ({VisualColumnCount} columns, wrap {_visualWrapWidth:0.##}).", this);
        }

        private static void CreateVisualSprite(string objectName, Transform parent, Sprite sprite, Color color,
            float localY, TilemapRenderer sourceRenderer)
        {
            GameObject spriteObject = new GameObject(objectName);
            spriteObject.transform.SetParent(parent, false);
            spriteObject.transform.localPosition = new Vector3(0f, localY, 0f);
            SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = sourceRenderer.sortingOrder;
            renderer.maskInteraction = sourceRenderer.maskInteraction;
        }

        private void MoveVisualStrip(float movement)
        {
            if (_visualColumns == null || _visualWrapWidth <= 0f) return;
            foreach (Transform column in _visualColumns)
            {
                if (column == null) continue;
                Vector3 position = column.localPosition;
                position.x -= movement;
                while (position.x < _visualLeftThreshold)
                    position.x += _visualWrapWidth;
                column.localPosition = position;
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

            if (_visualColumns != null && _visualStartPositions != null)
            {
                for (int i = 0; i < _visualColumns.Length && i < _visualStartPositions.Length; i++)
                    if (_visualColumns[i] != null)
                        _visualColumns[i].localPosition = _visualStartPositions[i];
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
