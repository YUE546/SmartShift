using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SmartShift.Core.Configuration;
using SmartShift.Core.Location;
using SmartShift.Core.Scheduler;
using SmartShift.Core.Theme;

namespace SmartShift.UI.Pages
{
    public partial class ThemeSettingsPage : Page
    {
        private readonly UserSettings _settings;

        public ThemeSettingsPage(UserSettings settings)
        {
            InitializeComponent();
            _settings = settings;

            // 初始化城市下拉框
            foreach (var city in CityLookup.Cities)
            {
                CityComboBox.Items.Add(city.Name);
            }

            // 设置当前选择
            switch (_settings.ThemeRule.Mode)
            {
                case ThemeRuleMode.SunriseSunset:
                    SunriseSunsetRadio.IsChecked = true;
                    break;
                case ThemeRuleMode.FixedTimeRange:
                    FixedTimeRadio.IsChecked = true;
                    break;
                case ThemeRuleMode.Disabled:
                    DisabledRadio.IsChecked = true;
                    break;
            }

            // 设置城市：如果保存的城市不在列表中（如 IP 定位的自定义城市），先添加进去
            if (!string.IsNullOrEmpty(_settings.CityName))
            {
                bool exists = false;
                foreach (var item in CityComboBox.Items)
                {
                    if (item.ToString() == _settings.CityName)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                    CityComboBox.Items.Add(_settings.CityName);
                CityComboBox.SelectedItem = _settings.CityName;
            }
            SunriseOffsetBox.Text = _settings.ThemeRule.SunriseOffsetMinutes.ToString();
            SunsetOffsetBox.Text = _settings.ThemeRule.SunsetOffsetMinutes.ToString();
            LightStartTimeBox.Text = _settings.ThemeRule.LightStartTime.ToString(@"hh\:mm");
            DarkStartTimeBox.Text = _settings.ThemeRule.DarkStartTime.ToString(@"hh\:mm");

            UpdateSunriseSunsetInfo();
        }

        private void ThemeMode_Changed(object sender, RoutedEventArgs e)
        {
            SunriseSunsetPanel.IsEnabled = SunriseSunsetRadio.IsChecked == true;
            FixedTimePanel.IsEnabled = FixedTimeRadio.IsChecked == true;
            UpdateSunriseSunsetInfo();
        }

        private void CityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSunriseSunsetInfo();
        }

        private void UpdateSunriseSunsetInfo()
        {
            try
            {
                double lat = _settings.Latitude;
                double lon = _settings.Longitude;

                if (CityComboBox.SelectedItem != null)
                {
                    string cityName = CityComboBox.SelectedItem.ToString();
                    if (TryGetCityCoordinates(cityName, out double cityLat, out double cityLon))
                    {
                        lat = cityLat;
                        lon = cityLon;
                    }
                }

                var result = SunriseSunsetCalculator.CalculateToday(lat, lon);
                if (result.IsPolarDay)
                    SunriseSunsetInfo.Text = "当前为极昼（永不日落）";
                else if (result.IsPolarNight)
                    SunriseSunsetInfo.Text = "当前为极夜（永不日出）";
                else
                    SunriseSunsetInfo.Text = $"今日日出：{result.Sunrise:HH:mm}  ·  今日日落：{result.Sunset:HH:mm}";
            }
            catch
            {
                SunriseSunsetInfo.Text = "无法计算日出日落时间";
            }
        }

        private void SwitchToLight_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.SetTheme(AppTheme.Light);
            (Application.Current as App)?.SwitchAppThemePublic(AppTheme.Light);
            (Application.Current as App)?.NotifySchedulerUserOverride();
        }

