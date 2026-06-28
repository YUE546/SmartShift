using System;
using System.IO;
using Serilog;

namespace SmartShift.Core
{
    public static class Logger
    {
        private static ILogger _logger;
        private static bool _initialized;

        public static string LogDirectory { get; } =
            Path.Combine(GetUserDataDirectory(), "logs");

        private static string GetUserDataDirectory()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "SmartShift");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return dir;
            }
            catch
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        public static void Init()
        {
            if (_initialized) return;

            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                string logPath = Path.Combine(LogDirectory, "smartshift-.log");

                _logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(
                        logPath,
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();

                _initialized = true;
            }
            catch
            {
                // 日志初始化失败不应阻止程序启动
                _logger = null;
                _initialized = true;
            }
        }

        public static void Info(string message)
        {
            EnsureInitialized();
            _logger?.Information(message);
        }

        public static void Warning(string message)
        {
            EnsureInitialized();
            _logger?.Warning(message);
        }

        public static void Error(string message)
        {
            EnsureInitialized();
            _logger?.Error(message);
        }

        public static void Debug(string message)
        {
            EnsureInitialized();
            _logger?.Debug(message);
        }

        private static void EnsureInitialized()
        {
            if (!_initialized) Init();
        }
    }
}
