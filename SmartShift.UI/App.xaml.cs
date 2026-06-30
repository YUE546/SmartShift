using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using Hardcodet.Wpf.TaskbarNotification;
using SmartShift.Core;
using SmartShift.Core.Configuration;
using SmartShift.Core.Hotkeys;
using SmartShift.Core.Power;
using SmartShift.Core.Scheduler;
using SmartShift.Core.Theme;

namespace SmartShift.UI
{
    public partial class App : Application
    {
        private TaskbarIcon _taskbarIcon;
        private SchedulerEngine _schedulerEngine;
        private CpuMonitor _cpuMonitor;
        private HotkeyManager _hotkeyManager;
        private UserSettings _settings;
        private Mutex _mutex;
        private MainWindow _mainWindow;
        private bool _cpuTriggeredHighPlan;

        private const int HOTKEY_ID_TOGGLE_THEME = 1;
        private const int HOTKEY_ID_TOGGLE_POWER = 2;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 全局异常处理
            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"发生未处理的异常:\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                    "SmartShift 错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Error($"未处理异常: {args.Exception}");
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show($"发生严重错误:\n{ex?.Message}", "SmartShift 错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Error($"严重错误: {ex}");
            };

            // 防止多实例
            bool createdNew;
            _mutex = new Mutex(true, "SmartShift-v1.0-{5F8A3D7E-2B1C-4E9F-A6D0-7C3B8E1F2D4A}", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("SmartShift 已经在运行中。", "SmartShift", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            try
            {
                // 初始化日志
                Logger.Init();
                Logger.Info("SmartShift 启动");

                // 加载配置
                _settings = SettingsStore.Load();

                // 判断是否为开机自启（带 --autostart 参数）
                bool launchedViaAutoStart = e.Args != null &&
                    e.Args.Any(a => string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase));

                // 先创建托盘图标，确保后续应用主题时能正确更新图标
                InitializeTrayIcon();

                // 应用系统主题到 UI
                ApplySystemTheme();

                // 监听系统主题变化
                Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;

                // 注册开机自启
                if (_settings.AutoStart)
                {
                    RegisterAutoStart();
                }

                // 初始化调度引擎
                InitializeSchedulerEngine();

                // 初始化 CPU 监控
                InitializeCpuMonitor();

                // 非开机自启时才显示设置窗口；开机自启仅创建窗口（维持热键消息循环）但不显示
                if (!launchedViaAutoStart)
                {
                    ShowSettingsWindow();
                }
                else
                {
                    EnsureMainWindow();
                }

                // 启动通知
                if (_taskbarIcon != null)
                {
                    var tip = launchedViaAutoStart
                        ? "SmartShift 已在后台启动，右键托盘图标可操作"
                        : "SmartShift 已启动，右键托盘图标可操作";
                    _taskbarIcon.ShowBalloonTip("SmartShift", tip, BalloonIcon.Info);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动失败:\n{ex.Message}\n\n{ex.StackTrace}",
                    "SmartShift 错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Error($"启动失败: {ex}");
                Shutdown();
            }
        }

        private void ApplySystemTheme()
        {
            var theme = ThemeManager.GetCurrentTheme();
            SwitchAppTheme(theme);
        }

        private void SwitchAppTheme(AppTheme theme)
        {
            try
            {
                var dict = new ResourceDictionary();
                dict.Source = new Uri(theme == AppTheme.Dark
                    ? "pack://application:,,,/Themes/Dark.xaml"
                    : "pack://application:,,,/Themes/Light.xaml");

                Current.Resources.MergedDictionaries.Clear();
                Current.Resources.MergedDictionaries.Add(dict);

                // 同步更新托盘图标、提示文字、窗口图标和关于页图标
                UpdateTrayIcon();
                UpdateTrayTooltip();
                _mainWindow?.RefreshAboutIcon();
                if (_mainWindow != null && _mainWindow.IsLoaded)
                {
                    _mainWindow.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var iconName = theme == AppTheme.Light ? "icon_light_app.ico" : "icon_dark_app.ico";
                            _mainWindow.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(
                                new Uri($"pack://application:,,,/Assets/{iconName}"));
                        }
                        catch { }
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"切换 UI 主题失败: {ex.Message}");
            }
        }

        private void OnSystemPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            if (e.Category == Microsoft.Win32.UserPreferenceCategory.General)
            {
                try
                {
                    var theme = ThemeManager.GetCurrentTheme();
                    Dispatcher.Invoke(() => SwitchAppTheme(theme));
                }
                catch { }
            }
        }

