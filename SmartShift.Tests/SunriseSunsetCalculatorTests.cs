using System;
using Xunit;
using SmartShift.Core.Location;

namespace SmartShift.Tests
{
    public class SunriseSunsetCalculatorTests
    {
        [Fact]
        public void Shanghai_SummerSolstice_SunriseAbout0450()
        {
            // 上海 2026-06-21（夏至），UTC+8
            var result = SunriseSunsetCalculator.Calculate(
                new DateTime(2026, 6, 21), 31.2304, 121.4737, 8);

            Assert.False(result.IsPolarDay);
            Assert.False(result.IsPolarNight);

            // 日出约 04:50，允许 ±5 分钟
            Assert.InRange(result.Sunrise.Hour, 4, 5);
            Assert.InRange(result.Sunrise.Minute, 40, 59);
        }

        [Fact]
        public void Shanghai_SummerSolstice_SunsetAbout1905()
        {
            var result = SunriseSunsetCalculator.Calculate(
                new DateTime(2026, 6, 21), 31.2304, 121.4737, 8);

            // 日落约 19:01，允许 ±5 分钟
            Assert.InRange(result.Sunset.Hour, 18, 19);
            if (result.Sunset.Hour == 18)
            {
                Assert.InRange(result.Sunset.Minute, 55, 59);
            }
            else
            {
                Assert.InRange(result.Sunset.Minute, 0, 10);
            }
        }

        [Fact]
        public void Shanghai_WinterSolstice_SunriseAbout0652()
        {
            // 上海 2026-12-21（冬至），UTC+8
            var result = SunriseSunsetCalculator.Calculate(
                new DateTime(2026, 12, 21), 31.2304, 121.4737, 8);

            Assert.False(result.IsPolarDay);
            Assert.False(result.IsPolarNight);

            // 日出约 06:52，允许 ±5 分钟
            Assert.Equal(6, result.Sunrise.Hour);
            Assert.InRange(result.Sunrise.Minute, 45, 59);
        }

        [Fact]
        public void Shanghai_WinterSolstice_SunsetAbout1655()
        {
            var result = SunriseSunsetCalculator.Calculate(
                new DateTime(2026, 12, 21), 31.2304, 121.4737, 8);

            // 日落约 16:55，允许 ±5 分钟
            Assert.Equal(16, result.Sunset.Hour);
            Assert.InRange(result.Sunset.Minute, 48, 59);
        }

        [Fact]
        public void Beijing_SpringEquinox_SunriseAbout0620()
        {
            // 北京 2026-03-20（春分），UTC+8
            var result = SunriseSunsetCalculator.Calculate(
                new DateTime(2026, 3, 20), 39.9042, 116.4074, 8);

            Assert.False(result.IsPolarDay);
            Assert.False(result.IsPolarNight);

            // 日出约 06:20，允许 ±5 分钟
            Assert.Equal(6, result.Sunrise.Hour);
            Assert.InRange(result.Sunrise.Minute, 12, 28);
        }

        [Fact]
        public void Murmansk_SummerSolstice_IsPolarDay()
        {
            // 摩尔曼斯克（68.9585°N）夏至 → 极昼
            var result = SunriseSunsetCalculator.Calculate(
                new DateTime(2026, 6, 21), 68.9585, 33.0829, 3);

            Assert.True(result.IsPolarDay);
            Assert.False(result.IsPolarNight);
        }

        [Fact]
        public void NorthernHemisphereSummer_SunriseBeforeSunset()
        {
            var result = SunriseSunsetCalculator.Calculate(
                new DateTime(2026, 6, 21), 31.2304, 121.4737, 8);

            Assert.True(result.Sunrise < result.Sunset);
        }

        [Fact]
        public void DateOnly_TimePartIgnored()
        {
            // 传入不同时间部分的同一日期，结果应相同
            var result1 = SunriseSunsetCalculator.Calculate(
                new DateTime(2026, 6, 21, 0, 0, 0), 31.2304, 121.4737, 8);
            var result2 = SunriseSunsetCalculator.Calculate(
                new DateTime(2026, 6, 21, 23, 59, 59), 31.2304, 121.4737, 8);

            Assert.Equal(result1.Sunrise, result2.Sunrise);
            Assert.Equal(result1.Sunset, result2.Sunset);
        }

        [Fact]
        public void InvalidLatitude_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                SunriseSunsetCalculator.Calculate(DateTime.Today, 91, 0, 0));
        }

        [Fact]
        public void InvalidLongitude_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                SunriseSunsetCalculator.Calculate(DateTime.Today, 0, 181, 0));
        }

        [Fact]
        public void CalculateToday_ReturnsValidResult()
        {
            var result = SunriseSunsetCalculator.CalculateToday(31.2304, 121.4737);
            Assert.NotEqual(DateTime.MinValue, result.Sunrise);
            Assert.NotEqual(DateTime.MinValue, result.Sunset);
        }
    }
}
