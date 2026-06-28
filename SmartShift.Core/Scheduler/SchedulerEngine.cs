using System;
using System.Collections.Generic;
using System.Linq;
using SmartShift.Core.AppRules;
using SmartShift.Core.Configuration;
using SmartShift.Core.Power;
using SmartShift.Core.Theme;

namespace SmartShift.Core.Scheduler
{
    public class SchedulerEngine
    {
        private readonly UserSettings _settings;
        private readonly Func<PowerPlan> _getActivePlan;
        private readonly Func<AppTheme> _getCurrentTheme;
        private readonly Action<AppTheme> _setTheme;
        private readonly Func<string, bool> _setActivePlanByName;

        private System.Threading.Timer _timer;
        private readonly Dictionary<string, DateTime> _lastTriggerTime = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private DateTime? _userOverrideUntil;
        private const int DebounceMinutes = 2;
        private const int ProcessCacheExpirySeconds = 30;
        private IReadOnlyList<string> _cachedProcesses;
        private DateTime _processCacheTime;

        public event EventHandler<string> StateChanged;

        public SchedulerEngine(
            UserSettings settings,
            Func<PowerPlan> getActivePlan,
            Func<AppTheme> getCurrentTheme,
            Action<AppTheme> setTheme,
            Func<string, bool> setActivePlanByName)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _getActivePlan = getActivePlan ?? throw new ArgumentNullException(nameof(getActivePlan));
            _getCurrentTheme = getCurrentTheme ?? throw new ArgumentNullException(nameof(getCurrentTheme));
            _setTheme = setTheme ?? throw new ArgumentNullException(nameof(setTheme));
            _setActivePlanByName = setActivePlanByName ?? throw new ArgumentNullException(nameof(setActivePlanByName));
        }

        public void Start()
        {
            _timer = new System.Threading.Timer(
                _ => TickNow(DateTime.Now, null),
                null,
                TimeSpan.FromSeconds(0),
                TimeSpan.FromMinutes(1));
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        /// <summary>手动切换主题后调用，阻止调度器在当前时段内自动覆盖</summary>
        public void NotifyUserOverride(TimeSpan currentPeriodEnd)
        {
            _userOverrideUntil = DateTime.Now + currentPeriodEnd;
        }

        /// <summary>单次检查（供测试和定时触发使用）</summary>
        public void TickNow(DateTime now, IReadOnlyList<string> activeProcesses)
        {
            try
            {
                CheckTheme(now);
                CheckPowerPlan(now, activeProcesses ?? GetCachedActiveProcessNames());
            }
            catch (Exception ex)
            {
                Logger.Error($"SchedulerEngine.TickNow 异常: {ex.Message}");
            }
        }

        /// <summary>获取缓存的进程列表，30秒内复用避免频繁调用 Process.GetProcesses()</summary>
        private IReadOnlyList<string> GetCachedActiveProcessNames()
        {
            if (_cachedProcesses != null && (DateTime.Now - _processCacheTime).TotalSeconds < ProcessCacheExpirySeconds)
                return _cachedProcesses;

            _cachedProcesses = AppWatcher.GetActiveProcessNames();
            _processCacheTime = DateTime.Now;
            return _cachedProcesses;
        }

        private void CheckTheme(DateTime now)
        {
            var rule = _settings.ThemeRule;
            if (rule == null || rule.Mode == ThemeRuleMode.Disabled)
                return;

            // 检查用户手动覆盖
            if (_userOverrideUntil.HasValue && now < _userOverrideUntil.Value)
                return;

            AppTheme currentTheme = _getCurrentTheme();
            bool shouldBeLight;

            switch (rule.Mode)
            {
                case ThemeRuleMode.FixedTimeRange:
                    shouldBeLight = TimeRangeRule.IsLightTime(
                        now.TimeOfDay, rule.LightStartTime, rule.DarkStartTime);
                    break;

                case ThemeRuleMode.SunriseSunset:
                    shouldBeLight = SunriseSunsetRule.IsLightTimeBySunriseSunset(
                        now.TimeOfDay,
                        _settings.Latitude,
                        _settings.Longitude,
                        rule.SunriseOffsetMinutes,
                        rule.SunsetOffsetMinutes);
                    break;

                default:
                    return;
            }

            AppTheme expectedTheme = shouldBeLight ? AppTheme.Light : AppTheme.Dark;

            if (expectedTheme != currentTheme)
            {
                string key = $"theme_{expectedTheme}";
                if (IsDebounced(key, now))
                    return;

                _setTheme(expectedTheme);
                _lastTriggerTime[key] = now;
                _userOverrideUntil = null;
                StateChanged?.Invoke(this, $"主题切换为 {expectedTheme}");
                Logger.Info($"自动切换主题为 {expectedTheme}");
            }
        }

        private void CheckPowerPlan(DateTime now, IReadOnlyList<string> activeProcesses)
        {
            if (_settings.PowerAppRules == null || _settings.PowerAppRules.Count == 0)
                return;

            PowerPlan currentPlan = _getActivePlan();

            // 如果 CPU 规则已启用且当前计划为高 CPU 计划，跳过调度器检查，避免与 CPU 监控冲突
            if (_settings.CpuRule != null && _settings.CpuRule.Enabled &&
                !string.IsNullOrEmpty(_settings.CpuRule.HighCpuPlanName) &&
                currentPlan.Name.Equals(_settings.CpuRule.HighCpuPlanName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            PowerAppRule matchedRule = null;

            foreach (var rule in _settings.PowerAppRules)
            {
                if (string.IsNullOrWhiteSpace(rule.ProcessName))
                    continue;

                if (activeProcesses.Any(p => p.Equals(rule.ProcessName, StringComparison.OrdinalIgnoreCase)))
                {
                    matchedRule = rule;
                    break;
                }
            }

            string targetPlanName;

            if (matchedRule != null)
            {
                targetPlanName = matchedRule.TargetPlanName;
            }
            else
            {
                targetPlanName = _settings.DefaultPowerPlanName;
            }

            if (string.IsNullOrWhiteSpace(targetPlanName))
                return;

            if (!currentPlan.Name.Equals(targetPlanName, StringComparison.OrdinalIgnoreCase))
            {
                string key = $"power_{targetPlanName}";
                if (IsDebounced(key, now))
                    return;

                _setActivePlanByName(targetPlanName);
                _lastTriggerTime[key] = now;
                StateChanged?.Invoke(this, $"电源计划切换为 {targetPlanName}");
                Logger.Info($"自动切换电源计划为 {targetPlanName}");
            }
        }

        private bool IsDebounced(string key, DateTime now)
        {
            if (_lastTriggerTime.TryGetValue(key, out DateTime lastTime))
            {
                return (now - lastTime).TotalMinutes < DebounceMinutes;
            }
            return false;
        }
    }
}
