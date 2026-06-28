using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;
using SmartShift.Core.Configuration;
using SmartShift.Core.Power;
using SmartShift.Core.Scheduler;
using SmartShift.Core.Theme;

namespace SmartShift.UI
{
    public partial class MainWindow : Window
    {
        private readonly UserSettings _settings;
        private readonly SchedulerEngine _schedulerEngine;
        private readonly CpuMonitor _cpuMonitor;
        private readonly Pages.ThemeSettingsPage _themePage;
        private readonly Pages.PowerSettingsPage _powerPage;
        private readonly Pages.HotkeySettingsPage _hotkeyPage;
        private readonly Pages.AboutPage _aboutPage;
        private UserSettings _snapshot;

        public MainWindow(UserSettings settings, SchedulerEngine schedulerEngine, CpuMonitor cpuMonitor)
        {
            InitializeComponent();

            _settings = settings;
            _schedulerEngine = schedulerEngine;
            _cpuMonitor = cpuMonitor;

            _themePage = new Pages.ThemeSettingsPage(settings);
            _powerPage = new Pages.PowerSettingsPage(settings, cpuMonitor);
            _hotkeyPage = new Pages.HotkeySettingsPage(settings);
            _aboutPage = new Pages.AboutPage(settings);

            ThemeFrame.Content = _themePage;
            PowerFrame.Content = _powerPage;
            HotkeyFrame.Content = _hotkeyPage;
            AboutFrame.Content = _aboutPage;

            // 根据当前主题设置窗口图标
            UpdateWindowIcon();

            // 窗口显示时保存设置快照，取消时可恢复
            IsVisibleChanged += (s, e) =>
            {
                if ((bool)e.NewValue)
                {
                    _snapshot = _settings.Clone();
                    RefreshPages();
                }
            };
        }

        private void UpdateWindowIcon()
        {
            try
            {
                var theme = ThemeManager.GetCurrentTheme();
                var iconName = theme == AppTheme.Light ? "icon_light_app.ico" : "icon_dark_app.ico";
                Icon = BitmapFrame.Create(new System.Uri($"pack://application:,,,/Assets/{iconName}"));
            }
            catch { }
        }

        private void SaveAndClose_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            _snapshot = null; // 已保存，清除快照
            Hide();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            RestoreSettings();
            Hide();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // 取消关闭，改为隐藏窗口以保持热键消息窗口存活
            e.Cancel = true;
            RestoreSettings();
            Hide();
            base.OnClosing(e);
        }

        /// <summary>取消时从快照恢复设置</summary>
        private void RestoreSettings()
        {
            if (_snapshot != null)
            {
                _settings.RestoreFrom(_snapshot);
                _snapshot = null;
                RefreshPages();
            }
        }

        /// <summary>刷新各页面 UI 以反映当前设置</summary>
        private void RefreshPages()
        {
            _themePage.Refresh(_settings);
            _powerPage.Refresh(_settings);
            _hotkeyPage.Refresh(_settings);
            _aboutPage.UpdateIcon();
        }

        public void RefreshAboutIcon() => _aboutPage?.UpdateIcon();

        private void SaveSettings()
        {
            try
            {
                _themePage.ApplySettings(_settings);
                _powerPage.ApplySettings(_settings);
                _hotkeyPage.ApplySettings(_settings);
                SettingsStore.Save(_settings);
                (Application.Current as App)?.RestartCpuMonitor();
                (Application.Current as App)?.RegisterHotkeys();
            }
            catch (Exception ex)
            {
                SmartShift.Core.Logger.Error($"保存设置失败: {ex.Message}");
            }
        }
    }
}
