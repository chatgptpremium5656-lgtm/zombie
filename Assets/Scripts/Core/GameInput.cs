using UnityEngine;
#if ENABLE_INPUT_SYSTEM && ZOMBIE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Zombie.Core
{
    /// <summary>
    /// Keyboard + mouse input that works with either input backend (new Input System package or the
    /// legacy Input Manager), whichever the project has enabled. Gameplay code only talks to this class,
    /// so touch controls for iOS/Android can be added here later.
    /// </summary>
    public static class GameInput
    {
        public enum Button
        {
            Jump,
            Sprint,
            Interact,
            Rotate,
            CraftMenu,
            Pause,
            Flashlight,
        }

        public static Vector2 Move
        {
            get
            {
                float x = 0f, y = 0f;
                if (KeyHeld(KeyCode.A)) x -= 1f;
                if (KeyHeld(KeyCode.D)) x += 1f;
                if (KeyHeld(KeyCode.S)) y -= 1f;
                if (KeyHeld(KeyCode.W)) y += 1f;
                var v = new Vector2(x, y);
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        /// <summary>Mouse movement this frame in pixels.</summary>
        public static Vector2 Look
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && ZOMBIE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
                // Legacy axes are pixels * 0.1 with the default Input Manager sensitivity.
                return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
#else
                return Vector2.zero;
#endif
            }
        }

        /// <summary>-1, 0 or +1 for one wheel notch.</summary>
        public static int Scroll
        {
            get
            {
                float y;
#if ENABLE_INPUT_SYSTEM && ZOMBIE_INPUT_SYSTEM
                var mouse = Mouse.current;
                y = mouse != null ? mouse.scroll.ReadValue().y : 0f;
#elif ENABLE_LEGACY_INPUT_MANAGER
                y = Input.mouseScrollDelta.y;
#else
                y = 0f;
#endif
                if (y > 0.01f) return 1;
                if (y < -0.01f) return -1;
                return 0;
            }
        }

        public static bool PrimaryHeld => MouseHeld(0);
        public static bool PrimaryPressed => MousePressed(0);
        public static bool SecondaryPressed => MousePressed(1);

        public static bool Held(Button button) => KeyHeld(KeyFor(button));

        public static bool Pressed(Button button)
        {
            if (button == Button.CraftMenu && KeyPressed(KeyCode.C)) return true;
            return KeyPressed(KeyFor(button));
        }

        /// <summary>Returns 0..8 when a number key 1..9 was pressed this frame, otherwise -1.</summary>
        public static int HotbarKeyPressed()
        {
            for (int i = 0; i < 9; i++)
            {
                if (KeyPressed(KeyCode.Alpha1 + i)) return i;
            }
            return -1;
        }

        static KeyCode KeyFor(Button button)
        {
            switch (button)
            {
                case Button.Jump: return KeyCode.Space;
                case Button.Sprint: return KeyCode.LeftShift;
                case Button.Interact: return KeyCode.E;
                case Button.Rotate: return KeyCode.R;
                case Button.CraftMenu: return KeyCode.Tab;
                case Button.Pause: return KeyCode.Escape;
                default: return KeyCode.F;
            }
        }

#if ENABLE_INPUT_SYSTEM && ZOMBIE_INPUT_SYSTEM
        static Key ToKey(KeyCode code)
        {
            switch (code)
            {
                case KeyCode.A: return Key.A;
                case KeyCode.C: return Key.C;
                case KeyCode.D: return Key.D;
                case KeyCode.E: return Key.E;
                case KeyCode.F: return Key.F;
                case KeyCode.R: return Key.R;
                case KeyCode.S: return Key.S;
                case KeyCode.W: return Key.W;
                case KeyCode.Space: return Key.Space;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.Escape: return Key.Escape;
                default:
                    if (code >= KeyCode.Alpha1 && code <= KeyCode.Alpha9) return Key.Digit1 + (code - KeyCode.Alpha1);
                    return Key.None;
            }
        }

        static bool KeyHeld(KeyCode code)
        {
            var kb = Keyboard.current;
            var key = ToKey(code);
            return kb != null && key != Key.None && kb[key].isPressed;
        }

        static bool KeyPressed(KeyCode code)
        {
            var kb = Keyboard.current;
            var key = ToKey(code);
            return kb != null && key != Key.None && kb[key].wasPressedThisFrame;
        }

        static bool MouseHeld(int button)
        {
            var mouse = Mouse.current;
            if (mouse == null) return false;
            return button == 0 ? mouse.leftButton.isPressed : mouse.rightButton.isPressed;
        }

        static bool MousePressed(int button)
        {
            var mouse = Mouse.current;
            if (mouse == null) return false;
            return button == 0 ? mouse.leftButton.wasPressedThisFrame : mouse.rightButton.wasPressedThisFrame;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        static bool KeyHeld(KeyCode code) => Input.GetKey(code);
        static bool KeyPressed(KeyCode code) => Input.GetKeyDown(code);
        static bool MouseHeld(int button) => Input.GetMouseButton(button);
        static bool MousePressed(int button) => Input.GetMouseButtonDown(button);
#else
        static bool KeyHeld(KeyCode code) => false;
        static bool KeyPressed(KeyCode code) => false;
        static bool MouseHeld(int button) => false;
        static bool MousePressed(int button) => false;
#endif
    }
}
