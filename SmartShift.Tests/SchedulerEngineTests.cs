using System;
using System.Collections.Generic;
using Xunit;
using SmartShift.Core.Configuration;
using SmartShift.Core.Power;
using SmartShift.Core.Scheduler;
using SmartShift.Core.Theme;

namespace SmartShift.Tests
{
    public class SchedulerEngineTests
    {
        private UserSettings CreateTestSettings(ThemeRuleMode mode = ThemeRuleMode.SunriseSunset)
        {
            return new UserSettings
            {
                Latitude = 31.2304,
                Longitude = 121.4737,
                ThemeRule = new ThemeRule
                {
                    Mode = mode,
                    LightStartTime = new TimeSpan(7, 0, 0),
                    DarkStartTime = new TimeSpan(19, 0, 0),
                },
                DefaultPowerPlanName = "Balanced",
                PowerAppRules = new List<PowerAppRule>
                {
                    new PowerAppRule
                    {
                        ProcessName = "devenv",
                        TargetPlanName = "High performance",
                        FallbackPlanName = "Balanced"
                    }
                }
            };
        }

        [Fact]
        public void SunriseSunsetMode_Midday_SwitchesToLight()
        {
            var settings = CreateTestSettings(ThemeRuleMode.SunriseSunset);
            string themeCallArg = null;

            var engine = new SchedulerEngine(
                settings,
                getActivePlan: () => new PowerPlan(Guid.Empty, "Balanced", true),
                getCurrentTheme: () => AppTheme.Dark,
                setTheme: t => { themeCallArg = t.ToString(); },
                setActivePlanByName: name => true);

            // 夏至中午，应为浅色
            engine.TickNow(new DateTime(2026, 6, 21, 12, 0, 0), new string[0]);

            Assert.Equal("Light", themeCallArg);
        }

        [Fact]
        public void SunriseSunsetMode_Night_SwitchesToDark()
        {
            var settings = CreateTestSettings(ThemeRuleMode.SunriseSunset);
            string themeCallArg = null;

            var engine = new SchedulerEngine(
                settings,
                getActivePlan: () => new PowerPlan(Guid.Empty, "Balanced", true),
                getCurrentTheme: () => AppTheme.Light,
                setTheme: t => { themeCallArg = t.ToString(); },
                setActivePlanByName: name => true);

            // 夏至晚上 21:00，应为深色
            engine.TickNow(new DateTime(2026, 6, 21, 21, 0, 0), new string[0]);

            Assert.Equal("Dark", themeCallArg);
        }

        [Fact]
        public void AppRule_MatchedProcess_SwitchesToTargetPlan()
        {
            var settings = CreateTestSettings(ThemeRuleMode.Disabled);
            string powerCallArg = null;

            var engine = new SchedulerEngine(
                settings,
                getActivePlan: () => new PowerPlan(Guid.Empty, "Balanced", true),
                getCurrentTheme: () => AppTheme.Light,
                setTheme: t => { },
                setActivePlanByName: name => { powerCallArg = name; return true; });

            engine.TickNow(DateTime.Now, new List<string> { "devenv", "chrome" });

            Assert.Equal("High performance", powerCallArg);
        }

        [Fact]
        public void AppRule_NoMatch_SwitchesToDefaultPlan()
        {
            var settings = CreateTestSettings(ThemeRuleMode.Disabled);
            string powerCallArg = null;

            var engine = new SchedulerEngine(
                settings,
                getActivePlan: () => new PowerPlan(Guid.Empty, "High performance", true),
                getCurrentTheme: () => AppTheme.Light,
                setTheme: t => { },
                setActivePlanByName: name => { powerCallArg = name; return true; });

            engine.TickNow(DateTime.Now, new List<string> { "notepad", "calc" });

            Assert.Equal("Balanced", powerCallArg);
        }

        [Fact]
        public void Debounce_ConsecutiveTicks_DoNotRepeatTrigger()
        {
            var settings = CreateTestSettings(ThemeRuleMode.FixedTimeRange);
            settings.ThemeRule.LightStartTime = new TimeSpan(0, 0, 0);
            settings.ThemeRule.DarkStartTime = new TimeSpan(23, 59, 0);

            int themeCallCount = 0;
            var now = new DateTime(2026, 6, 21, 12, 0, 0);

            var engine = new SchedulerEngine(
                settings,
                getActivePlan: () => new PowerPlan(Guid.Empty, "Balanced", true),
                getCurrentTheme: () => AppTheme.Dark,
                setTheme: t => { themeCallCount++; },
                setActivePlanByName: name => true);

            engine.TickNow(now, new string[0]);
            engine.TickNow(now.AddSeconds(30), new string[0]);

            Assert.Equal(1, themeCallCount);
        }

        [Fact]
        public void FixedTimeMode_DayTime_ReturnsLight()
        {
            var settings = CreateTestSettings(ThemeRuleMode.FixedTimeRange);
            string themeCallArg = null;

            var engine = new SchedulerEngine(
                settings,
                getActivePlan: () => new PowerPlan(Guid.Empty, "Balanced", true),
                getCurrentTheme: () => AppTheme.Dark,
                setTheme: t => { themeCallArg = t.ToString(); },
                setActivePlanByName: name => true);

            engine.TickNow(new DateTime(2026, 6, 21, 10, 0, 0), new string[0]);

            Assert.Equal("Light", themeCallArg);
        }

        [Fact]
        public void FixedTimeMode_NightTime_ReturnsDark()
        {
            var settings = CreateTestSettings(ThemeRuleMode.FixedTimeRange);
            string themeCallArg = null;

            var engine = new SchedulerEngine(
                settings,
                getActivePlan: () => new PowerPlan(Guid.Empty, "Balanced", true),
                getCurrentTheme: () => AppTheme.Light,
                setTheme: t => { themeCallArg = t.ToString(); },
                setActivePlanByName: name => true);

            engine.TickNow(new DateTime(2026, 6, 21, 20, 0, 0), new string[0]);

            Assert.Equal("Dark", themeCallArg);
        }

        [Fact]
        public void DisabledMode_DoesNotSwitchTheme()
        {
            var settings = CreateTestSettings(ThemeRuleMode.Disabled);
            string themeCallArg = null;

            var engine = new SchedulerEngine(
                settings,
                getActivePlan: () => new PowerPlan(Guid.Empty, "Balanced", true),
                getCurrentTheme: () => AppTheme.Dark,
                setTheme: t => { themeCallArg = t.ToString(); },
                setActivePlanByName: name => true);

            engine.TickNow(new DateTime(2026, 6, 21, 12, 0, 0), new string[0]);

            Assert.Null(themeCallArg);
        }
    }
}
