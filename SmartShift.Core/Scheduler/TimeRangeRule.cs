using System;

namespace SmartShift.Core.Scheduler
{
    /// <summary>
    /// 固定时间段判断逻辑
    /// </summary>
    public static class TimeRangeRule
    {
        /// <summary>
        /// 根据当前时间判断应处于浅色还是深色主题
        /// </summary>
        /// <param name="now">当前时间</param>
        /// <param name="lightStartTime">浅色主题开始时间</param>
        /// <param name="darkStartTime">深色主题开始时间</param>
        /// <returns>true 表示应切换到浅色主题，false 表示深色主题</returns>
        public static bool IsLightTime(TimeSpan now, TimeSpan lightStartTime, TimeSpan darkStartTime)
        {
            if (lightStartTime < darkStartTime)
            {
                // 例如 Light=7:00, Dark=19:00：7:00~19:00 浅色，其余深色
                return now >= lightStartTime && now < darkStartTime;
            }
            else
            {
                // 跨午夜（如 Light=22:00, Dark=7:00）
                return now >= lightStartTime || now < darkStartTime;
            }
        }
    }
}