        private void SwitchToDark_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.SetTheme(AppTheme.Dark);
            (Application.Current as App)?.SwitchAppThemePublic(AppTheme.Dark);
            (Application.Current as App)?.NotifySchedulerUserOverride();
        }

        private bool TryGetCityCoordinates(string cityName, out double latitude, out double longitude)
        {
            latitude = 0;
            longitude = 0;

            if (string.IsNullOrEmpty(cityName))
            {
                return false;
            }

            foreach (var city in CityLookup.Cities)
            {
                if (city.Name == cityName)
                {
                    latitude = city.Latitude;
                    longitude = city.Longitude;
                    return true;
                }
            }

            return false;
        }

        private async void DetectLocation_Click(object sender, RoutedEventArgs e)
        {
            DetectLocationButton.IsEnabled = false;
            LocationStatusText.Text = "正在检测位置...";
            LocationStatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextSecondaryBrush"];

            try
            {
                var result = await IpLocationService.DetectLocationAsync();

                // 更新设置
                _settings.Latitude = result.Latitude;
                _settings.Longitude = result.Longitude;
                _settings.CityName = result.City ?? "未知位置";

                // 尝试在城市列表中找到匹配项
                bool foundInList = false;
                foreach (var city in CityLookup.Cities)
                {
                    if (city.Name == result.City)
                    {
                        CityComboBox.SelectedItem = city.Name;
                        foundInList = true;
                        break;
                    }
                }

                // 如果不在列表中，添加到下拉框并选择
                if (!foundInList && !string.IsNullOrEmpty(result.City))
                {
                    CityComboBox.Items.Add(result.City);
                    CityComboBox.SelectedItem = result.City;
                }

                LocationStatusText.Text = $"已定位到：{result.City} ({result.Latitude:F2}, {result.Longitude:F2})";
                LocationStatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SuccessBrush"];
                UpdateSunriseSunsetInfo();
            }
            catch (Exception ex)
            {
                LocationStatusText.Text = "定位失败";
                LocationStatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["WarningBrush"];
                MessageBox.Show(
                    $"无法自动获取位置信息。\n\n错误详情：{ex.Message}\n\n请手动选择城市。",
                    "定位失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                DetectLocationButton.IsEnabled = true;
            }
        }

        public void ApplySettings(UserSettings settings)
        {
            if (SunriseSunsetRadio.IsChecked == true)
                settings.ThemeRule.Mode = ThemeRuleMode.SunriseSunset;
            else if (FixedTimeRadio.IsChecked == true)
                settings.ThemeRule.Mode = ThemeRuleMode.FixedTimeRange;
            else
                settings.ThemeRule.Mode = ThemeRuleMode.Disabled;

            if (CityComboBox.SelectedItem != null)
            {
                string cityName = CityComboBox.SelectedItem.ToString();
                settings.CityName = cityName;
                if (TryGetCityCoordinates(cityName, out double lat, out double lon))
                {
                    settings.Latitude = lat;
                    settings.Longitude = lon;
                }
            }

            // 日出日落偏移
            if (int.TryParse(SunriseOffsetBox.Text, out int sunriseOffset))
                settings.ThemeRule.SunriseOffsetMinutes = sunriseOffset;
            if (int.TryParse(SunsetOffsetBox.Text, out int sunsetOffset))
                settings.ThemeRule.SunsetOffsetMinutes = sunsetOffset;

            // 固定时间段
            if (TimeSpan.TryParse(LightStartTimeBox.Text, out TimeSpan lightStart))
                settings.ThemeRule.LightStartTime = lightStart;
            if (TimeSpan.TryParse(DarkStartTimeBox.Text, out TimeSpan darkStart))
                settings.ThemeRule.DarkStartTime = darkStart;
        }

        /// <summary>刷新 UI 以反映当前设置</summary>
        public void Refresh(UserSettings settings)
        {
            switch (settings.ThemeRule.Mode)
            {
                case ThemeRuleMode.SunriseSunset:
                    SunriseSunsetRadio.IsChecked = true;
                    break;
                case ThemeRuleMode.FixedTimeRange:
                    FixedTimeRadio.IsChecked = true;
                    break;
                case ThemeRuleMode.Disabled:
                    DisabledRadio.IsChecked = true;
                    break;
            }

            // 更新城市选择
            if (!string.IsNullOrEmpty(settings.CityName))
            {
                bool exists = false;
                foreach (var item in CityComboBox.Items)
                {
                    if (item.ToString() == settings.CityName)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                    CityComboBox.Items.Add(settings.CityName);
                CityComboBox.SelectedItem = settings.CityName;
            }

            SunriseOffsetBox.Text = settings.ThemeRule.SunriseOffsetMinutes.ToString();
            SunsetOffsetBox.Text = settings.ThemeRule.SunsetOffsetMinutes.ToString();
            LightStartTimeBox.Text = settings.ThemeRule.LightStartTime.ToString(@"hh\:mm");
            DarkStartTimeBox.Text = settings.ThemeRule.DarkStartTime.ToString(@"hh\:mm");

            SunriseSunsetPanel.IsEnabled = settings.ThemeRule.Mode == ThemeRuleMode.SunriseSunset;
            FixedTimePanel.IsEnabled = settings.ThemeRule.Mode == ThemeRuleMode.FixedTimeRange;
            UpdateSunriseSunsetInfo();
        }
    }
}
