using System;
using Xunit;
using SmartShift.Core.Scheduler;
using SmartShift.Core.Theme;

namespace SmartShift.Tests
{
    public class TimeRangeRuleTests
    {
        [Fact]
        public void Light7Dark19_Now8_ReturnsLight()
        {
            var lightStart = new TimeSpan(7, 0, 0);
            var darkStart = new TimeSpan(19, 0, 0);
            var now = new TimeSpan(8, 0, 0);

            bool result = TimeRangeRule.IsLightTime(now, lightStart, darkStart);
            Assert.True(result);
        }

        [Fact]
        public void Light7Dark19_Now20_ReturnsDark()
        {
            var lightStart = new TimeSpan(7, 0, 0);
            var darkStart = new TimeSpan(19, 0, 0);
            var now = new TimeSpan(20, 0, 0);

            bool result = TimeRangeRule.IsLightTime(now, lightStart, darkStart);
            Assert.False(result);
        }

        [Fact]
        public void Light7Dark19_Now7_Boundary_ReturnsLight()
        {
            var lightStart = new TimeSpan(7, 0, 0);
            var darkStart = new TimeSpan(19, 0, 0);
            var now = new TimeSpan(7, 0, 0);

            bool result = TimeRangeRule.IsLightTime(now, lightStart, darkStart);
            Assert.True(result);
        }

        [Fact]
        public void Light7Dark19_Now19_Boundary_ReturnsDark()
        {
            var lightStart = new TimeSpan(7, 0, 0);
            var darkStart = new TimeSpan(19, 0, 0);
            var now = new TimeSpan(19, 0, 0);

            bool result = TimeRangeRule.IsLightTime(now, lightStart, darkStart);
            Assert.False(result);
        }

        [Fact]
        public void Light22Dark7_CrossMidnight_Now23_ReturnsLight()
        {
            // 跨午夜：22:00~7:00 浅色，7:00~22:00 深色
            var lightStart = new TimeSpan(22, 0, 0);
            var darkStart = new TimeSpan(7, 0, 0);
            var now = new TimeSpan(23, 0, 0);

            bool result = TimeRangeRule.IsLightTime(now, lightStart, darkStart);
            Assert.True(result);
        }

        [Fact]
        public void Light22Dark7_CrossMidnight_Now3_ReturnsLight()
        {
            var lightStart = new TimeSpan(22, 0, 0);
            var darkStart = new TimeSpan(7, 0, 0);
            var now = new TimeSpan(3, 0, 0);

            bool result = TimeRangeRule.IsLightTime(now, lightStart, darkStart);
            Assert.True(result);
        }

        [Fact]
        public void Light22Dark7_CrossMidnight_Now12_ReturnsDark()
        {
            var lightStart = new TimeSpan(22, 0, 0);
            var darkStart = new TimeSpan(7, 0, 0);
            var now = new TimeSpan(12, 0, 0);

            bool result = TimeRangeRule.IsLightTime(now, lightStart, darkStart);
            Assert.False(result);
        }

        [Fact]
        public void Light7Dark19_Now6_ReturnsDark()
        {
            var lightStart = new TimeSpan(7, 0, 0);
            var darkStart = new TimeSpan(19, 0, 0);
            var now = new TimeSpan(6, 0, 0);

            bool result = TimeRangeRule.IsLightTime(now, lightStart, darkStart);
            Assert.False(result);
        }
    }
}
