using System;
using System.IO;
using System.Windows.Input;
using Newtonsoft.Json;
using SmartShift.Core.Configuration;
using SmartShift.Core.Hotkeys;
using Xunit;

namespace SmartShift.Tests
{
    public class HotkeyManagerTests
    {
        [Fact]
        public void ProcessHotkeyMessage_WithRegisteredHotkey_InvokesCallbackAndReturnsTrue()
        {
            var manager = new HotkeyManager(IntPtr.Zero);
            bool callbackInvoked = false;

            // 模拟注册：直接向内部字典添加热键
            manager._registeredHotkeys[1] = new Hotkey
            {
                Id = 1,
                Key = 0x54,
                Ctrl = true,
                Alt = true,
                OnPressed = () => callbackInvoked = true
            };

            bool result = manager.ProcessHotkeyMessage(new IntPtr(1));

            Assert.True(result, "ProcessHotkeyMessage 应该返回 true 表示消息已处理");
            Assert.True(callbackInvoked, "注册的回调应该被调用");
        }

        [Fact]
        public void ProcessHotkeyMessage_WithUnregisteredHotkey_ReturnsFalse()
        {
            var manager = new HotkeyManager(IntPtr.Zero);

            bool result = manager.ProcessHotkeyMessage(new IntPtr(999));

            Assert.False(result, "未注册的热键 ID 应该返回 false");
        }

        [Fact]
        public void Hotkey_AllModifiersSet_ProducesCorrectCombination()
        {
            var hotkey = new Hotkey
            {
                Id = 1,
                Key = 0x54,
                Ctrl = true,
                Alt = true,
                Shift = true,
                Win = true
            };

            Assert.True(hotkey.Ctrl);
            Assert.True(hotkey.Alt);
            Assert.True(hotkey.Shift);
            Assert.True(hotkey.Win);
            Assert.Equal(0x54, hotkey.Key);
        }

        [Fact]
        public void Unregister_RemovesHotkeyFromDictionary()
        {
            var manager = new HotkeyManager(IntPtr.Zero);
            manager._registeredHotkeys[1] = new Hotkey { Id = 1 };

            manager.Unregister(1);

            Assert.False(manager._registeredHotkeys.ContainsKey(1));
        }

        // ========================
        // VK 码转换测试 (TDD: RED)
        // ========================

        /// <summary>
        /// 验证默认设置的 ToggleThemeHotkey 使用正确的 VK 码 (VK_T = 0x54)，
        /// 而不是 WPF Key 枚举值 (Key.T = 44)。
        /// </summary>
        [Fact]
        public void DefaultSettings_ToggleThemeHotkey_UsesVkCodeNotWpfEnum()
        {
            var settings = new UserSettings();

            // VK_T = 0x54 = 84 (Windows 虚拟键码)
            Assert.Equal(0x54, settings.ToggleThemeHotkey.Key);
            Assert.True(settings.ToggleThemeHotkey.Ctrl, "默认应按 Ctrl");
            Assert.True(settings.ToggleThemeHotkey.Alt, "默认应按 Alt");
            Assert.False(settings.ToggleThemeHotkey.Win, "默认不应按 Win");
        }

        /// <summary>
        /// 验证默认设置的 TogglePowerHotkey 使用正确的 VK 码 (VK_P = 0x50)，
        /// 而不是 WPF Key 枚举值 (Key.P = 60)。
        /// </summary>
        [Fact]
        public void DefaultSettings_TogglePowerHotkey_UsesVkCodeNotWpfEnum()
        {
            var settings = new UserSettings();

            // VK_P = 0x50 = 80 (Windows 虚拟键码)
            Assert.Equal(0x50, settings.TogglePowerHotkey.Key);
            Assert.True(settings.TogglePowerHotkey.Ctrl, "默认应按 Ctrl");
            Assert.True(settings.TogglePowerHotkey.Alt, "默认应按 Alt");
            Assert.False(settings.TogglePowerHotkey.Win, "默认不应按 Win");
        }

        /// <summary>
        /// 验证 KeyInterop.VirtualKeyFromKey 对字母键返回正确的 Windows VK 码。
        /// 这是关键：WPF Key.T = 63，但 VK_T = 0x54 = 84。
        /// </summary>
        [Fact]
        public void KeyInterop_VirtualKeyFromKey_ForLetters_ReturnsWindowsVkCode()
        {
            // WPF Key 枚举值 ≠ Windows VK 码
            // Key.A=44, Key.B=45, ..., Key.T=63, Key.Z=69
            Assert.Equal(63, (int)Key.T);  // WPF Key.T 枚举值 = 44 + 19

            // KeyInterop 正确转换: VK_T = 0x54 = 84
            int vkCode = KeyInterop.VirtualKeyFromKey(Key.T);
            Assert.Equal(0x54, vkCode);  // VK_T = 0x54 = 84
            Assert.NotEqual((int)Key.T, vkCode);  // 绝不能相同！
        }

