using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SmartShift.UI.Pages
{
    /// <summary>
    /// 进程图标提取工具：根据进程名提取其可执行文件的图标并转为 ImageSource。
    /// 带缓存，避免重复调用 Win32 API。
    /// </summary>
    internal static class ProcessIconHelper
    {
        private static readonly Dictionary<string, ImageSource> _cache =
            new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        /// <summary>根据进程名获取图标（找不到时返回 null）</summary>
        public static ImageSource GetIcon(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return null;

            if (_cache.TryGetValue(processName, out ImageSource cached))
                return cached;

            ImageSource result = null;
            try
            {
                string exePath = ResolveProcessExecutablePath(processName);
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    using (var icon = ExtractIconFromPath(exePath))
                    {
                        if (icon != null)
                            result = ToImageSource(icon);
                    }
                }
            }
            catch
            {
                // 单个进程图标提取失败不影响其他
            }

            _cache[processName] = result; // 允许存 null，避免重复尝试
            return result;
        }

        /// <summary>清除缓存（用于配置变更或进程列表更新后强制刷新）</summary>
        public static void ClearCache()
        {
            _cache.Clear();
        }

        private static string ResolveProcessExecutablePath(string processName)
        {
            // 通过 Process 获取 MainModule.FileName
            // 注意：该方法在访问受保护进程时会抛异常，需逐个尝试
            Process[] processes = null;
            try
            {
                processes = Process.GetProcessesByName(processName);
                foreach (var p in processes)
                {
                    try
                    {
                        // 在 64位系统上从 32位进程读取 64位进程的 MainModule 会抛 Win32Exception
                        // 这里仅尝试第一个能成功访问的
                        return p.MainModule?.FileName;
                    }
                    catch
                    {
                        // 继续尝试下一个
                    }
                }
            }
            catch
            {
                // ignore
            }
            finally
            {
                if (processes != null)
                {
                    foreach (var p in processes)
                    {
                        try { p.Dispose(); } catch { }
                    }
                }
            }

            return null;
        }

        private static Icon ExtractIconFromPath(string filePath)
        {
            // 优先使用 Icon.ExtractAssociatedIcon（更稳定）
            try
            {
                return Icon.ExtractAssociatedIcon(filePath);
            }
            catch
            {
                return null;
            }
        }

        private static ImageSource ToImageSource(Icon icon)
        {
            // 使用 Imaging.CreateBitmapSourceFromHIcon 转换
            BitmapSource bitmap = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(16, 16));

            // 冻结使其可跨线程使用
            bitmap.Freeze();
            return bitmap;
        }
    }
}
