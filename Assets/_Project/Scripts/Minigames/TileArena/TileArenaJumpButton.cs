using UnityEngine;
using UnityEngine.EventSystems;

namespace StreamOn.Minigames.TileArena
{
    public sealed class TileArenaJumpButton : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private TileArenaController controller;
        public void OnPointerDown(PointerEventData eventData) => controller?.TryJump();
    }
}
