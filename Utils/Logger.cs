using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace WordFlow.Utils
{
    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// 文件日志记录器
    /// </summary>
    public static class Logger
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WordFlow",
            "logs");
        
        private static readonly string LogFilePath = Path.Combine(
            LogDirectory, 
            $"wordflow_{DateTime.Now:yyyyMMdd}.log");
        
        private static readonly object LockObj = new object();

        /// <summary>
        /// 获取日志文件路径
        /// </summary>
        public static string GetLogFilePath()
        {
            return LogFilePath;
        }

        /// <summary>
        /// 获取日志目录路径
        /// </summary>
        public static string GetLogDirectory()
        {
            return LogDirectory;
        }

        /// <summary>
        /// 记录日志
        /// </summary>
        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            try
            {
                lock (LockObj)
                {
                    // 确保日志目录存在
                    if (!Directory.Exists(LogDirectory))
                    {
                        Directory.CreateDirectory(LogDirectory);
                    }

                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var levelStr = level.ToString().ToUpper();
                    var logLine = $"[{timestamp}] [{levelStr}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogFilePath, logLine);
                }
            }
            catch (Exception ex)
            {
                // 日志记录失败时静默处理，避免影响主程序
                System.Diagnostics.Debug.WriteLine($"Logger error: {ex.Message}");
            }
        }

        /// <summary>
        /// 记录调试日志
        /// </summary>
        public static void Debug(string message)
        {
            Log(message, LogLevel.Debug);
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        public static void Info(string message)
        {
            Log(message, LogLevel.Info);
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        public static void Warning(string message)
        {
            Log(message, LogLevel.Warning);
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        public static void Error(string message, Exception? ex = null)
        {
            var fullMessage = ex != null ? $"{message} - Exception: {ex.Message}\nStackTrace: {ex.StackTrace}" : message;
            Log(fullMessage, LogLevel.Error);
        }

        /// <summary>
        /// 清除所有日志文件
        /// </summary>
        public static void Clear()
        {
            lock (LockObj)
            {
                try
                {
                    if (Directory.Exists(LogDirectory))
                    {
                        var files = Directory.GetFiles(LogDirectory, "wordflow_*.log");
                        foreach (var file in files)
                        {
                            File.Delete(file);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Clear logs error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 获取最近的日志内容
        /// </summary>
        /// <param name="lines">要读取的行数</param>
        /// <returns>日志内容</returns>
        public static string GetRecentLogs(int lines = 100)
        {
            try
            {
                if (!File.Exists(LogFilePath))
                {
                    return "暂无日志记录";
                }

                lock (LockObj)
                {
                    var allLines = File.ReadAllLines(LogFilePath);
                    var recentLines = allLines.Length > lines 
                        ? allLines.Skip(allLines.Length - lines) 
                        : allLines;
                    return string.Join(Environment.NewLine, recentLines);
                }
            }
            catch (Exception ex)
            {
                return $"读取日志失败：{ex.Message}";
            }
        }

        /// <summary>
        /// 获取所有日志内容
        /// </summary>
        public static string GetAllLogs()
        {
            try
            {
                if (!File.Exists(LogFilePath))
                {
                    return "暂无日志记录";
                }

                lock (LockObj)
                {
                    return File.ReadAllText(LogFilePath);
                }
            }
            catch (Exception ex)
            {
                return $"读取日志失败：{ex.Message}";
            }
        }
    }
}
