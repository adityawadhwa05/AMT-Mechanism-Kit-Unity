using System;
using System.Collections.Generic;
using AMT.AR.Core;
using UnityEngine;

namespace AMT.AR.Interaction
{
    // Turns raw pointer input into gestures: tap, double tap, one-finger drag, two-finger pinch,
    // twist and pan. Mouse: left drag, right/middle pan, wheel zoom. Gestures that start on the UI
    // are ignored until every finger is lifted.
    public sealed class GestureInput : MonoBehaviour
    {
        public event Action<Vector2> Tapped;
        public event Action<Vector2> DoubleTapped;

        public Vector2 Drag { get; private set; }
        public Vector2 Pan { get; private set; }
        public float Pinch { get; private set; } = 1f;
        public float Twist { get; private set; }
        public bool Active { get; private set; }
        public int FingerCount { get; private set; }

        readonly List<Vector2> touches = new List<Vector2>();
        readonly List<Vector2> previous = new List<Vector2>();
        bool blocked, moved, multi;
        Vector2 downPosition, lastMouse;
        float downTime, lastTapTime = -10f;
        Vector2 lastTapPosition;
        int lastMouseButtons;

        void Update()
        {
            Drag = Vector2.zero; Pan = Vector2.zero; Pinch = 1f; Twist = 0f;
            PointerInput.Touches(touches);
            if (touches.Count > 0) UpdateTouch();
            else UpdateMouse();
            previous.Clear();
            previous.AddRange(touches);
        }

        void UpdateTouch()
        {
            FingerCount = touches.Count;
            bool startedNow = previous.Count == 0;
            if (startedNow)
            {
                blocked = PointerInput.OverUI(touches[0]);
                downPosition = touches[0]; downTime = Time.unscaledTime; moved = false; multi = false;
            }
            if (blocked) { Active = false; return; }
            Active = true;
            if (touches.Count >= 2) multi = true;
            if (touches.Count == 1 && previous.Count == 1)
            {
                var d = touches[0] - previous[0];
                if ((touches[0] - downPosition).magnitude > PointerInput.TapSlop) moved = true;
                if (moved && !multi) Drag = d;
            }
            else if (touches.Count >= 2 && previous.Count >= 2)
            {
                Vector2 a = touches[0], b = touches[1], pa = previous[0], pb = previous[1];
                float now = (a - b).magnitude, before = (pa - pb).magnitude;
                if (before > 1f) Pinch = now / before;
                Twist = Vector2.SignedAngle(pb - pa, b - a);
                Pan = (a + b) * .5f - (pa + pb) * .5f;
            }
        }

        void UpdateMouse()
        {
            // A touch gesture just ended.
            if (previous.Count > 0)
            {
                if (!blocked && !moved && !multi && Time.unscaledTime - downTime < .35f) RegisterTap(previous[0]);
                Active = false; blocked = false; FingerCount = 0;
                return;
            }
            FingerCount = 0;
            if (!PointerInput.MouseAvailable) { Active = false; return; }
            var pos = PointerInput.MousePosition;
            int buttons = (PointerInput.MouseHeld(0) ? 1 : 0) | (PointerInput.MouseHeld(1) ? 2 : 0) | (PointerInput.MouseHeld(2) ? 4 : 0);
            if (buttons != 0 && lastMouseButtons == 0)
            {
                blocked = PointerInput.OverUI(pos);
                downPosition = pos; downTime = Time.unscaledTime; moved = false; lastMouse = pos;
            }
            if (buttons != 0 && !blocked)
            {
                Active = true;
                var d = pos - lastMouse;
                if ((pos - downPosition).magnitude > PointerInput.TapSlop) moved = true;
                if (moved)
                {
                    if ((buttons & 1) != 0) Drag = d;
                    else Pan = d;
                }
            }
            if (buttons == 0 && lastMouseButtons != 0)
            {
                if (!blocked && !moved && (lastMouseButtons & 1) != 0 && Time.unscaledTime - downTime < .35f) RegisterTap(pos);
                blocked = false; Active = false;
            }
            float scroll = PointerInput.ScrollSteps;
            if (Mathf.Abs(scroll) > .001f && !PointerInput.OverUI(pos)) Pinch = Mathf.Pow(1.12f, scroll);
            lastMouse = pos; lastMouseButtons = buttons;
        }

        void RegisterTap(Vector2 position)
        {
            if (Time.unscaledTime - lastTapTime < .32f && (position - lastTapPosition).magnitude < PointerInput.TapSlop * 3f)
            {
                lastTapTime = -10f;
                DoubleTapped?.Invoke(position);
                return;
            }
            lastTapTime = Time.unscaledTime; lastTapPosition = position;
            Tapped?.Invoke(position);
        }
    }
}
