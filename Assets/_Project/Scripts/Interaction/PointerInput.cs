using UnityEngine;
using UnityEngine.InputSystem;

namespace MonsoonRooftops.Interaction
{
    // One place that reads touch (device) or mouse (editor) so gameplay code stays device-agnostic.
    public static class PointerInput
    {
        public static int PressedCount
        {
            get
            {
                var ts = Touchscreen.current;
                if (ts != null)
                {
                    int n = 0;
                    foreach (var t in ts.touches)
                        if (t.press.isPressed) n++;
                    if (n > 0) return n;
                }
                var mouse = Mouse.current;
                return mouse != null && mouse.leftButton.isPressed ? 1 : 0;
            }
        }

        public static Vector2 Position
        {
            get
            {
                var ts = Touchscreen.current;
                if (ts != null && ts.primaryTouch.press.isPressed) return ts.primaryTouch.position.ReadValue();
                var mouse = Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            }
        }

        public static bool TryGetPinch(out float distance)
        {
            distance = 0f;
            var ts = Touchscreen.current;
            if (ts == null) return false;
            Vector2 a = default, b = default;
            int found = 0;
            foreach (var t in ts.touches)
            {
                if (!t.press.isPressed) continue;
                if (found == 0) a = t.position.ReadValue();
                else if (found == 1) b = t.position.ReadValue();
                found++;
            }
            if (found < 2) return false;
            distance = Vector2.Distance(a, b);
            return true;
        }

        public static float ScrollDelta()
        {
            var mouse = Mouse.current;
            return mouse != null ? mouse.scroll.ReadValue().y : 0f;
        }
    }
}
