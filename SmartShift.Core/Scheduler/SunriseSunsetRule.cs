using System;
using SmartShift.Core.Location;
using SmartShift.Core.Theme;

namespace SmartShift.Core.Scheduler
{
    /// <summary>
    /// 日出日落规则判断逻辑
    /// </summary>
    public static class SunriseSunsetRule
    {
        /// <summary>
        /// 根据日出日落时间判断当前应处于浅色还是深色主题
        /// </summary>
        /// <param name="now">当前时间</param>
        /// <param name="latitude">纬度</param>
        /// <param name="longitude">经度</param>
        /// <param name="sunriseOffsetMinutes">日出偏移（分钟）</param>
        /// <param name="sunsetOffsetMinutes">日落偏移（分钟）</param>
        /// <returns>true 表示应切换到浅色主题，false 表示深色主题</returns>
        public static bool IsLightTimeBySunriseSunset(
            TimeSpan now,
            double latitude,
            double longitude,
            int sunriseOffsetMinutes = 0,
            int sunsetOffsetMinutes = 0)
        {
            var result = SunriseSunsetCalculator.CalculateToday(latitude, longitude);

            if (result.IsPolarDay)
                return true;  // 极昼：始终浅色

            if (result.IsPolarNight)
                return false; // 极夜：始终深色

            TimeSpan sunriseTime = result.Sunrise.TimeOfDay + TimeSpan.FromMinutes(sunriseOffsetMinutes);
            TimeSpan sunsetTime = result.Sunset.TimeOfDay + TimeSpan.FromMinutes(sunsetOffsetMinutes);

            return now >= sunriseTime && now < sunsetTime;
        }
    }
}
