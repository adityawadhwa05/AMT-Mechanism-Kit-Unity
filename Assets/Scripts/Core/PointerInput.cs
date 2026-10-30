using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AMT.AR.Core
{
    // Raw pointer state for Editor mouse and Android touch, under either Unity input backend.
    public static class PointerInput
    {
        static readonly List<RaycastResult> UiHits = new List<RaycastResult>();

        // Positions of all fingers currently down (empty when using a mouse).
        public static void Touches(List<Vector2> into)
        {
            into.Clear();
#if ENABLE_INPUT_SYSTEM
            var screen = Touchscreen.current;
            if (screen == null) return;
            for (int i = 0; i < screen.touches.Count; i++)
                if (screen.touches[i].press.isPressed) into.Add(screen.touches[i].position.ReadValue());
#else
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled) into.Add(t.position);
            }
#endif
        }

        public static bool MouseAvailable
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null;
#else
                return Input.mousePresent;
#endif
            }
        }

        public static Vector2 MousePosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
                return Input.mousePosition;
#endif
            }
        }

        // 0 = left, 1 = right, 2 = middle
        public static bool MouseHeld(int button)
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            if (m == null) return false;
            return button == 0 ? m.leftButton.isPressed : button == 1 ? m.rightButton.isPressed : m.middleButton.isPressed;
#else
            return Input.GetMouseButton(button);
#endif
        }

        public static float ScrollSteps
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current == null ? 0 : Mouse.current.scroll.ReadValue().y / 120f;
#else
                return Input.mouseScrollDelta.y;
#endif
            }
        }

        public static bool OverUI(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            UiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, UiHits);
            bool over = UiHits.Count > 0;
            UiHits.Clear();
            return over;
        }

        // Pixel threshold that feels the same on phones and desktop monitors.
        public static float TapSlop => Mathf.Max(10f, (Screen.dpi > 0 ? Screen.dpi : 160f) * .07f);
    }
}
