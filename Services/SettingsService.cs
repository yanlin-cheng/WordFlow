using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using WordFlow.Utils;

namespace WordFlow.Services
{
    /// <summary>
    /// 应用设置服务
    /// </summary>
    public class SettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WordFlow", "settings.json");

        private AppSettings _settings = new();

        public AppSettings Settings => _settings;

        public SettingsService()
        {
            Load();
        }

        public void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    Logger.Log($"设置已加载：热键={GetKeyName(_settings.HotkeyCode)}({_settings.HotkeyCode}), 开机启动={_settings.AutoStart}, 最小化启动={_settings.StartMinimized}");
                }
                else
                {
                    Logger.Log("设置文件不存在，使用默认设置");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"加载设置失败: {ex.Message}");
                _settings = new AppSettings();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
                Logger.Log("设置已保存");
            }
            catch (Exception ex)
            {
                Logger.Log($"保存设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 热键键码转换为友好名称
        /// </summary>
        public static string GetKeyName(int vkCode)
        {
            return vkCode switch
            {
                0xC0 => "` 键",  // ESC 下面、数字 1 左边的波浪线键
                0xA5 => "右 Alt",  // 右 Alt（菜单键）
                0xA2 => "左 Ctrl",
                0xA3 => "右 Ctrl",
                0x10 => "Shift",
                0xA0 => "左 Shift",
                0xA1 => "右 Shift",
                0x12 => "Alt",
                0xA4 => "左 Alt",
                0x5B => "左 Win",
                0x5C => "右 Win",
                _ => $"键码 {vkCode}"
            };
        }

        /// <summary>
        /// 获取所有支持的热键选项
        /// </summary>
        public static List<HotkeyOption> GetAvailableHotkeys()
        {
            return new List<HotkeyOption>
            {
                new() { Code = 0xC0, Name = "` 键", Description = "ESC 下面、数字 1 左边的波浪线键（推荐）" },
                new() { Code = 0xA5, Name = "右 Alt", Description = "右 Alt（菜单键）" },
            };
        }
    }

    /// <summary>
    /// 应用设置
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// 录音热键键码（默认 CapsLock = 0x14）
        /// </summary>
        public int HotkeyCode { get; set; } = 0xC0;  // ESC 下面的 `~ 键

        /// <summary>
        /// 是否开机自启动
        /// </summary>
        public bool AutoStart { get; set; }

        /// <summary>
        /// 是否最小化到托盘
        /// </summary>
        public bool MinimizeToTray { get; set; } = true;

        /// <summary>
        /// 是否启动时最小化
        /// </summary>
        public bool StartMinimized { get; set; }

        /// <summary>
        /// 关闭按钮行为：0=最小化到托盘，1=退出程序，2=每次询问（默认）
        /// </summary>
        public int CloseAction { get; set; } = 2;

        /// <summary>
        /// 界面语言代码（默认 zh-CN）
        /// </summary>
        public string LanguageCode { get; set; } = "zh-CN";
    }

    /// <summary>
    /// 热键选项
    /// </summary>
    public class HotkeyOption
    {
        public int Code { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
