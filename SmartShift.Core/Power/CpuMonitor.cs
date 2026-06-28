using System;
using System.Diagnostics;
using System.Threading;

namespace SmartShift.Core.Power
{
    /// <summary>
    /// CPU 使用率监控器，使用 PerformanceCounter 采样
    /// </summary>
    public class CpuMonitor : IDisposable
    {
        private PerformanceCounter _cpuCounter;
        private Timer _sampleTimer;
        private float _currentCpuUsage;
        private DateTime _highUsageStart;
        private bool _isHighUsage;
        private bool _alreadyTriggered;
        private bool _disposed;

        /// <summary>当前 CPU 使用率（0-100）</summary>
        public float CurrentCpuUsage => _currentCpuUsage;

        /// <summary>CPU 使用率是否超过阈值并持续了指定时间</summary>
        public bool IsHighUsageSustained => _isHighUsage;

        /// <summary>采样间隔（毫秒）</summary>
        public int SampleIntervalMs { get; set; } = 2000;

        /// <summary>CPU 高使用率阈值（百分比）</summary>
        public float Threshold { get; set; } = 80f;

        /// <summary>持续时间阈值（秒），CPU 使用率超过 Threshold 并持续此时间后才触发</summary>
        public int SustainSeconds { get; set; } = 30;

        public event EventHandler<CpuEventArgs> CpuHighUsageTriggered;
        public event EventHandler<CpuEventArgs> CpuHighUsageEnded;

        public CpuMonitor()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                // 首次读取返回 0，丢弃
                _cpuCounter.NextValue();
            }
            catch
            {
                // 某些系统可能无法创建 PerformanceCounter
                _cpuCounter = null;
            }
        }

        public void Start()
        {
            if (_cpuCounter == null) return;
            _sampleTimer = new Timer(SampleCallback, null, SampleIntervalMs, SampleIntervalMs);
        }

        public void Stop()
        {
            _sampleTimer?.Dispose();
            _sampleTimer = null;
        }

        /// <summary>手动采样一次（供测试使用）</summary>
        public float SampleOnce()
        {
            if (_cpuCounter == null) return 0;
            _currentCpuUsage = _cpuCounter.NextValue();
            return _currentCpuUsage;
        }

        private void SampleCallback(object state)
        {
            try
            {
                _currentCpuUsage = _cpuCounter.NextValue();
                bool aboveThreshold = _currentCpuUsage >= Threshold;

                if (aboveThreshold && !_isHighUsage)
                {
                    // 刚超过阈值，记录开始时间
                    _highUsageStart = DateTime.Now;
                    _isHighUsage = true;
                    _alreadyTriggered = false;
                }
                else if (aboveThreshold && _isHighUsage && !_alreadyTriggered)
                {
                    // 持续超过阈值，检查是否达到持续时间
                    if ((DateTime.Now - _highUsageStart).TotalSeconds >= SustainSeconds)
                    {
                        CpuHighUsageTriggered?.Invoke(this, new CpuEventArgs(_currentCpuUsage, Threshold, SustainSeconds));
                        _alreadyTriggered = true;
                    }
                }
                else if (!aboveThreshold && _isHighUsage)
                {
                    // 低于阈值，重置
                    _isHighUsage = false;
                    if (_alreadyTriggered)
                        CpuHighUsageEnded?.Invoke(this, new CpuEventArgs(_currentCpuUsage, Threshold, SustainSeconds));
                    _alreadyTriggered = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"CpuMonitor 采样异常: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _cpuCounter?.Dispose();
                _disposed = true;
            }
        }
    }

    public class CpuEventArgs : EventArgs
    {
        public float CpuUsage { get; }
        public float Threshold { get; }
        public int SustainSeconds { get; }

        public CpuEventArgs(float cpuUsage, float threshold, int sustainSeconds)
        {
            CpuUsage = cpuUsage;
            Threshold = threshold;
            SustainSeconds = sustainSeconds;
        }
    }
}
