using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        private ObservableCollection<PowerAppRuleViewModel> _rules;
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

            // 加载应用规则（包装为 ViewModel 以支持图标绑定）
            _rules = new ObservableCollection<PowerAppRuleViewModel>();
            foreach (var r in _settings.PowerAppRules)
            {
                _rules.Add(new PowerAppRuleViewModel
                {
                    ProcessName = r.ProcessName,
                    TargetPlanName = r.TargetPlanName,
                    FallbackPlanName = r.FallbackPlanName,
                    Icon = ProcessIconHelper.GetIcon(r.ProcessName)
                });
            }
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
                _rules.Add(new PowerAppRuleViewModel
                {
                    ProcessName = dialog.ProcessName,
                    TargetPlanName = dialog.TargetPlanName,
                    FallbackPlanName = dialog.FallbackPlanName,
                    Icon = ProcessIconHelper.GetIcon(dialog.ProcessName)
                });
                RulesDataGrid.Items.Refresh();
            }
        }

        private void RemoveRule_Click(object sender, RoutedEventArgs e)
        {
            if (RulesDataGrid.SelectedItem is PowerAppRuleViewModel rule)
            {
                _rules.Remove(rule);
                RulesDataGrid.Items.Refresh();
            }
        }

        private void EditRule_Click(object sender, RoutedEventArgs e)
        {
            if (RulesDataGrid.SelectedItem is PowerAppRuleViewModel rule)
            {
                var dialog = new PowerRuleDialog
                {
                    ProcessName = rule.ProcessName,
                    TargetPlanName = rule.TargetPlanName,
                    FallbackPlanName = rule.FallbackPlanName
                };
                if (dialog.ShowDialog() == true)
                {
                    bool procChanged = !string.Equals(rule.ProcessName, dialog.ProcessName, StringComparison.OrdinalIgnoreCase);
                    rule.ProcessName = dialog.ProcessName;
                    rule.TargetPlanName = dialog.TargetPlanName;
                    rule.FallbackPlanName = dialog.FallbackPlanName;
                    if (procChanged)
                        rule.Icon = ProcessIconHelper.GetIcon(dialog.ProcessName);
                }
            }
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            int index = RulesDataGrid.SelectedIndex;
            if (index <= 0) return;
            var item = _rules[index];
            _rules.RemoveAt(index);
            _rules.Insert(index - 1, item);
            RulesDataGrid.SelectedIndex = index - 1;
            RulesDataGrid.Items.Refresh();
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            int index = RulesDataGrid.SelectedIndex;
            if (index < 0 || index >= _rules.Count - 1) return;
            var item = _rules[index];
            _rules.RemoveAt(index);
            _rules.Insert(index + 1, item);
            RulesDataGrid.SelectedIndex = index + 1;
            RulesDataGrid.Items.Refresh();
        }

        // ====== 拖拽调整优先级 ======

        private PowerAppRuleViewModel _dragItem;
        private int _dragIndex = -1;

        private void RulesDataGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragItem == null)
                return;

            // 启动拖拽
            try
            {
                DragDrop.DoDragDrop(RulesDataGrid, _dragItem, DragDropEffects.Move);
            }
            finally
            {
                _dragItem = null;
                _dragIndex = -1;
            }
        }

        private void RulesDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // 记录鼠标按下时的行/项，供 MouseMove 判断是否开始拖拽
            e.Row.PreviewMouseLeftButtonDown += (s, args) =>
            {
                if (e.Row.Item is PowerAppRuleViewModel item)
                {
                    _dragItem = item;
                    _dragIndex = e.Row.GetIndex();
                }
            };

            // 行 Header 显示从 1 开始的优先级编号
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        private void RulesDataGrid_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(PowerAppRuleViewModel)))
                e.Effects = DragDropEffects.Move;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void RulesDataGrid_Drop(object sender, DragEventArgs e)
        {
            if (_dragItem == null || _dragIndex < 0) return;

            // 计算落点行索引
            var targetRow = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (targetRow == null) return;
            int targetIndex = targetRow.GetIndex();
            if (targetIndex < 0 || targetIndex == _dragIndex) return;

            _rules.RemoveAt(_dragIndex);
            _rules.Insert(targetIndex, _dragItem);
            RulesDataGrid.SelectedIndex = targetIndex;
            RulesDataGrid.Items.Refresh();

            _dragItem = null;
            _dragIndex = -1;
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null && !(child is T))
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            return child as T;
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
            // 按当前列表顺序保存（顺序即优先级，列表顶部优先级最高）
            settings.PowerAppRules = _rules.Select(vm => new PowerAppRule
            {
                ProcessName = vm.ProcessName,
                TargetPlanName = vm.TargetPlanName,
                FallbackPlanName = vm.FallbackPlanName
            }).ToList();

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

            // 重新加载应用规则（保留拖拽后的顺序同步）
            _rules = new ObservableCollection<PowerAppRuleViewModel>();
            foreach (var r in settings.PowerAppRules)
            {
                _rules.Add(new PowerAppRuleViewModel
                {
                    ProcessName = r.ProcessName,
                    TargetPlanName = r.TargetPlanName,
                    FallbackPlanName = r.FallbackPlanName,
                    Icon = ProcessIconHelper.GetIcon(r.ProcessName)
                });
            }
            RulesDataGrid.ItemsSource = _rules;

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

    /// <summary>电源应用规则的 UI 视图模型，封装图标绑定</summary>
    public class PowerAppRuleViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private string _processName;
        private string _targetPlanName;
        private string _fallbackPlanName;
        private ImageSource _icon;

        public string ProcessName
        {
            get => _processName;
            set { _processName = value; OnPropertyChanged(nameof(ProcessName)); }
        }

        public string TargetPlanName
        {
            get => _targetPlanName;
            set { _targetPlanName = value; OnPropertyChanged(nameof(TargetPlanName)); }
        }

        public string FallbackPlanName
        {
            get => _fallbackPlanName;
            set { _fallbackPlanName = value; OnPropertyChanged(nameof(FallbackPlanName)); }
        }

        public ImageSource Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(nameof(Icon)); }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
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
        private List<ProcessListItem> _allProcesses;

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

            // 设置 ItemTemplate，使下拉项显示"图标 + 进程名"
            _processCombo.ItemTemplate = CreateProcessItemTemplate();
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
                // 在已加载的进程列表中找到匹配项作为 SelectedItem
                var match = _allProcesses.FirstOrDefault(p =>
                    string.Equals(p.Name, ProcessName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    _processCombo.SelectedItem = match;
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
                string proc = null;
                if (_processCombo.SelectedItem is ProcessListItem item)
                    proc = item.Name;
                else if (_processCombo.SelectedItem != null)
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

        /// <summary>系统进程黑名单，这些进程不应作为电源规则的目标</summary>
        private static readonly HashSet<string> SystemProcessBlacklist = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "dwm",       // 桌面窗口管理器
            "svchost",   // 服务宿主
            "system",    // 系统进程
            "explorer",  // 资源管理器
            "rundll32",  // DLL 执行器
            "csrss",     // 客户/服务运行时子系统
            "lsass",     // 本地安全机构子系统服务
            "smss",      // 会话管理器子系统
            "wininit",   // Windows 启动应用
            "winlogon",  // Windows 登录
            "services",  // 服务控制管理器
            "spoolsv",   // 打印后台处理
            "conhost",   // 控制台窗口主机
            "taskhostw", // 任务宿主
            "fontdrvhost",// 字体驱动宿主
            "sihost",    // Shell 交互主机
            "ctfmon",    // 文本服务框架
            "WUDFHost",  // 用户模式驱动程序框架
            "dllhost",   // COM Surrogate
            "RuntimeBroker",      // 运行时代理
            "backgroundTaskHost",  // 后台任务宿主
            "SearchIndexer",       // 搜索索引
            "SearchHost",          // 搜索宿主
            "StartMenuExperienceHost", // 开始菜单体验宿主
            "ShellExperienceHost",     // Shell 体验宿主
            "TextInputHost",           // 文本输入宿主
            "Widgets",                 // 小组件
        };

        private void LoadProcessList()
        {
            try
            {
                var names = Process.GetProcesses()
                    .Select(p => { try { return p.ProcessName; } catch { return null; } })
                    .Where(n => n != null)
                    .Where(n => !SystemProcessBlacklist.Contains(n))
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                _allProcesses = names
                    .Select(n => new ProcessListItem
                    {
                        Name = n,
                        Icon = ProcessIconHelper.GetIcon(n)
                    })
                    .ToList();
                RefreshProcessCombo(_allProcesses);
            }
            catch { _allProcesses = new List<ProcessListItem>(); }
        }

        private void ProcessFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = (_processFilterBox.Text ?? "").Trim().ToLowerInvariant();
            var filtered = string.IsNullOrEmpty(filter)
                ? _allProcesses
                : _allProcesses.Where(p => p.Name.ToLowerInvariant().Contains(filter)).ToList();
            RefreshProcessCombo(filtered);
        }

        private void RefreshProcessCombo(List<ProcessListItem> processes)
        {
            object current = _processCombo.SelectedItem;
            _processCombo.Items.Clear();
            foreach (var item in processes) _processCombo.Items.Add(item);
            if (current != null && _processCombo.Items.Contains(current))
                _processCombo.SelectedItem = current;
        }

        /// <summary>构造进程 ComboBox 项的 DataTemplate：图标 + 名称</summary>
        private DataTemplate CreateProcessItemTemplate()
        {
            // 用 XAML 字符串构造模板（最简洁的方式）
            string xaml = @"
<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
              xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <StackPanel Orientation=""Horizontal"">
        <Image Source=""{Binding Icon}"" Width=""16"" Height=""16""
               VerticalAlignment=""Center"" Margin=""0,0,8,0""/>
        <TextBlock Text=""{Binding Name}"" VerticalAlignment=""Center""/>
    </StackPanel>
</DataTemplate>";

            return (DataTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
        }
    }

    /// <summary>进程选择下拉框的项：进程名 + 图标</summary>
    public class ProcessListItem
    {
        public string Name { get; set; }
        public ImageSource Icon { get; set; }

        public override string ToString() => Name ?? string.Empty;

        // 重写 Equals/GetHashCode 使 Contains 能按名称匹配
        public override bool Equals(object obj)
        {
            if (obj is ProcessListItem other)
                return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
            return false;
        }

        public override int GetHashCode()
            => Name == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
    }
}
