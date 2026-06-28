using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using SmartShift.Core;
using SmartShift.Core.Configuration;
using SmartShift.Core.Theme;

namespace SmartShift.UI.Pages
{
    public partial class AboutPage : Page
    {
        private readonly UserSettings _settings;
        private bool _isInitializing;

        public AboutPage(UserSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            _isInitializing = true;
            AutoStartCheckBox.IsChecked = _settings.AutoStart;
            _isInitializing = false;
            UpdateIcon();
        }

        public void UpdateIcon()
        {
            var theme = ThemeManager.GetCurrentTheme();
            var iconName = theme == AppTheme.Light ? "icon_light_hd.png" : "icon_dark_hd.png";
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri("pack://application:,,,/Assets/" + iconName);
            img.DecodePixelWidth = 128;
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            AppIconImage.Source = img;
        }

        private void GitHubLink_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start("https://github.com/smartshift/SmartShift");
            }
            catch
            {
                MessageBox.Show("无法打开链接", "SmartShift", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AutoStartCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _settings.AutoStart = true;
            (Application.Current as App)?.RegisterAutoStart();
        }

        private void AutoStartCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _settings.AutoStart = false;
            (Application.Current as App)?.UnregisterAutoStart();
        }
    }
}
