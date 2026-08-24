using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    public sealed class RunnerParallaxLayer : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private RunnerGameManager gameManager;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private SpriteRenderer sourceRenderer;
        [SerializeField] private SpriteRenderer[] tileRenderers;

        [Header("Editable Parallax")]
        [Tooltip("0은 고정 원경, 1은 지면과 같은 이동 속도입니다.")]
        [SerializeField, Min(0f)] private float parallaxScale = 0.1f;

        private float _tileLocalWidth;
        private float _alpha = 1f;

        public float ParallaxScale => parallaxScale;

        private void OnEnable()
        {
            ConfigureRenderers();
            SetAlpha(_alpha);
        }

        private void Update()
        {
            if (gameManager == null || gameManager.State != RunnerGameState.Playing
                || tileRenderers == null || tileRenderers.Length < 2 || _tileLocalWidth <= 0f) return;

            float worldScaleX = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
            float movement = gameManager.WorldSpeed * parallaxScale * Time.deltaTime / worldScaleX;
            if (movement <= 0f) return;

            foreach (SpriteRenderer tile in tileRenderers)
            {
                if (tile == null) continue;
                tile.transform.localPosition += Vector3.left * movement;
            }

            RecycleOffscreenTiles();
        }

        private void ConfigureRenderers()
        {
            if (sourceRenderer == null || sourceRenderer.sprite == null) return;
            _tileLocalWidth = Mathf.Max(0.01f, sourceRenderer.sprite.bounds.size.x);
            sourceRenderer.enabled = false;

            if (tileRenderers == null) return;
            foreach (SpriteRenderer tile in tileRenderers)
            {
                if (tile == null) continue;
                tile.sprite = sourceRenderer.sprite;
                tile.sharedMaterial = sourceRenderer.sharedMaterial;
                tile.sortingLayerID = sourceRenderer.sortingLayerID;
                tile.sortingOrder = sourceRenderer.sortingOrder;
                tile.drawMode = SpriteDrawMode.Simple;
                tile.flipX = false;
            }
        }

        private void RecycleOffscreenTiles()
        {
            Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
            float localLeft = -_tileLocalWidth;
            if (cameraToUse != null && cameraToUse.orthographic)
            {
                float worldLeft = cameraToUse.transform.position.x
                    - cameraToUse.orthographicSize * cameraToUse.aspect;
                Vector3 localPoint = transform.InverseTransformPoint(
                    new Vector3(worldLeft, transform.position.y, transform.position.z));
                localLeft = localPoint.x;
            }

            for (int pass = 0; pass < tileRenderers.Length; pass++)
            {
                SpriteRenderer leftmost = FindLeftmostTile();
                if (leftmost == null
                    || leftmost.transform.localPosition.x + _tileLocalWidth * 0.5f >= localLeft - 0.05f) return;

                SpriteRenderer rightmost = FindRightmostTile(leftmost);
                if (rightmost == null) return;
                Vector3 position = leftmost.transform.localPosition;
                position.x = rightmost.transform.localPosition.x + _tileLocalWidth;
                leftmost.transform.localPosition = position;
                leftmost.flipX = false;
            }
        }

        private SpriteRenderer FindLeftmostTile()
        {
            SpriteRenderer result = null;
            foreach (SpriteRenderer tile in tileRenderers)
                if (tile != null && (result == null
                    || tile.transform.localPosition.x < result.transform.localPosition.x))
                    result = tile;
            return result;
        }

        private SpriteRenderer FindRightmostTile(SpriteRenderer excluded)
        {
            SpriteRenderer result = null;
            foreach (SpriteRenderer tile in tileRenderers)
                if (tile != null && tile != excluded && (result == null
                    || tile.transform.localPosition.x > result.transform.localPosition.x))
                    result = tile;
            return result;
        }

        public void SetAlpha(float alpha)
        {
            _alpha = Mathf.Clamp01(alpha);
            if (tileRenderers == null) return;
            foreach (SpriteRenderer tile in tileRenderers) ApplyAlpha(tile, _alpha);
        }

        private static void ApplyAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer == null) return;
            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }

        private void OnValidate() => parallaxScale = Mathf.Max(0f, parallaxScale);
    }
}
