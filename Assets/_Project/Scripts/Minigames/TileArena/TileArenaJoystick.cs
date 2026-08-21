using UnityEngine;
using UnityEngine.EventSystems;

namespace StreamOn.Minigames.TileArena
{
    public sealed class TileArenaJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private TileArenaController controller;
        [SerializeField] private RectTransform knob;
        [SerializeField, Range(0f, 0.5f)] private float deadZone = 0.12f;
        private RectTransform _root;

        private void Awake() => _root = transform as RectTransform;
        public void OnPointerDown(PointerEventData eventData) => UpdatePointer(eventData);
        public void OnDrag(PointerEventData eventData) => UpdatePointer(eventData);
        public void OnPointerUp(PointerEventData eventData) => ResetJoystick();
        private void OnDisable() => ResetJoystick();

        private void UpdatePointer(PointerEventData eventData)
        {
            if (_root == null) _root = transform as RectTransform;
            if (_root == null || knob == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, eventData.position, eventData.pressEventCamera, out Vector2 local)) return;
            float radius = Mathf.Min(_root.rect.width, _root.rect.height) * 0.5f;
            float maximumDistance = Mathf.Max(1f, radius - knob.rect.width * 0.5f - 4f);
            Vector2 offset = Vector2.ClampMagnitude(local, maximumDistance);
            Vector2 normalized = offset / maximumDistance;
            if (Mathf.Abs(normalized.x) < deadZone) normalized.x = 0f;
            if (Mathf.Abs(normalized.y) < deadZone) normalized.y = 0f;
            knob.anchoredPosition = offset;
            controller?.SetJoystickVector(new Vector2(normalized.x, -normalized.y));
        }

        private void ResetJoystick()
        {
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            controller?.SetJoystickVector(Vector2.zero);
        }
    }
}
