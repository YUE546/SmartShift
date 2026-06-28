using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SmartShift.Core.Configuration;
using SmartShift.Core.Power;
using SmartShift.Core.Scheduler;

namespace SmartShift.UI.Pages
{
    public partial class PowerSettingsPage : Page, IDisposable
    {
        private readonly UserSettings _settings;
        private readonly CpuMonitor _cpuMonitor;
        private List<PowerAppRule> _rules;
        private DispatcherTimer _cpuRefreshTimer;
        private PerformanceCounter _cpuCounter;
        private bool _disposed;

        public PowerSettingsPage(UserSettings settings, CpuMonitor cpuMonitor)
        {
            InitializeComponent();
            _settings = settings;
            _cpuMonitor = cpuMonitor;

            // 枚举系统电源计划
            InitializePowerPlanComboBox(DefaultPlanComboBox, _settings.DefaultPowerPlanName);
            InitializePowerPlanComboBox(HighCpuPlanCombo);
            InitializePowerPlanComboBox(NormalPlanCombo);

            // 加载应用规则
            _rules = _settings.PowerAppRules.Select(r => new PowerAppRule
            {
                ProcessName = r.ProcessName,
                TargetPlanName = r.TargetPlanName,
                FallbackPlanName = r.FallbackPlanName
            }).ToList();
            RulesDataGrid.ItemsSource = _rules;

            // 加载 CPU 规则
            var cpuRule = _settings.CpuRule;
            CpuEnabled.IsChecked = cpuRule.Enabled;
            ThresholdBox.Text = cpuRule.Threshold.ToString("F0");
            SustainBox.Text = cpuRule.SustainSeconds.ToString();
            HighCpuPlanCombo.SelectedItem = cpuRule.HighCpuPlanName;
            NormalPlanCombo.SelectedItem = cpuRule.NormalPlanName;
            NotifyOnSwitch.IsChecked = cpuRule.NotifyOnSwitch;
            CpuConfigPanel.IsEnabled = cpuRule.Enabled;

            // 实时刷新 CPU 使用率：仅在 CpuMonitor 不可用时才创建本地计数器
            if (_cpuMonitor == null)
            {
                try
                {
                    _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                    _cpuCounter.NextValue(); // 首次读取丢弃
                }
                catch { _cpuCounter = null; }
            }

            _cpuRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _cpuRefreshTimer.Tick += CpuRefreshTimer_Tick;
            _cpuRefreshTimer.Start();
        }

        /// <summary>初始化电源计划下拉框，统一处理枚举和异常</summary>
        internal static void InitializePowerPlanComboBox(ComboBox comboBox, string selectedItem = null)
        {
            try
            {
                var plans = PowerPlanManager.GetAllPlans();
                foreach (var plan in plans)
                {
                    comboBox.Items.Add(plan.Name);
                }
            }
            catch
            {
                comboBox.Items.Add("Balanced");
                comboBox.Items.Add("High performance");
                comboBox.Items.Add("Power saver");
            }

            if (!string.IsNullOrEmpty(selectedItem) && comboBox.Items.Contains(selectedItem))
            {
                comboBox.SelectedItem = selectedItem;
            }
        }

