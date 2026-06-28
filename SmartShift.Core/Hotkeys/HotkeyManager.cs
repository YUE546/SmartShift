using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using SmartShift.Core;

namespace SmartShift.Core.Hotkeys
{
    public class Hotkey
    {
        public int Id { get; set; }
        public int Key { get; set; }
        public bool Ctrl { get; set; }
        public bool Alt { get; set; }
        public bool Shift { get; set; }
        public bool Win { get; set; }
        public Action OnPressed { get; set; }
    }

    public class HotkeyManager : IDisposable
    {
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        private readonly IntPtr _windowHandle;
        internal readonly Dictionary<int, Hotkey> _registeredHotkeys = new Dictionary<int, Hotkey>();
        private bool _disposed;

        public HotkeyManager(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
        }

        public bool Register(Hotkey hotkey)
        {
            if (hotkey == null) throw new ArgumentNullException(nameof(hotkey));

            uint modifiers = 0;
            if (hotkey.Ctrl) modifiers |= MOD_CONTROL;
            if (hotkey.Alt) modifiers |= MOD_ALT;
            if (hotkey.Shift) modifiers |= MOD_SHIFT;
            if (hotkey.Win) modifiers |= MOD_WIN;

            bool result = NativeMethods.RegisterHotKey(_windowHandle, hotkey.Id, modifiers, (uint)hotkey.Key);
            if (result)
            {
                _registeredHotkeys[hotkey.Id] = hotkey;
            }
            else
            {
                int err = Marshal.GetLastWin32Error();
                Logger.Warning($"RegisterHotKey 失败: ID={hotkey.Id}, VK=0x{hotkey.Key:X2}, Mods=0x{modifiers:X4}, 错误码={err}, 错误={new Win32Exception(err).Message}");
            }
            return result;
        }

        public void Unregister(int hotkeyId)
        {
            if (_registeredHotkeys.ContainsKey(hotkeyId))
            {
                NativeMethods.UnregisterHotKey(_windowHandle, hotkeyId);
                _registeredHotkeys.Remove(hotkeyId);
            }
        }

        public void UnregisterAll()
        {
            foreach (var pair in _registeredHotkeys)
            {
                NativeMethods.UnregisterHotKey(_windowHandle, pair.Key);
            }
            _registeredHotkeys.Clear();
        }

        /// <summary>
        /// 处理 WM_HOTKEY 消息。由 UI 层在窗口消息循环中调用。
        /// </summary>
        /// <param name="wParam">消息的 wParam，包含热键 ID</param>
        /// <returns>如果消息被处理则返回 true，否则返回 false</returns>
        public bool ProcessHotkeyMessage(IntPtr wParam)
        {
            int id = wParam.ToInt32();
            if (_registeredHotkeys.TryGetValue(id, out Hotkey hotkey))
            {
                hotkey.OnPressed?.Invoke();
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                UnregisterAll();
                _disposed = true;
            }
        }

        internal static class NativeMethods
        {
            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        }
    }
}