        /// <summary>
        /// 验证 KeyInterop.VirtualKeyFromKey 对功能键返回正确的 Windows VK 码。
        /// 这是之前 bug 的核心：WPF Key.F5 = 94，但 VK_F5 = 0x74 = 116。
        /// </summary>
        [Fact]
        public void KeyInterop_VirtualKeyFromKey_ForFunctionKeys_ReturnsWindowsVkCode()
        {
            Assert.Equal(94, (int)Key.F5);  // WPF Key.F5 枚举值 ≠ VK_F5

            int vkCode = KeyInterop.VirtualKeyFromKey(Key.F5);
            Assert.Equal(0x74, vkCode);  // VK_F5 = 0x74 = 116
            Assert.NotEqual((int)Key.F5, vkCode);  // 绝不能相同！
        }

        /// <summary>
        /// 验证 KeyInterop 往返转换保持一致性：Key → VK → Key。
        /// </summary>
        [Fact]
        public void KeyInterop_RoundTrip_KeyToVkAndBack_PreservesKey()
        {
            var testKeys = new[] { Key.T, Key.P, Key.F5, Key.F12, Key.A, Key.Z };

            foreach (var key in testKeys)
            {
                int vk = KeyInterop.VirtualKeyFromKey(key);
                Key restored = KeyInterop.KeyFromVirtualKey(vk);

                Assert.Equal(key, restored);
            }
        }

        /// <summary>
        /// 验证 KeyInterop 往返转换保持一致性：VK → Key → VK。
        /// </summary>
        [Fact]
        public void KeyInterop_RoundTrip_VkToKeyAndBack_PreservesVkCode()
        {
            var testVkCodes = new[] { 0x54, 0x50, 0x74, 0x7B, 0x41, 0x5A };

            foreach (int vk in testVkCodes)
            {
                Key key = KeyInterop.KeyFromVirtualKey(vk);
                int restored = KeyInterop.VirtualKeyFromKey(key);

                Assert.Equal(vk, restored);
            }
        }

        /// <summary>
        /// 验证热键配置序列化往返后保留正确的 VK 码。
        /// 模拟：保存设置 → 从 JSON 加载 → VK 码应保持不变。
        /// </summary>
        [Fact]
        public void HotkeyConfig_SerializeRoundTrip_PreservesCorrectVkCodes()
        {
            var settings = new UserSettings();
            // 确保使用正确的 VK 码
            settings.ToggleThemeHotkey.Key = 0x54;  // VK_T
            settings.TogglePowerHotkey.Key = 0x50;  // VK_P

            string json = JsonConvert.SerializeObject(settings);
            var loaded = JsonConvert.DeserializeObject<UserSettings>(json);

            Assert.NotNull(loaded);
            Assert.Equal(0x54, loaded.ToggleThemeHotkey.Key);
            Assert.Equal(0x50, loaded.TogglePowerHotkey.Key);
            Assert.True(loaded.ToggleThemeHotkey.Ctrl);
            Assert.True(loaded.ToggleThemeHotkey.Alt);
        }

        /// <summary>
        /// 验证坏数据检测：如果 Key 值等于 WPF 枚举值（如 94=Key.F5），
        /// 则说明存的是 WPF 枚举值而非 VK 码，这是 bug。
        /// 此测试记录已知的 WPF Key 枚举值 vs VK 码的差异表。
        /// </summary>
        [Fact]
        public void HotkeyConfig_WpfKeyEnumValues_AreNotValidVkCodes()
        {
            // 验证已知的 WPF Key 枚举值 ≠ VK 码
            // 如果 settings.json 中存的是这些值，说明有 bug

            // T 键：WPF Key.T = 44，VK_T = 0x54 = 84
            Assert.NotEqual((int)Key.T, KeyInterop.VirtualKeyFromKey(Key.T));

            // P 键：WPF Key.P = 60，VK_P = 0x50 = 80
            Assert.NotEqual((int)Key.P, KeyInterop.VirtualKeyFromKey(Key.P));

            // F5：WPF Key.F5 = 94，VK_F5 = 0x74 = 116
            Assert.NotEqual((int)Key.F5, KeyInterop.VirtualKeyFromKey(Key.F5));

            // F4：WPF Key.F4 = 93，VK_F4 = 0x73 = 115
            Assert.NotEqual((int)Key.F4, KeyInterop.VirtualKeyFromKey(Key.F4));
        }
    }
}