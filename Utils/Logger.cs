using System;
using System.IO;
using System.Threading;

namespace WordFlow.Utils
{
    /// <summary>
    /// 文件日志记录器
    /// </summary>
    public static class Logger
    {
        private static readonly string LogFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "debug_log.txt");
        private static readonly object LockObj = new object();

        public static void Log(string message)
        {
            lock (LockObj)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logLine = $"[{timestamp}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFilePath, logLine);
            }
        }

        public static void Clear()
        {
            lock (LockObj)
            {
                if (File.Exists(LogFilePath))
                {
                    File.Delete(LogFilePath);
                }
            }
        }
    }
}
