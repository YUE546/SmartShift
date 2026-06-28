using System;

namespace SmartShift.Core.Location
{
    public struct SunriseSunsetResult
    {
        public DateTime Sunrise { get; }
        public DateTime Sunset { get; }
        public bool IsPolarDay { get; }
        public bool IsPolarNight { get; }

        public SunriseSunsetResult(DateTime sunrise, DateTime sunset, bool isPolarDay, bool isPolarNight)
        {
            Sunrise = sunrise;
            Sunset = sunset;
            IsPolarDay = isPolarDay;
            IsPolarNight = isPolarNight;
        }
    }

    public static class SunriseSunsetCalculator
    {
        /// <summary>计算指定日期和坐标的日出日落时间</summary>
        public static SunriseSunsetResult Calculate(
            DateTime date,
            double latitude,
            double longitude,
            double utcOffsetHours)
        {
            if (latitude < -90 || latitude > 90)
                throw new ArgumentException("纬度必须在 -90 到 90 之间", nameof(latitude));
            if (longitude < -180 || longitude > 180)
                throw new ArgumentException("经度必须在 -180 到 180 之间", nameof(longitude));

            // 步骤 1 — 计算儒略日（忽略时间部分，仅使用日期）
            double jd = date.Date.ToOADate() + 2415018.5;

            // 步骤 2 — 儒略世纪
            double jc = (jd - 2451545.0) / 36525.0;

            // 步骤 3 — 太阳几何平黄经 L（度）
            double L = (280.46646 + jc * (36000.76983 + jc * 0.0003032)) % 360;
            if (L < 0) L += 360;

            // 步骤 4 — 太阳平近点角 M（度）
            double M = 357.52911 + jc * (35999.05029 - 0.0001537 * jc);

            // 步骤 5 — 地球轨道离心率 e
            double e = 0.016708634 - jc * (0.000042037 + 0.0000001267 * jc);

            // 步骤 6 — 太阳中心方程 C（度）
            double M_rad = M * Math.PI / 180;
            double C = Math.Sin(M_rad) * (1.914602 - jc * (0.004817 + 0.000014 * jc))
                     + Math.Sin(2 * M_rad) * (0.019993 - 0.000101 * jc)
                     + Math.Sin(3 * M_rad) * 0.000289;

            // 步骤 7 — 太阳真黄经 + 黄经章动修正
            double sunTrueLong = L + C;
            double sunAppLong = sunTrueLong - 0.00569 - 0.00478 * Math.Sin((125.04 - 1934.136 * jc) * Math.PI / 180);

            // 步骤 8 — 黄赤交角 + 交角章动修正（度）
            double obliq = 23 + (26 + (21.448 - jc * (46.815 + jc * (0.00059 - jc * 0.001813))) / 60) / 60;
            double obliqCorr = obliq + 0.00256 * Math.Cos((125.04 - 1934.136 * jc) * Math.PI / 180);

            // 步骤 9 — 太阳赤纬 decl（度）
            double obliqCorr_rad = obliqCorr * Math.PI / 180;
            double sunAppLong_rad = sunAppLong * Math.PI / 180;
            double decl = Math.Asin(Math.Sin(obliqCorr_rad) * Math.Sin(sunAppLong_rad)) * 180 / Math.PI;

            // 步骤 10 — 时差（Equation of Time，分钟）
            double y = Math.Tan(obliqCorr_rad / 2) * Math.Tan(obliqCorr_rad / 2);
            double L_rad = L * Math.PI / 180;
            double EoT = 4 * (180 / Math.PI) * (
                y * Math.Sin(2 * L_rad)
                - 2 * e * Math.Sin(M_rad)
                + 4 * e * y * Math.Sin(M_rad) * Math.Cos(2 * L_rad)
                - 0.5 * y * y * Math.Sin(4 * L_rad)
                - 1.25 * e * e * Math.Sin(2 * M_rad)
            );

            // 步骤 11 — 日出日落时角
            double lat_rad = latitude * Math.PI / 180;
            double decl_rad = decl * Math.PI / 180;
            double cosHourAngle = (Math.Cos(90.833 * Math.PI / 180) - Math.Sin(lat_rad) * Math.Sin(decl_rad))
                                / (Math.Cos(lat_rad) * Math.Cos(decl_rad));

            if (cosHourAngle > 1)
                return new SunriseSunsetResult(DateTime.MinValue, DateTime.MinValue, false, true); // 极夜

            if (cosHourAngle < -1)
                return new SunriseSunsetResult(DateTime.MinValue, DateTime.MinValue, true, false); // 极昼

            double ha = Math.Acos(cosHourAngle) * 180 / Math.PI;

            // 步骤 12 — 计算本地日出日落时间
            double solarNoon = 720 - 4 * longitude - EoT;
            double sunriseUTC = (solarNoon - 4 * ha) / 1440.0;
            double sunsetUTC = (solarNoon + 4 * ha) / 1440.0;

            DateTime sunriseLocal = date.Date.AddDays(sunriseUTC).AddHours(utcOffsetHours);
            DateTime sunsetLocal = date.Date.AddDays(sunsetUTC).AddHours(utcOffsetHours);

            return new SunriseSunsetResult(sunriseLocal, sunsetLocal, false, false);
        }

        /// <summary>使用系统当前时区计算今日日出日落</summary>
        public static SunriseSunsetResult CalculateToday(double latitude, double longitude)
        {
            double utcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Today).TotalHours;
            return Calculate(DateTime.Today, latitude, longitude, utcOffset);
        }
    }
}
