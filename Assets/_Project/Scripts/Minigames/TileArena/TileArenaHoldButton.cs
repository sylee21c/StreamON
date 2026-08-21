using UnityEngine;
using UnityEngine.EventSystems;

namespace StreamOn.Minigames.TileArena
{
    public enum TileArenaDirection { Up, Down, Left, Right }

    public sealed class TileArenaHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private TileArenaController controller;
        [SerializeField] private TileArenaDirection direction;

        public void OnPointerDown(PointerEventData eventData) => controller?.SetPointerDirection(direction, true);
        public void OnPointerUp(PointerEventData eventData) => controller?.SetPointerDirection(direction, false);
        public void OnPointerExit(PointerEventData eventData) => controller?.SetPointerDirection(direction, false);

        private void OnDisable() => controller?.SetPointerDirection(direction, false);
    }
}
