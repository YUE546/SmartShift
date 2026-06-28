using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SmartShift.Core.Scheduler;

namespace SmartShift.Core.Configuration
{
    public class HotkeyConfig
    {
        public int Key { get; set; }
        public bool Ctrl { get; set; }
        public bool Alt { get; set; }
        public bool Shift { get; set; }
        public bool Win { get; set; }
        public bool Enabled { get; set; } = true;

        public HotkeyConfig() { }

        public HotkeyConfig(int key, bool ctrl, bool alt, bool shift, bool win, bool enabled = true)
        {
            Key = key;
            Ctrl = ctrl;
            Alt = alt;
            Shift = shift;
            Win = win;
            Enabled = enabled;
        }

        public override string ToString()
        {
            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            parts.Add($"0x{Key:X2}");
            return string.Join(" + ", parts);
        }
    }

    public class CpuRule
    {
        public bool Enabled { get; set; } = false;
        public float Threshold { get; set; } = 80f;
        public int SustainSeconds { get; set; } = 30;
        public string HighCpuPlanName { get; set; } = "High performance";
        public string NormalPlanName { get; set; } = "Balanced";
        public bool NotifyOnSwitch { get; set; } = true;
    }

    public class UserSettings
    {
        /// <summary>配置版本号，用于自动迁移旧配置</summary>
        public int SettingsVersion { get; set; } = 2;

        // -------- 主题设置 --------
        public ThemeRule ThemeRule { get; set; } = new ThemeRule
        {
            Mode = ThemeRuleMode.SunriseSunset,
            LightStartTime = new TimeSpan(7, 0, 0),
            DarkStartTime = new TimeSpan(19, 0, 0),
        };

        public double Latitude { get; set; } = 31.2304;
        public double Longitude { get; set; } = 121.4737;
        public string CityName { get; set; } = "上海";

        // -------- 电源设置 --------
        public string DefaultPowerPlanName { get; set; } = "Balanced";

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<PowerAppRule> PowerAppRules { get; set; } = new List<PowerAppRule>
        {
            new PowerAppRule
            {
                ProcessName = "devenv",
                TargetPlanName = "High performance",
                FallbackPlanName = "Balanced"
            }
        };

        // -------- CPU 规则 --------
        public CpuRule CpuRule { get; set; } = new CpuRule();

        // -------- 通用设置 --------
        public bool AutoStart { get; set; } = true;
        public bool StartMinimized { get; set; } = true;
        public bool ShowNotificationOnSwitch { get; set; } = true;

        // -------- 快捷键设置 --------
        public HotkeyConfig ToggleThemeHotkey { get; set; } = new HotkeyConfig
        {
            Key = 0x54, // VK_T
            Ctrl = true,
            Alt = true,
            Shift = false,
            Win = false,
            Enabled = true
        };

        public HotkeyConfig TogglePowerHotkey { get; set; } = new HotkeyConfig
        {
            Key = 0x50, // VK_P
            Ctrl = true,
            Alt = true,
            Shift = false,
            Win = false,
            Enabled = true
        };

        /// <summary>深拷贝当前设置，用于取消时恢复</summary>
        public UserSettings Clone()
        {
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<UserSettings>(json);
        }

        /// <summary>从另一个设置对象恢复所有属性值</summary>
        public void RestoreFrom(UserSettings source)
        {
            var json = JsonConvert.SerializeObject(source);
            JsonConvert.PopulateObject(json, this);
        }
    }
}
