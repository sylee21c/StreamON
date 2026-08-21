using UnityEngine;

namespace StreamOn.Minigames.TileArena
{
    [ExecuteAlways]
    public sealed class TileArenaSliderHandleLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform track;
        [SerializeField] private RectTransform handleArea;
        [SerializeField] private RectTransform handle;

        private Vector3 _lastHandleScale;
        private Vector2 _lastHandleSize;
        private Vector2 _lastTrackSize;

        private void OnEnable() => RefreshLayout(true);
        private void OnValidate() => RefreshLayout(true);
        private void Update() => RefreshLayout(false);

        private void RefreshLayout(bool force)
        {
            if (track == null || handleArea == null || handle == null) return;
            Vector2 trackSize = track.rect.size;
            handle.anchorMin = new Vector2(handle.anchorMin.x, 0.5f);
            handle.anchorMax = new Vector2(handle.anchorMax.x, 0.5f);
            float diameter = Mathf.Max(1f, handle.sizeDelta.x);
            handle.sizeDelta = new Vector2(diameter, diameter);
            handle.localScale = Vector3.one;
            Vector2 handleSize = handle.rect.size;
            Vector3 handleScale = handle.localScale;
            if (!force && trackSize == _lastTrackSize && handleSize == _lastHandleSize && handleScale == _lastHandleScale) return;

            float visualHandleWidth = Mathf.Abs(handleSize.x * handleScale.x);
            handleArea.anchorMin = handleArea.anchorMax = handleArea.pivot = new Vector2(0.5f, 0.5f);
            handleArea.anchoredPosition = new Vector2(track.anchoredPosition.x, 0f);
            handleArea.sizeDelta = new Vector2(Mathf.Max(1f, trackSize.x - visualHandleWidth),
                Mathf.Max(handleSize.y, trackSize.y));
            handle.anchoredPosition = new Vector2(handle.anchoredPosition.x, 0f);

            _lastTrackSize = trackSize;
            _lastHandleSize = handleSize;
            _lastHandleScale = handleScale;
        }
    }
}
