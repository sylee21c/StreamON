using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StreamOn.Minigames.TileArena
{
    public sealed class TileArenaMouseDragSlider : Slider
    {
        private bool _draggingHandle;

        public override void OnPointerDown(PointerEventData eventData)
        {
            _draggingHandle = eventData.button == PointerEventData.InputButton.Left
                && handleRect != null
                && RectTransformUtility.RectangleContainsScreenPoint(handleRect, eventData.position, eventData.pressEventCamera);
            if (_draggingHandle) base.OnPointerDown(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (_draggingHandle) base.OnDrag(eventData);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (_draggingHandle) base.OnPointerUp(eventData);
            _draggingHandle = false;
        }

        public override void OnMove(AxisEventData eventData)
        {
            // Volume sliders are intentionally mouse-drag only.
        }

        protected override void OnDisable()
        {
            _draggingHandle = false;
            base.OnDisable();
        }
    }
}
