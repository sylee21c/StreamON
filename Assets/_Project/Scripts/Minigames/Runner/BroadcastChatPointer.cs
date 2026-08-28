using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StreamOn.Minigames.Runner
{
    /// <summary>
    /// The chat overlay bans a viewer through the EventSystem, but gameplay reads the mouse
    /// directly, so one click on a chat line would also swing a weapon or drop a toy in the
    /// world. Gameplay input asks this whether the pointer is over the chat first.
    ///
    /// This deliberately checks the chat hierarchy rather than
    /// <see cref="EventSystem.IsPointerOverGameObject"/>: any full-screen HUD image with
    /// raycastTarget left on would otherwise swallow every click in the scene.
    /// </summary>
    public static class BroadcastChatPointer
    {
        private static readonly List<RaycastResult> Results = new List<RaycastResult>();
        private static int _cachedFrame = -1;
        private static bool _cachedResult;

        public static bool IsPointerOverChat()
        {
            // Gameplay polls this from several input helpers each frame; one raycast is enough.
            if (_cachedFrame == Time.frameCount) return _cachedResult;
            _cachedFrame = Time.frameCount;
            _cachedResult = Raycast();
            return _cachedResult;
        }

        private static bool Raycast()
        {
            EventSystem events = EventSystem.current;
            if (events == null) return false;

            PointerEventData pointer = new PointerEventData(events) { position = PointerPosition() };
            Results.Clear();
            events.RaycastAll(pointer, Results);
            for (int index = 0; index < Results.Count; index++)
            {
                GameObject hit = Results[index].gameObject;
                if (hit != null && hit.GetComponentInParent<RunnerChatController>() != null) return true;
            }
            return false;
        }

        private static Vector2 PointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }
    }
}
