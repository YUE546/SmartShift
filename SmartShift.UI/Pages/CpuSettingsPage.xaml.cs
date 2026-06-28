using System.Windows;
using System.Windows.Controls;
using SmartShift.Core.Configuration;
using SmartShift.Core.Power;

namespace SmartShift.UI.Pages
{
    public partial class CpuSettingsPage : Page
    {
        private readonly UserSettings _settings;
        private readonly CpuMonitor _cpuMonitor;

        public CpuSettingsPage(UserSettings settings, CpuMonitor cpuMonitor)
        {
            InitializeComponent();
            _settings = settings;
            _cpuMonitor = cpuMonitor;

            // 填充电源计划下拉框
            try
            {
                var plans = PowerPlanManager.GetAllPlans();
                foreach (var plan in plans)
                {
                    HighCpuPlanCombo.Items.Add(plan.Name);
                    NormalPlanCombo.Items.Add(plan.Name);
                }
            }
            catch
            {
                HighCpuPlanCombo.Items.Add("High performance");
                HighCpuPlanCombo.Items.Add("Balanced");
                NormalPlanCombo.Items.Add("High performance");
                NormalPlanCombo.Items.Add("Balanced");
                NormalPlanCombo.Items.Add("Power saver");
            }

            // 设置当前值
            var rule = _settings.CpuRule;
            CpuEnabled.IsChecked = rule.Enabled;
            ThresholdBox.Text = rule.Threshold.ToString("F0");
            SustainBox.Text = rule.SustainSeconds.ToString();
            HighCpuPlanCombo.SelectedItem = rule.HighCpuPlanName;
            NormalPlanCombo.SelectedItem = rule.NormalPlanName;
            NotifyOnSwitch.IsChecked = rule.NotifyOnSwitch;

            CpuConfigPanel.IsEnabled = rule.Enabled;
        }

        private void CpuEnabled_Changed(object sender, RoutedEventArgs e)
        {
            CpuConfigPanel.IsEnabled = CpuEnabled.IsChecked == true;
        }

        private void RefreshCpu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_cpuMonitor != null)
                {
                    float usage = _cpuMonitor.SampleOnce();
                    CurrentCpuText.Text = $"{usage:F1}%";
                }
                else
                {
                    // 如果 CpuMonitor 未启动，临时采样
                    using (var counter = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", "_Total"))
                    {
                        counter.NextValue(); // 丢弃首次
                        System.Threading.Thread.Sleep(500);
                        float usage = counter.NextValue();
                        CurrentCpuText.Text = $"{usage:F1}%";
                    }
                }
            }
            catch
            {
                CurrentCpuText.Text = "不可用";
            }
        }

        public void ApplySettings(UserSettings settings)
        {
            settings.CpuRule.Enabled = CpuEnabled.IsChecked == true;

            if (float.TryParse(ThresholdBox.Text, out float threshold))
                settings.CpuRule.Threshold = threshold;

            if (int.TryParse(SustainBox.Text, out int sustain))
                settings.CpuRule.SustainSeconds = sustain;

            settings.CpuRule.HighCpuPlanName = HighCpuPlanCombo.SelectedItem as string ?? "High performance";
            settings.CpuRule.NormalPlanName = NormalPlanCombo.SelectedItem as string ?? "Balanced";
            settings.CpuRule.NotifyOnSwitch = NotifyOnSwitch.IsChecked == true;
        }
    }
}