        private void InitializeTrayIcon()
        {
            _taskbarIcon = new TaskbarIcon();
            _taskbarIcon.ToolTipText = "SmartShift";

            // 根据当前系统主题设置初始托盘图标
            UpdateTrayIcon();
            if (_taskbarIcon.Icon == null)
            {
                try { _taskbarIcon.Icon = System.Drawing.SystemIcons.Application; } catch { }
            }

            UpdateTrayTooltip();

            // 右键菜单
            var contextMenu = new System.Windows.Controls.ContextMenu();

            var headerItem = new System.Windows.Controls.MenuItem
            {
                Header = "SmartShift v1.0",
                IsEnabled = false,
                FontWeight = FontWeights.Bold
            };
            contextMenu.Items.Add(headerItem);
            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            var toggleThemeItem = new System.Windows.Controls.MenuItem { Header = "切换主题" };
            toggleThemeItem.Click += (s, args) =>
            {
                var newTheme = ThemeManager.Toggle();
                SwitchAppTheme(newTheme);
                UpdateTrayIcon();
                UpdateTrayTooltip();
                NotifySchedulerUserOverride();
                if (_settings.ShowNotificationOnSwitch)
                    _taskbarIcon.ShowBalloonTip("SmartShift", $"已切换到{(newTheme == AppTheme.Light ? "浅色" : "深色")}主题", BalloonIcon.Info);
            };
            contextMenu.Items.Add(toggleThemeItem);

            var powerItem = new System.Windows.Controls.MenuItem { Header = "切换电源计划" };
            powerItem.SubmenuOpened += (s, args) =>
            {
                powerItem.Items.Clear();
                PopulatePowerPlanMenuItems(powerItem);
            };
            // 初始填充一次
            PopulatePowerPlanMenuItems(powerItem);

            contextMenu.Items.Add(powerItem);
            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            var settingsItem = new System.Windows.Controls.MenuItem { Header = "设置..." };
            settingsItem.Click += (s, args) => ShowSettingsWindow();
            contextMenu.Items.Add(settingsItem);
            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            var exitItem = new System.Windows.Controls.MenuItem { Header = "退出 SmartShift" };
            exitItem.Click += (s, args) =>
            {
                _cpuMonitor?.Stop();
                _cpuMonitor?.Dispose();
                _schedulerEngine?.Stop();
                _taskbarIcon?.Dispose();
                Shutdown();
            };
            contextMenu.Items.Add(exitItem);

            _taskbarIcon.ContextMenu = contextMenu;
            _taskbarIcon.TrayMouseDoubleClick += (s, args) => ShowSettingsWindow();
        }

        private void PopulatePowerPlanMenuItems(System.Windows.Controls.MenuItem powerItem)
        {
            try
            {
                var plans = PowerPlanManager.GetAllPlans();
                foreach (var plan in plans)
                {
                    var planItem = new System.Windows.Controls.MenuItem
                    {
                        Header = $"{plan.Name}{(plan.IsActive ? " (当前)" : "")}",
                        IsChecked = plan.IsActive,
                        IsCheckable = true,
                        Tag = plan
                    };
                    planItem.Click += (s, args) =>
                    {
                        var p = (PowerPlan)((System.Windows.Controls.MenuItem)s).Tag;
                        if (PowerPlanManager.SetActivePlan(p.Id))
                        {
                            UpdateTrayTooltip();
                            if (_settings.ShowNotificationOnSwitch)
                                _taskbarIcon.ShowBalloonTip("SmartShift", $"已切换到 {p.Name}", BalloonIcon.Info);
                        }
                    };
                    powerItem.Items.Add(planItem);
                }
            }
            catch (Exception ex) { Logger.Error($"枚举电源计划失败: {ex.Message}"); }
        }

