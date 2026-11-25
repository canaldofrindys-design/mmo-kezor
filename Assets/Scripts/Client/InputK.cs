using UnityEngine;
#if KEZOR_INPUTSYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Kezor.Client
{
    /// <summary>
    /// Entrada independente do backend: usa o Input Manager antigo quando ativo, senão o pacote Input System
    /// (padrão em projetos novos do Unity 6). Mouse: 0 = esquerdo, 1 = direito, 2 = meio.
    /// </summary>
    public static class InputK
    {
#if KEZOR_INPUTSYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        private static UnityEngine.InputSystem.Controls.ButtonControl? Btn(int b)
        {
            var m = UnityEngine.InputSystem.Mouse.current; if (m == null) return null;
            return b == 0 ? m.leftButton : b == 1 ? m.rightButton : m.middleButton;
        }
        public static Vector3 MousePosition => UnityEngine.InputSystem.Mouse.current != null ? (Vector3)UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector3.zero;
        public static float Scroll => UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y / 120f : 0f;
        public static float MouseDeltaX => UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.delta.ReadValue().x * 0.1f : 0f;
        public static bool MouseDown(int b) => Btn(b)?.wasPressedThisFrame ?? false;
        public static bool MouseUp(int b) => Btn(b)?.wasReleasedThisFrame ?? false;
        public static bool MouseHeld(int b) => Btn(b)?.isPressed ?? false;
        public static bool KeyDown(KeyCode k)
        {
            var kb = Keyboard.current; if (kb == null) return false;
            Key key;
            if (k >= KeyCode.A && k <= KeyCode.Z) key = Key.A + (k - KeyCode.A);                     // todas as letras
            else if (k >= KeyCode.Alpha1 && k <= KeyCode.Alpha9) key = Key.Digit1 + (k - KeyCode.Alpha1);
            else key = k switch
            {
                KeyCode.Alpha0 => Key.Digit0, KeyCode.Space => Key.Space, KeyCode.F1 => Key.F1, KeyCode.Escape => Key.Escape,
                KeyCode.Return => Key.Enter, KeyCode.Tab => Key.Tab, KeyCode.Home => Key.Home,
                _ => Key.None
            };
            return key != Key.None && kb[key].wasPressedThisFrame;
        }
#else
        public static Vector3 MousePosition => Input.mousePosition;
        public static float Scroll => Input.mouseScrollDelta.y;
        public static float MouseDeltaX => Input.GetAxis("Mouse X");
        public static bool MouseDown(int b) => Input.GetMouseButtonDown(b);
        public static bool MouseUp(int b) => Input.GetMouseButtonUp(b);
        public static bool MouseHeld(int b) => Input.GetMouseButton(b);
        public static bool KeyDown(KeyCode k) => Input.GetKeyDown(k);
#endif
    }
}
