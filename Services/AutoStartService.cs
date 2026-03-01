using System;
using Microsoft.Win32;

namespace WordFlow.Services
{
    /// <summary>
    /// 开机自启动服务
    /// </summary>
    public static class AutoStartService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "WordFlow";

        /// <summary>
        /// 检查是否已启用开机自启动
        /// </summary>
        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                var value = key?.GetValue(AppName);
                return value != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 启用开机自启动
        /// </summary>
        public static bool EnableAutoStart()
        {
            try
            {
                var exePath = Environment.ProcessPath ?? 
                    System.Reflection.Assembly.GetExecutingAssembly().Location;

                // 如果是 .dll 文件，改为 .exe（单文件发布时可能是 .exe）
                if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    exePath = System.IO.Path.ChangeExtension(exePath, ".exe");
                }

                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                key?.SetValue(AppName, $"\"{exePath}\" --minimized");

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启用自启动失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 禁用开机自启动
        /// </summary>
        public static bool DisableAutoStart()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                key?.DeleteValue(AppName, false);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"禁用自启动失败: {ex.Message}");
                return false;
            }
        }
    }
}
