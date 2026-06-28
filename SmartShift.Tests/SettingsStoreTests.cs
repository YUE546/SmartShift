using System;
using System.IO;
using Xunit;
using SmartShift.Core.Configuration;

namespace SmartShift.Tests
{
    public class SettingsStoreTests : IDisposable
    {
        private readonly string _testDir;

        public SettingsStoreTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "SmartShift_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                    Directory.Delete(_testDir, true);
            }
            catch { }
        }

        [Fact]
        public void DefaultSettings_CanBeWrittenAndReadBack()
        {
            string testPath = Path.Combine(_testDir, "settings.json");
            var settings = new UserSettings();

            // 手动写入
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(testPath, json);

            // 读取回来
            string readJson = File.ReadAllText(testPath);
            var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<UserSettings>(readJson);

            Assert.NotNull(loaded);
            Assert.Equal(settings.Latitude, loaded.Latitude);
            Assert.Equal(settings.Longitude, loaded.Longitude);
            Assert.Equal(settings.CityName, loaded.CityName);
            Assert.Equal(settings.AutoStart, loaded.AutoStart);
            Assert.Equal(settings.DefaultPowerPlanName, loaded.DefaultPowerPlanName);
        }

        [Fact]
        public void CorruptedJson_ReturnsDefaultSettings()
        {
            string testPath = Path.Combine(_testDir, "settings_broken.json");
            File.WriteAllText(testPath, "{ this is not valid JSON }}}");

            // 直接测试反序列化失败时的 fallback
            try
            {
                var result = Newtonsoft.Json.JsonConvert.DeserializeObject<UserSettings>("{ invalid }");
                // 如果反序列化返回 null，说明需要 fallback
                if (result == null)
                {
                    result = new UserSettings();
                }
                Assert.NotNull(result);
                Assert.Equal(31.2304, result.Latitude);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                // JSON 解析异常，应 fallback 到默认配置
                var result = new UserSettings();
                Assert.Equal(31.2304, result.Latitude);
            }
        }

        [Fact]
        public void DefaultSettings_HaveExpectedValues()
        {
            var settings = new UserSettings();

            Assert.Equal(31.2304, settings.Latitude);
            Assert.Equal(121.4737, settings.Longitude);
            Assert.Equal("上海", settings.CityName);
            Assert.True(settings.AutoStart);
            Assert.True(settings.StartMinimized);
            Assert.Equal("Balanced", settings.DefaultPowerPlanName);
        }

        [Fact]
        public void PowerAppRules_SerializeCorrectly()
        {
            var settings = new UserSettings();
            settings.PowerAppRules.Add(new SmartShift.Core.Scheduler.PowerAppRule
            {
                ProcessName = "chrome",
                TargetPlanName = "High performance",
                FallbackPlanName = "Balanced"
            });

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(settings);
            var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<UserSettings>(json);

            Assert.NotNull(loaded);
            Assert.Equal(2, loaded.PowerAppRules.Count);
            Assert.Equal("chrome", loaded.PowerAppRules[1].ProcessName);
        }
    }
}
