using System;
using System.IO;
using Newtonsoft.Json;

namespace SmartShift.Core.Configuration
{
    public static class SettingsStore
    {
        public static string ConfigPath { get; } =
            Path.Combine(GetUserDataDirectory(), "settings.json");

        private static string GetUserDataDirectory()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "SmartShift");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return dir;
            }
            catch
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        /// <summary>加载用户配置，不存在则返回默认配置</summary>
        public static UserSettings Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var settings = JsonConvert.DeserializeObject<UserSettings>(json);
                    if (settings != null)
                    {
                        return MigrateIfNeeded(settings);
                    }
                }
            }
            catch (Exception ex)
            {
                // JSON 格式损坏 → 重命名损坏文件，返回默认配置
                try
                {
                    string brokenPath = ConfigPath + ".broken";
                    File.Move(ConfigPath, brokenPath);
                    Logger.Warning($"配置文件损坏，已重命名为 {brokenPath}。错误: {ex.Message}");
                }
                catch
                {
                    // 重命名也失败，忽略
                }
            }

            // 文件不存在或损坏，返回默认配置并写入磁盘
            var defaults = new UserSettings();
            Save(defaults);
            return defaults;
        }

        /// <summary>
        /// 根据版本号迁移旧配置。当前版本 = 2。
        /// v1 → v2: 修复快捷键 VK 码（旧版可能存储了 WPF Key 枚举值而非 Windows VK 码）
        /// </summary>
        private static UserSettings MigrateIfNeeded(UserSettings settings)
        {
            if (settings.SettingsVersion < 2)
            {
                Logger.Info($"检测到旧版配置 (v{settings.SettingsVersion})，正在迁移快捷键设置到 v2");

                // 重置为默认快捷键配置（使用正确的 VK 码）
                var defaults = new UserSettings();
                settings.ToggleThemeHotkey = defaults.ToggleThemeHotkey;
                settings.TogglePowerHotkey = defaults.TogglePowerHotkey;
                settings.SettingsVersion = 2;

                // 保存迁移后的配置
                Save(settings);

                Logger.Info("快捷键设置已迁移到 v2（Ctrl+Alt+T / Ctrl+Alt+P）");
            }

            return settings;
        }

        /// <summary>保存用户配置到 JSON 文件</summary>
        public static void Save(UserSettings settings)
        {
            try
            {
                string dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Logger.Error($"保存配置失败: {ex.Message}");
            }
        }
    }
}
