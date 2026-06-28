using System.Windows.Controls;
using System.Windows.Input;
using SmartShift.Core.Configuration;

namespace SmartShift.UI.Pages
{
    public partial class HotkeySettingsPage : Page
    {
        private readonly UserSettings _settings;

        public HotkeySettingsPage(UserSettings settings)
        {
            InitializeComponent();
            _settings = settings;

            // 填充主键下拉框
            var keys = new[] { Key.A, Key.B, Key.C, Key.D, Key.E, Key.F, Key.G, Key.H,
                               Key.I, Key.J, Key.K, Key.L, Key.M, Key.N, Key.O, Key.P,
                               Key.Q, Key.R, Key.S, Key.T, Key.U, Key.V, Key.W, Key.X, Key.Y, Key.Z,
                               Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6,
                               Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12 };

            foreach (var key in keys)
            {
                ThemeKeyCombo.Items.Add(key.ToString());
                PowerKeyCombo.Items.Add(key.ToString());
            }

            // 设置当前值
            ThemeCtrl.IsChecked = _settings.ToggleThemeHotkey.Ctrl;
            ThemeAlt.IsChecked = _settings.ToggleThemeHotkey.Alt;
            ThemeShift.IsChecked = _settings.ToggleThemeHotkey.Shift;
            ThemeWin.IsChecked = _settings.ToggleThemeHotkey.Win;
            ThemeHotkeyEnabled.IsChecked = _settings.ToggleThemeHotkey.Enabled;

            PowerCtrl.IsChecked = _settings.TogglePowerHotkey.Ctrl;
            PowerAlt.IsChecked = _settings.TogglePowerHotkey.Alt;
            PowerShift.IsChecked = _settings.TogglePowerHotkey.Shift;
            PowerWin.IsChecked = _settings.TogglePowerHotkey.Win;
            PowerHotkeyEnabled.IsChecked = _settings.TogglePowerHotkey.Enabled;

            // 设置主键选择（settings 中存储的是 VK 码，需转换为 Key 枚举）
            string themeKey = KeyInterop.KeyFromVirtualKey(_settings.ToggleThemeHotkey.Key).ToString();
            string powerKey = KeyInterop.KeyFromVirtualKey(_settings.TogglePowerHotkey.Key).ToString();
            ThemeKeyCombo.SelectedItem = themeKey;
            PowerKeyCombo.SelectedItem = powerKey;

            UpdateHotkeyDisplay();
        }

        private void UpdateHotkeyDisplay()
        {
            ThemeHotkeyBox.Text = BuildHotkeyString(ThemeCtrl, ThemeAlt, ThemeShift, ThemeWin, ThemeKeyCombo);
            PowerHotkeyBox.Text = BuildHotkeyString(PowerCtrl, PowerAlt, PowerShift, PowerWin, PowerKeyCombo);
        }

        private string BuildHotkeyString(CheckBox ctrl, CheckBox alt, CheckBox shift, CheckBox win, ComboBox keyCombo)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (ctrl.IsChecked == true) parts.Add("Ctrl");
            if (alt.IsChecked == true) parts.Add("Alt");
            if (shift.IsChecked == true) parts.Add("Shift");
            if (win.IsChecked == true) parts.Add("Win");
            parts.Add(keyCombo.SelectedItem?.ToString() ?? "T");
            return string.Join(" + ", parts);
        }

        public void ApplySettings(UserSettings settings)
        {
            settings.ToggleThemeHotkey.Ctrl = ThemeCtrl.IsChecked == true;
            settings.ToggleThemeHotkey.Alt = ThemeAlt.IsChecked == true;
            settings.ToggleThemeHotkey.Shift = ThemeShift.IsChecked == true;
            settings.ToggleThemeHotkey.Win = ThemeWin.IsChecked == true;
            settings.ToggleThemeHotkey.Enabled = ThemeHotkeyEnabled.IsChecked == true;

            if (ThemeKeyCombo.SelectedItem != null && System.Enum.TryParse<Key>(ThemeKeyCombo.SelectedItem.ToString(), out Key themeKey))
                settings.ToggleThemeHotkey.Key = KeyInterop.VirtualKeyFromKey(themeKey);

            settings.TogglePowerHotkey.Ctrl = PowerCtrl.IsChecked == true;
            settings.TogglePowerHotkey.Alt = PowerAlt.IsChecked == true;
            settings.TogglePowerHotkey.Shift = PowerShift.IsChecked == true;
            settings.TogglePowerHotkey.Win = PowerWin.IsChecked == true;
            settings.TogglePowerHotkey.Enabled = PowerHotkeyEnabled.IsChecked == true;

            if (PowerKeyCombo.SelectedItem != null && System.Enum.TryParse<Key>(PowerKeyCombo.SelectedItem.ToString(), out Key powerKey))
                settings.TogglePowerHotkey.Key = KeyInterop.VirtualKeyFromKey(powerKey);
        }

        /// <summary>刷新 UI 以反映当前设置</summary>
        public void Refresh(UserSettings settings)
        {
            ThemeCtrl.IsChecked = settings.ToggleThemeHotkey.Ctrl;
            ThemeAlt.IsChecked = settings.ToggleThemeHotkey.Alt;
            ThemeShift.IsChecked = settings.ToggleThemeHotkey.Shift;
            ThemeWin.IsChecked = settings.ToggleThemeHotkey.Win;
            ThemeHotkeyEnabled.IsChecked = settings.ToggleThemeHotkey.Enabled;

            PowerCtrl.IsChecked = settings.TogglePowerHotkey.Ctrl;
            PowerAlt.IsChecked = settings.TogglePowerHotkey.Alt;
            PowerShift.IsChecked = settings.TogglePowerHotkey.Shift;
            PowerWin.IsChecked = settings.TogglePowerHotkey.Win;
            PowerHotkeyEnabled.IsChecked = settings.TogglePowerHotkey.Enabled;

            string themeKey = KeyInterop.KeyFromVirtualKey(settings.ToggleThemeHotkey.Key).ToString();
            string powerKey = KeyInterop.KeyFromVirtualKey(settings.TogglePowerHotkey.Key).ToString();
            ThemeKeyCombo.SelectedItem = themeKey;
            PowerKeyCombo.SelectedItem = powerKey;

            UpdateHotkeyDisplay();
        }
    }
}