        private void InitializeSchedulerEngine()
        {
            _schedulerEngine = new SchedulerEngine(
                _settings,
                () => PowerPlanManager.GetActivePlan(),
                () => ThemeManager.GetCurrentTheme(),
                (theme) =>
                {
                    ThemeManager.SetTheme(theme);
                    Dispatcher.Invoke(() =>
                    {
                        SwitchAppTheme(theme);
                        UpdateTrayIcon();
                        UpdateTrayTooltip();
                    });
                },
                (name) => PowerPlanManager.SetActivePlanByName(name));

            _schedulerEngine.StateChanged += (s, msg) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_settings.ShowNotificationOnSwitch)
                        _taskbarIcon?.ShowBalloonTip("SmartShift", msg, BalloonIcon.Info);
                });
            };

            _schedulerEngine.Start();
        }

        private void InitializeCpuMonitor()
        {
            var rule = _settings.CpuRule;
            if (rule == null || !rule.Enabled) return;

            _cpuMonitor = new CpuMonitor
            {
                Threshold = rule.Threshold,
                SustainSeconds = rule.SustainSeconds,
                SampleIntervalMs = 2000
            };

            _cpuMonitor.CpuHighUsageTriggered += (s, e) =>
            {
                Logger.Info($"CPU 使用率 {e.CpuUsage:F1}% 超过阈值 {e.Threshold:F1}% 并持续 {e.SustainSeconds} 秒，切换到 {rule.HighCpuPlanName}");
                bool ok = PowerPlanManager.SetActivePlanByName(rule.HighCpuPlanName);
                _cpuTriggeredHighPlan = true;
                Dispatcher.Invoke(() =>
                {
                    UpdateTrayTooltip();
                    if (rule.NotifyOnSwitch)
                        _taskbarIcon?.ShowBalloonTip("SmartShift", $"CPU 高负载，已切换到 {rule.HighCpuPlanName}", BalloonIcon.Info);
                });
            };

            _cpuMonitor.CpuHighUsageEnded += (s, e) =>
            {
                if (!_cpuTriggeredHighPlan) return;
                Logger.Info($"CPU 使用率降至 {e.CpuUsage:F1}%，切回 {rule.NormalPlanName}");
                PowerPlanManager.SetActivePlanByName(rule.NormalPlanName);
                _cpuTriggeredHighPlan = false;
                Dispatcher.Invoke(() =>
                {
                    UpdateTrayTooltip();
                    if (rule.NotifyOnSwitch)
                        _taskbarIcon?.ShowBalloonTip("SmartShift", $"CPU 负载恢复，已切回 {rule.NormalPlanName}", BalloonIcon.Info);
                });
            };

            _cpuMonitor.Start();
            Logger.Info($"CPU 监控已启动，阈值: {rule.Threshold}%，持续: {rule.SustainSeconds} 秒");
        }

        public void RestartCpuMonitor()
        {
            _cpuMonitor?.Stop();
            _cpuMonitor?.Dispose();
            _cpuMonitor = null;
            _cpuTriggeredHighPlan = false;
            InitializeCpuMonitor();
        }

        public void RegisterHotkeys()
        {
            if (_mainWindow == null) return;
            UnregisterHotkeys();

            try
            {
                var helper = new WindowInteropHelper(_mainWindow);
                IntPtr handle = helper.EnsureHandle();

                if (_hotkeyManager == null)
                {
                    _hotkeyManager = new HotkeyManager(handle);
                    var source = HwndSource.FromHwnd(handle);
                    source?.AddHook(HwndHook);
                }

                // 注册切换主题快捷键
                var themeCfg = _settings.ToggleThemeHotkey;
                if (themeCfg != null && themeCfg.Enabled)
                {
                    bool ok = _hotkeyManager.Register(new Hotkey
                    {
                        Id = HOTKEY_ID_TOGGLE_THEME,
                        Key = themeCfg.Key,
                        Ctrl = themeCfg.Ctrl,
                        Alt = themeCfg.Alt,
                        Shift = themeCfg.Shift,
                        Win = themeCfg.Win,
                        OnPressed = () =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                var newTheme = ThemeManager.Toggle();
                                SwitchAppTheme(newTheme);
                                UpdateTrayIcon();
                                UpdateTrayTooltip();
                                NotifySchedulerUserOverride();
                                if (_settings.ShowNotificationOnSwitch)
                                    _taskbarIcon?.ShowBalloonTip("SmartShift",
                                        $"快捷键：已切换到{(newTheme == AppTheme.Light ? "浅色" : "深色")}主题", BalloonIcon.Info);
                            });
                        }
                    });
                    if (!ok)
                        Logger.Warning($"注册主题快捷键失败: VK=0x{themeCfg.Key:X2}, Mods={themeCfg.Ctrl},{themeCfg.Alt},{themeCfg.Shift},{themeCfg.Win}");
                }

                // 注册切换电源快捷键
                var powerCfg = _settings.TogglePowerHotkey;
                if (powerCfg != null && powerCfg.Enabled)
                {
                    bool ok = _hotkeyManager.Register(new Hotkey
                    {
                        Id = HOTKEY_ID_TOGGLE_POWER,
                        Key = powerCfg.Key,
                        Ctrl = powerCfg.Ctrl,
                        Alt = powerCfg.Alt,
                        Shift = powerCfg.Shift,
                        Win = powerCfg.Win,
                        OnPressed = () =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                var plans = PowerPlanManager.GetAllPlans().ToList();
                                var active = plans.Find(p => p.IsActive);
                                int idx = active != null ? plans.IndexOf(active) : -1;
                                var next = plans[(idx + 1) % plans.Count];
                                PowerPlanManager.SetActivePlan(next.Id);
                                UpdateTrayTooltip();
                                if (_settings.ShowNotificationOnSwitch)
                                    _taskbarIcon?.ShowBalloonTip("SmartShift",
                                        $"快捷键：已切换到 {next.Name}", BalloonIcon.Info);
                            });
                        }
                    });
                    if (!ok)
                        Logger.Warning($"注册电源快捷键失败: VK=0x{powerCfg.Key:X2}, Mods={powerCfg.Ctrl},{powerCfg.Alt},{powerCfg.Shift},{powerCfg.Win}");
                }

                Logger.Info("快捷键已注册");
            }
            catch (Exception ex)
            {
                Logger.Error($"注册快捷键失败: {ex.Message}");
            }
        }

        private void UnregisterHotkeys()
        {
            _hotkeyManager?.UnregisterAll();
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                _hotkeyManager?.ProcessHotkeyMessage(wParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void SwitchAppThemePublic(AppTheme theme)
        {
            SwitchAppTheme(theme);
        }

        public void NotifySchedulerUserOverride()
        {
            if (_schedulerEngine == null) return;
            // 计算当前时段剩余时间作为覆盖期
            var now = DateTime.Now;
            var rule = _settings.ThemeRule;
            if (rule == null || rule.Mode == ThemeRuleMode.Disabled) return;

            TimeSpan periodEnd;
            if (rule.Mode == ThemeRuleMode.FixedTimeRange)
            {
                periodEnd = now.TimeOfDay < rule.DarkStartTime
                    ? rule.DarkStartTime - now.TimeOfDay
                    : rule.LightStartTime - now.TimeOfDay;
                if (periodEnd < TimeSpan.Zero) periodEnd += TimeSpan.FromHours(24);
            }
            else
            {
                // 日出日落模式，覆盖到下一个切换点（简化为2小时）
                periodEnd = TimeSpan.FromHours(2);
            }
            _schedulerEngine.NotifyUserOverride(periodEnd);
        }

        private void UpdateTrayIcon()
        {
            if (_taskbarIcon == null) return;
            try
            {
                var theme = ThemeManager.GetCurrentTheme();
                var iconName = theme == AppTheme.Light ? "icon_light_tray.ico" : "icon_dark_tray.ico";
                var iconStream = GetResourceStream(new Uri($"pack://application:,,,/Assets/{iconName}"))?.Stream;
                if (iconStream != null)
                    _taskbarIcon.Icon = new System.Drawing.Icon(iconStream);
            }
            catch { }
        }

        private void UpdateTrayTooltip()
        {
            if (_taskbarIcon == null) return;
            try
            {
                var theme = ThemeManager.GetCurrentTheme();
                var plan = PowerPlanManager.GetActivePlan();
                _taskbarIcon.ToolTipText = $"SmartShift — {(theme == AppTheme.Light ? "浅色" : "深色")} | {plan.Name}";
            }
            catch { _taskbarIcon.ToolTipText = "SmartShift"; }
        }

        private void EnsureMainWindow()
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow(_settings, _schedulerEngine, _cpuMonitor);
                RegisterHotkeys();
            }
        }

        private void ShowSettingsWindow()
        {
            EnsureMainWindow();
            _mainWindow.Show();
            _mainWindow.Activate();
        }

        public void RegisterAutoStart()
        {
            try
            {
                var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
                key?.SetValue("SmartShift", $"\"{System.Reflection.Assembly.GetExecutingAssembly().Location}\" --autostart");
                key?.Close();
                Logger.Info("已设置开机自启");
            }
            catch (Exception ex) { Logger.Error($"注册开机自启失败: {ex.Message}"); }
        }

        public void UnregisterAutoStart()
        {
            try
            {
                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
                key?.DeleteValue("SmartShift", throwOnMissingValue: false);
                key?.Close();
                Logger.Info("已取消开机自启");
            }
            catch (Exception ex) { Logger.Error($"取消开机自启失败: {ex.Message}"); }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;
            _hotkeyManager?.UnregisterAll();
            _hotkeyManager?.Dispose();
            _cpuMonitor?.Stop();
            _cpuMonitor?.Dispose();
            _schedulerEngine?.Stop();
            _taskbarIcon?.Dispose();
            GC.KeepAlive(_mutex);
            base.OnExit(e);
        }
    }
}
