using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SmartShift.Core.AppRules
{
    public static class AppWatcher
    {
        /// <summary>获取当前运行的所有进程名（去重，小写，不含 .exe 扩展名）</summary>
        public static IReadOnlyList<string> GetActiveProcessNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Process[] processes = Process.GetProcesses();
            try
            {
                foreach (var process in processes)
                {
                    try
                    {
                        names.Add(process.ProcessName.ToLowerInvariant());
                    }
                    catch
                    {
                        // 受保护进程可能拒绝访问，跳过
                    }
                }
            }
            finally
            {
                foreach (var process in processes)
                {
                    try { process.Dispose(); } catch { }
                }
            }

            return names.ToList().AsReadOnly();
        }

        /// <summary>检查指定进程是否正在运行（忽略大小写，忽略 .exe）</summary>
        public static bool IsProcessRunning(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            // 去掉 .exe 后缀
            string name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName.Substring(0, processName.Length - 4)
                : processName;

            try
            {
                Process[] processes = Process.GetProcessesByName(name);
                try
                {
                    return processes.Length > 0;
                }
                finally
                {
                    foreach (var p in processes)
                    {
                        try { p.Dispose(); } catch { }
                    }
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
