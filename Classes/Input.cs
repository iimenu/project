using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace iiMenu
{
    public static class Input
    {
        private static readonly Dictionary<KeyCode, Key> keyMap = new Dictionary<KeyCode, Key>
        {
            { KeyCode.None, Key.None },
            { KeyCode.Space, Key.Space },
            { KeyCode.Return, Key.Enter },
            { KeyCode.KeypadEnter, Key.NumpadEnter },
            { KeyCode.Tab, Key.Tab },
            { KeyCode.BackQuote, Key.Backquote },
            { KeyCode.Quote, Key.Quote },
            { KeyCode.Semicolon, Key.Semicolon },
            { KeyCode.Comma, Key.Comma },
            { KeyCode.Period, Key.Period },
            { KeyCode.Slash, Key.Slash },
            { KeyCode.Backslash, Key.Backslash },
            { KeyCode.LeftBracket, Key.LeftBracket },
            { KeyCode.RightBracket, Key.RightBracket },
            { KeyCode.Minus, Key.Minus },
            { KeyCode.Equals, Key.Equals },
            { KeyCode.A, Key.A },
            { KeyCode.B, Key.B },
            { KeyCode.C, Key.C },
            { KeyCode.D, Key.D },
            { KeyCode.E, Key.E },
            { KeyCode.F, Key.F },
            { KeyCode.G, Key.G },
            { KeyCode.H, Key.H },
            { KeyCode.I, Key.I },
            { KeyCode.J, Key.J },
            { KeyCode.K, Key.K },
            { KeyCode.L, Key.L },
            { KeyCode.M, Key.M },
            { KeyCode.N, Key.N },
            { KeyCode.O, Key.O },
            { KeyCode.P, Key.P },
            { KeyCode.Q, Key.Q },
            { KeyCode.R, Key.R },
            { KeyCode.S, Key.S },
            { KeyCode.T, Key.T },
            { KeyCode.U, Key.U },
            { KeyCode.V, Key.V },
            { KeyCode.W, Key.W },
            { KeyCode.X, Key.X },
            { KeyCode.Y, Key.Y },
            { KeyCode.Z, Key.Z },
            { KeyCode.Alpha0, Key.Digit0 },
            { KeyCode.Alpha1, Key.Digit1 },
            { KeyCode.Alpha2, Key.Digit2 },
            { KeyCode.Alpha3, Key.Digit3 },
            { KeyCode.Alpha4, Key.Digit4 },
            { KeyCode.Alpha5, Key.Digit5 },
            { KeyCode.Alpha6, Key.Digit6 },
            { KeyCode.Alpha7, Key.Digit7 },
            { KeyCode.Alpha8, Key.Digit8 },
            { KeyCode.Alpha9, Key.Digit9 },
            { KeyCode.LeftShift, Key.LeftShift },
            { KeyCode.RightShift, Key.RightShift },
            { KeyCode.LeftAlt, Key.LeftAlt },
            { KeyCode.RightAlt, Key.RightAlt },
            { KeyCode.LeftControl, Key.LeftCtrl },
            { KeyCode.RightControl, Key.RightCtrl },
            { KeyCode.Escape, Key.Escape },
            { KeyCode.LeftArrow, Key.LeftArrow },
            { KeyCode.RightArrow, Key.RightArrow },
            { KeyCode.UpArrow, Key.UpArrow },
            { KeyCode.DownArrow, Key.DownArrow },
            { KeyCode.Backspace, Key.Backspace },
            { KeyCode.PageDown, Key.PageDown },
            { KeyCode.PageUp, Key.PageUp },
            { KeyCode.Home, Key.Home },
            { KeyCode.End, Key.End },
            { KeyCode.Insert, Key.Insert },
            { KeyCode.Delete, Key.Delete },
            { KeyCode.CapsLock, Key.CapsLock },
            { KeyCode.Numlock, Key.NumLock },
            { KeyCode.ScrollLock, Key.ScrollLock },
            { KeyCode.Keypad0, Key.Numpad0 },
            { KeyCode.Keypad1, Key.Numpad1 },
            { KeyCode.Keypad2, Key.Numpad2 },
            { KeyCode.Keypad3, Key.Numpad3 },
            { KeyCode.Keypad4, Key.Numpad4 },
            { KeyCode.Keypad5, Key.Numpad5 },
            { KeyCode.Keypad6, Key.Numpad6 },
            { KeyCode.Keypad7, Key.Numpad7 },
            { KeyCode.Keypad8, Key.Numpad8 },
            { KeyCode.Keypad9, Key.Numpad9 },
            { KeyCode.KeypadDivide, Key.NumpadDivide },
            { KeyCode.KeypadMultiply, Key.NumpadMultiply },
            { KeyCode.KeypadPlus, Key.NumpadPlus },
            { KeyCode.KeypadMinus, Key.NumpadMinus },
            { KeyCode.KeypadPeriod, Key.NumpadPeriod },
            { KeyCode.KeypadEquals, Key.NumpadEquals },
            { KeyCode.F1, Key.F1 },
            { KeyCode.F2, Key.F2 },
            { KeyCode.F3, Key.F3 },
            { KeyCode.F4, Key.F4 },
            { KeyCode.F5, Key.F5 },
            { KeyCode.F6, Key.F6 },
            { KeyCode.F7, Key.F7 },
            { KeyCode.F8, Key.F8 },
            { KeyCode.F9, Key.F9 },
            { KeyCode.F10, Key.F10 },
            { KeyCode.F11, Key.F11 },
            { KeyCode.F12, Key.F12 }
        };

        public static Key ToKey(KeyCode keyCode)
        {
            if (keyMap.TryGetValue(keyCode, out Key key))
                return key;
            if (Enum.TryParse<Key>(keyCode.ToString(), true, out key))
                return key;
            return Key.None;
        }

        public static bool GetKey(KeyCode keyCode)
        {
            if (Keyboard.current != null)
            {
                Key key = ToKey(keyCode);
                if (key != Key.None)
                    return Keyboard.current[key].isPressed;
            }
            try { return UnityEngine.Input.GetKey(keyCode); } catch { return false; }
        }

        public static bool GetKeyDown(KeyCode keyCode)
        {
            if (Keyboard.current != null)
            {
                Key key = ToKey(keyCode);
                if (key != Key.None)
                    return Keyboard.current[key].wasPressedThisFrame;
            }
            try { return UnityEngine.Input.GetKeyDown(keyCode); } catch { return false; }
        }

        public static bool GetKeyUp(KeyCode keyCode)
        {
            if (Keyboard.current != null)
            {
                Key key = ToKey(keyCode);
                if (key != Key.None)
                    return Keyboard.current[key].wasReleasedThisFrame;
            }
            try { return UnityEngine.Input.GetKeyUp(keyCode); } catch { return false; }
        }

        public static bool GetKey(Key key) => Keyboard.current != null && key != Key.None && Keyboard.current[key].isPressed;
        public static bool GetKeyDown(Key key) => Keyboard.current != null && key != Key.None && Keyboard.current[key].wasPressedThisFrame;
        public static bool GetKeyUp(Key key) => Keyboard.current != null && key != Key.None && Keyboard.current[key].wasReleasedThisFrame;

        public static bool GetKey(string keyName)
        {
            if (Enum.TryParse<Key>(keyName, true, out var key))
                return GetKey(key);
            if (Enum.TryParse<KeyCode>(keyName, true, out var keyCode))
                return GetKey(keyCode);
            return false;
        }

        public static bool GetKeyDown(string keyName)
        {
            if (Enum.TryParse<Key>(keyName, true, out var key))
                return GetKeyDown(key);
            if (Enum.TryParse<KeyCode>(keyName, true, out var keyCode))
                return GetKeyDown(keyCode);
            return false;
        }

        public static bool GetKeyUp(string keyName)
        {
            if (Enum.TryParse<Key>(keyName, true, out var key))
                return GetKeyUp(key);
            if (Enum.TryParse<KeyCode>(keyName, true, out var keyCode))
                return GetKeyUp(keyCode);
            return false;
        }

        public static bool GetMouseButton(int button)
        {
            if (Mouse.current != null)
            {
                if (button == 0) return Mouse.current.leftButton.isPressed;
                if (button == 1) return Mouse.current.rightButton.isPressed;
                if (button == 2) return Mouse.current.middleButton.isPressed;
            }
            try { return UnityEngine.Input.GetMouseButton(button); } catch { return false; }
        }

        public static bool GetMouseButtonDown(int button)
        {
            if (Mouse.current != null)
            {
                if (button == 0) return Mouse.current.leftButton.wasPressedThisFrame;
                if (button == 1) return Mouse.current.rightButton.wasPressedThisFrame;
                if (button == 2) return Mouse.current.middleButton.wasPressedThisFrame;
            }
            try { return UnityEngine.Input.GetMouseButtonDown(button); } catch { return false; }
        }

        public static bool GetMouseButtonUp(int button)
        {
            if (Mouse.current != null)
            {
                if (button == 0) return Mouse.current.leftButton.wasReleasedThisFrame;
                if (button == 1) return Mouse.current.rightButton.wasReleasedThisFrame;
                if (button == 2) return Mouse.current.middleButton.wasReleasedThisFrame;
            }
            try { return UnityEngine.Input.GetMouseButtonUp(button); } catch { return false; }
        }

        public static Vector3 mousePosition
        {
            get
            {
                if (Mouse.current != null)
                {
                    Vector2 pos = Mouse.current.position.ReadValue();
                    return new Vector3(pos.x, pos.y, 0f);
                }
                try { return UnityEngine.Input.mousePosition; } catch { return Vector3.zero; }
            }
        }

        public static Vector2 mouseScrollDelta
        {
            get
            {
                if (Mouse.current != null)
                    return Mouse.current.scroll.ReadValue();
                try { return UnityEngine.Input.mouseScrollDelta; } catch { return Vector2.zero; }
            }
        }
    }
}

