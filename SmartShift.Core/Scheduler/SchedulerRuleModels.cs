using System;

namespace SmartShift.Core.Scheduler
{
    public enum ThemeRuleMode
    {
        Disabled,
        FixedTimeRange,
        SunriseSunset
    }

    public class ThemeRule
    {
        public ThemeRuleMode Mode { get; set; } = ThemeRuleMode.SunriseSunset;
        public TimeSpan LightStartTime { get; set; } = new TimeSpan(7, 0, 0);
        public TimeSpan DarkStartTime { get; set; } = new TimeSpan(19, 0, 0);
        public int SunriseOffsetMinutes { get; set; } = 0;
        public int SunsetOffsetMinutes { get; set; } = 0;
    }

    public class PowerAppRule
    {
        public string ProcessName { get; set; }
        public string TargetPlanName { get; set; }
        public string FallbackPlanName { get; set; }
    }
}
