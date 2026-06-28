using System;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace SmartShift.Core.Theme
{
    public enum AppTheme
    {
        Light,
        Dark
    }

    public static class ThemeManager
    {
        private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string AppsUseLightTheme = "AppsUseLightTheme";
        private const string SystemUsesLightTheme = "SystemUsesLightTheme";

        /// <summary>获取当前系统主题（读注册表）</summary>
        public static AppTheme GetCurrentTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(AppsUseLightTheme);
                        if (value != null && (int)value == 0)
                            return AppTheme.Dark;
                    }
                }
            }
            catch
            {
                // 读取失败默认返回浅色
            }
            return AppTheme.Light;
        }

        /// <summary>设置系统主题（写注册表 + 广播 WM_SETTINGCHANGE）</summary>
        public static bool SetTheme(AppTheme theme)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(PersonalizeKeyPath, writable: true))
                {
                    if (key == null) return false;

                    int value = (theme == AppTheme.Light) ? 1 : 0;
                    key.SetValue(AppsUseLightTheme, value, RegistryValueKind.DWord);
                    key.SetValue(SystemUsesLightTheme, value, RegistryValueKind.DWord);
                }

                BroadcastSettingChange();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>快捷切换（浅色 <-> 深色）</summary>
        public static AppTheme Toggle()
        {
            AppTheme current = GetCurrentTheme();
            AppTheme target = (current == AppTheme.Light) ? AppTheme.Dark : AppTheme.Light;
            SetTheme(target);
            return target;
        }

        private static void BroadcastSettingChange()
        {
            UIntPtr result;
            NativeMethods.SendMessageTimeout(
                (IntPtr)NativeMethods.HWND_BROADCAST,
                NativeMethods.WM_SETTINGCHANGE,
                UIntPtr.Zero,
                "ImmersiveColorSet",
                NativeMethods.SMTO_ABORTIFHUNG,
                5000,
                out result);
        }

        internal static class NativeMethods
        {
            public const uint HWND_BROADCAST = 0xFFFF;
            public const uint WM_SETTINGCHANGE = 0x001A;
            public const uint SMTO_ABORTIFHUNG = 0x0002;

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr SendMessageTimeout(
                IntPtr hWnd,
                uint Msg,
                UIntPtr wParam,
                [MarshalAs(UnmanagedType.LPStr)] string lParam,
                uint fuFlags,
                uint uTimeout,
                out UIntPtr lpdwResult);
        }
    }
}