        private void CpuRefreshTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                float usage = _cpuMonitor != null
                    ? _cpuMonitor.CurrentCpuUsage
                    : (_cpuCounter?.NextValue() ?? 0);
                CurrentCpuText.Text = $"当前 CPU 使用率：{usage:F1}%";
            }
            catch
            {
                CurrentCpuText.Text = "当前 CPU 使用率：不可用";
            }
        }

        // ====== 应用规则 ======

        private void AddRule_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new PowerRuleDialog();
            if (dialog.ShowDialog() == true)
            {
                _rules.Add(new PowerAppRule
                {
                    ProcessName = dialog.ProcessName,
                    TargetPlanName = dialog.TargetPlanName,
                    FallbackPlanName = dialog.FallbackPlanName
                });
                RulesDataGrid.Items.Refresh();
            }
        }

        private void RemoveRule_Click(object sender, RoutedEventArgs e)
        {
            if (RulesDataGrid.SelectedItem is PowerAppRule rule)
            {
                _rules.Remove(rule);
                RulesDataGrid.Items.Refresh();
            }
        }

        private void EditRule_Click(object sender, RoutedEventArgs e)
        {
            if (RulesDataGrid.SelectedItem is PowerAppRule rule)
            {
                var dialog = new PowerRuleDialog
                {
                    ProcessName = rule.ProcessName,
                    TargetPlanName = rule.TargetPlanName,
                    FallbackPlanName = rule.FallbackPlanName
                };
                if (dialog.ShowDialog() == true)
                {
                    rule.ProcessName = dialog.ProcessName;
                    rule.TargetPlanName = dialog.TargetPlanName;
                    rule.FallbackPlanName = dialog.FallbackPlanName;
                    RulesDataGrid.Items.Refresh();
                }
            }
        }

        // ====== CPU 规则 ======

        private void CpuEnabled_Changed(object sender, RoutedEventArgs e)
        {
            CpuConfigPanel.IsEnabled = CpuEnabled.IsChecked == true;
        }

        // ====== 保存 ======

        public void ApplySettings(UserSettings settings)
        {
            settings.DefaultPowerPlanName = DefaultPlanComboBox.SelectedItem as string ?? "Balanced";
            settings.PowerAppRules = _rules.ToList();

            settings.CpuRule.Enabled = CpuEnabled.IsChecked == true;
            if (float.TryParse(ThresholdBox.Text, out float threshold))
                settings.CpuRule.Threshold = threshold;
            if (int.TryParse(SustainBox.Text, out int sustain))
                settings.CpuRule.SustainSeconds = sustain;
            settings.CpuRule.HighCpuPlanName = HighCpuPlanCombo.SelectedItem as string ?? "High performance";
            settings.CpuRule.NormalPlanName = NormalPlanCombo.SelectedItem as string ?? "Balanced";
            settings.CpuRule.NotifyOnSwitch = NotifyOnSwitch.IsChecked == true;
        }

        /// <summary>刷新 UI 以反映当前设置</summary>
        public void Refresh(UserSettings settings)
        {
            DefaultPlanComboBox.SelectedItem = settings.DefaultPowerPlanName;

            // 重新加载应用规则
            _rules = settings.PowerAppRules.Select(r => new PowerAppRule
            {
                ProcessName = r.ProcessName,
                TargetPlanName = r.TargetPlanName,
                FallbackPlanName = r.FallbackPlanName
            }).ToList();
            RulesDataGrid.ItemsSource = _rules;
            RulesDataGrid.Items.Refresh();

            // 刷新 CPU 规则
            CpuEnabled.IsChecked = settings.CpuRule.Enabled;
            ThresholdBox.Text = settings.CpuRule.Threshold.ToString("F0");
            SustainBox.Text = settings.CpuRule.SustainSeconds.ToString();
            HighCpuPlanCombo.SelectedItem = settings.CpuRule.HighCpuPlanName;
            NormalPlanCombo.SelectedItem = settings.CpuRule.NormalPlanName;
            NotifyOnSwitch.IsChecked = settings.CpuRule.NotifyOnSwitch;
            CpuConfigPanel.IsEnabled = settings.CpuRule.Enabled;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cpuRefreshTimer?.Stop();
                _cpuCounter?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>电源规则编辑对话框</summary>
    public class PowerRuleDialog : Window
    {
        public string ProcessName { get; set; }
        public string TargetPlanName { get; set; }
        public string FallbackPlanName { get; set; }

        private ComboBox _processCombo;
        private ComboBox _targetPlanCombo;
        private ComboBox _fallbackPlanCombo;
        private TextBox _processFilterBox;
        private List<string> _allProcesses;

        public PowerRuleDialog()
        {
            Title = "编辑电源规则";
            Width = 460;
            Height = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            // ---- 关键修复 1：把应用当前激活的主题资源字典合并到本窗口 ----
            // 代码创建的 Window 不自动继承 App.xaml 的 MergedDictionaries
            foreach (var rd in Application.Current.Resources.MergedDictionaries)
            {
                if (!Resources.MergedDictionaries.Contains(rd))
                    Resources.MergedDictionaries.Add(rd);
            }

            // ---- 直接取当前激活主题的颜色资源 ----
            var bgBrush = Application.Current.Resources["BgSecondaryBrush"] as Brush;
            var fgBrush = Application.Current.Resources["TextPrimaryBrush"] as Brush;
            var inputBgBrush = Application.Current.Resources["InputBgBrush"] as Brush;
            var borderBrush = Application.Current.Resources["BorderBrush"] as Brush;
            if (bgBrush != null) Background = bgBrush;
            if (fgBrush != null) Foreground = fgBrush;

            // ---- 构造 ComboBoxItem 容器样式，确保下拉项文字色正确 ----
            var itemContainerStyle = new Style(typeof(ComboBoxItem));
            if (bgBrush != null)
                itemContainerStyle.Setters.Add(new Setter(Control.BackgroundProperty, bgBrush));
            if (fgBrush != null)
                itemContainerStyle.Setters.Add(new Setter(Control.ForegroundProperty, fgBrush));
            itemContainerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
            itemContainerStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(2, 1, 2, 1)));
            itemContainerStyle.Seal();

            // ---- 容器面板 ----
            var root = new Border { Padding = new Thickness(18) };
            if (bgBrush != null) root.Background = bgBrush;

            var panel = new StackPanel();

            // ---- 选择进程 ----
            var procLabel = new TextBlock
            {
                Text = "选择进程：",
                Margin = new Thickness(0, 0, 0, 6),
                FontSize = 13
            };
            if (fgBrush != null) procLabel.Foreground = fgBrush;
            panel.Children.Add(procLabel);

            _processFilterBox = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 8),
                FontSize = 13,
                Height = 32,
                Padding = new Thickness(8, 4, 8, 4)
            };
            if (fgBrush != null) _processFilterBox.Foreground = fgBrush;
            if (inputBgBrush != null) _processFilterBox.Background = inputBgBrush;
            if (borderBrush != null) _processFilterBox.BorderBrush = borderBrush;
            _processFilterBox.TextChanged += ProcessFilterBox_TextChanged;
            panel.Children.Add(_processFilterBox);

            // ---- 关键修复 2：process ComboBox 不再 IsEditable（我们已有上面的过滤框） ----
            // 自定义 ComboBox 模板不支持 IsEditable，强制禁用避免文本区用系统默认色
            _processCombo = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 18),
                FontSize = 13,
                Height = 32,
                Padding = new Thickness(8, 4, 8, 4),
                IsEditable = false,
                ItemContainerStyle = itemContainerStyle
            };
            if (fgBrush != null) _processCombo.Foreground = fgBrush;
            if (inputBgBrush != null) _processCombo.Background = inputBgBrush;
            if (borderBrush != null) _processCombo.BorderBrush = borderBrush;
            panel.Children.Add(_processCombo);

            LoadProcessList();

            // ---- 运行时切换到 ----
            var targetLabel = new TextBlock
            {
                Text = "运行时切换到：",
                Margin = new Thickness(0, 0, 0, 6),
                FontSize = 13
            };
            if (fgBrush != null) targetLabel.Foreground = fgBrush;
            panel.Children.Add(targetLabel);

            _targetPlanCombo = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 16),
                FontSize = 13,
                Height = 32,
                Padding = new Thickness(8, 4, 8, 4),
                ItemContainerStyle = itemContainerStyle
            };
            if (fgBrush != null) _targetPlanCombo.Foreground = fgBrush;
            if (inputBgBrush != null) _targetPlanCombo.Background = inputBgBrush;
            if (borderBrush != null) _targetPlanCombo.BorderBrush = borderBrush;
            panel.Children.Add(_targetPlanCombo);

            // ---- 退出后切回 ----
            var fallbackLabel = new TextBlock
            {
                Text = "退出后切回：",
                Margin = new Thickness(0, 0, 0, 6),
                FontSize = 13
            };
            if (fgBrush != null) fallbackLabel.Foreground = fgBrush;
            panel.Children.Add(fallbackLabel);

            _fallbackPlanCombo = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 24),
                FontSize = 13,
                Height = 32,
                Padding = new Thickness(8, 4, 8, 4),
                ItemContainerStyle = itemContainerStyle
            };
            if (fgBrush != null) _fallbackPlanCombo.Foreground = fgBrush;
            if (inputBgBrush != null) _fallbackPlanCombo.Background = inputBgBrush;
            if (borderBrush != null) _fallbackPlanCombo.BorderBrush = borderBrush;
            panel.Children.Add(_fallbackPlanCombo);

            // 加载电源计划选项
            PowerSettingsPage.InitializePowerPlanComboBox(_targetPlanCombo);
            PowerSettingsPage.InitializePowerPlanComboBox(_fallbackPlanCombo);

            _targetPlanCombo.SelectedItem = TargetPlanName ?? "High performance";
            _fallbackPlanCombo.SelectedItem = FallbackPlanName ?? "Balanced";

            if (!string.IsNullOrEmpty(ProcessName))
            {
                _processCombo.SelectedItem = ProcessName;
                _processFilterBox.Text = ProcessName;
            }

            // ---- 按钮区 ----
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var okButton = new Button
            {
                Content = "确定",
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(16, 8, 16, 8),
                FontSize = 13,
                IsDefault = true
            };
            // 使用 PrimaryButton 样式（主题内定义的命名样式）
            if (Application.Current.Resources.Contains("PrimaryButton"))
                okButton.Style = (Style)Application.Current.Resources["PrimaryButton"];

            okButton.Click += (s, e) =>
            {
                string proc;
                if (_processCombo.SelectedItem != null)
                    proc = _processCombo.SelectedItem.ToString();
                else
                    proc = (_processCombo.Text ?? "").Trim();

                if (string.IsNullOrEmpty(proc))
                {
                    MessageBox.Show("请选择或输入进程名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (proc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    proc = proc.Substring(0, proc.Length - 4);

                ProcessName = proc;
                TargetPlanName = _targetPlanCombo.SelectedItem as string ?? "Balanced";
                FallbackPlanName = _fallbackPlanCombo.SelectedItem as string ?? "Balanced";
                DialogResult = true;
            };

            var cancelButton = new Button
            {
                Content = "取消",
                Padding = new Thickness(16, 8, 16, 8),
                FontSize = 13,
                IsCancel = true
            };
            if (Application.Current.Resources.Contains("SecondaryButton"))
                cancelButton.Style = (Style)Application.Current.Resources["SecondaryButton"];

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            panel.Children.Add(buttonPanel);

            root.Child = panel;
            Content = root;
        }

        private void LoadProcessList()
        {
            try
            {
                _allProcesses = Process.GetProcesses()
                    .Select(p => { try { return p.ProcessName; } catch { return null; } })
                    .Where(n => n != null)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();
                RefreshProcessCombo(_allProcesses);
            }
            catch { _allProcesses = new List<string>(); }
        }

        private void ProcessFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = (_processFilterBox.Text ?? "").Trim().ToLowerInvariant();
            var filtered = string.IsNullOrEmpty(filter)
                ? _allProcesses
                : _allProcesses.Where(p => p.ToLowerInvariant().Contains(filter)).ToList();
            RefreshProcessCombo(filtered);
        }

        private void RefreshProcessCombo(List<string> processes)
        {
            object current = _processCombo.SelectedItem;
            _processCombo.Items.Clear();
            foreach (var name in processes) _processCombo.Items.Add(name);
            if (current != null && _processCombo.Items.Contains(current))
                _processCombo.SelectedItem = current;
        }
    }
}
