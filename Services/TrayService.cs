using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using WordFlow.Services;

namespace WordFlow.Services
{
    /// <summary>
    /// 系统托盘服务 - 管理最小化到托盘功能
    /// </summary>
    public class TrayService : IDisposable
    {
        private TaskbarIcon? _notifyIcon;
        private readonly Window _mainWindow;
        private readonly SettingsService? _settingsService;
        private bool _isMinimizedToTray = false;

        public event EventHandler? ShowWindowRequested;
        public event EventHandler? ExitRequested;

        public TrayService(Window mainWindow, SettingsService? settingsService = null)
        {
            _mainWindow = mainWindow;
            _settingsService = settingsService;
            InitializeTray();
        }

        private void InitializeTray()
        {
            _notifyIcon = new TaskbarIcon
            {
                ToolTipText = "WordFlow - 智能语音输入\n双击显示窗口",
                Icon = GetApplicationIcon()
            };

            // 双击显示窗口
            _notifyIcon.TrayMouseDoubleClick += (s, e) => ShowMainWindow();

            // 构建右键菜单
            var contextMenu = new System.Windows.Controls.ContextMenu();

            // 显示主窗口
            var showItem = new System.Windows.Controls.MenuItem { Header = "显示主窗口" };
            showItem.Click += (s, e) => ShowMainWindow();
            contextMenu.Items.Add(showItem);

            // 分隔线
            contextMenu.Items.Add(new Separator());

            // 开机自启动
            var autoStartItem = new System.Windows.Controls.MenuItem
            {
                Header = "开机自启动",
                IsCheckable = true,
                IsChecked = AutoStartService.IsAutoStartEnabled()
            };
            autoStartItem.Click += (s, e) =>
            {
                if (autoStartItem.IsChecked)
                {
                    AutoStartService.EnableAutoStart();
                    _settingsService?.Save();
                }
                else
                {
                    AutoStartService.DisableAutoStart();
                    _settingsService?.Save();
                }
            };
            contextMenu.Items.Add(autoStartItem);

            // 启动时最小化
            var startMinimizedItem = new System.Windows.Controls.MenuItem
            {
                Header = "启动时最小化",
                IsCheckable = true,
                IsChecked = _settingsService?.Settings.StartMinimized ?? false
            };
            startMinimizedItem.Click += (s, e) =>
            {
                if (_settingsService != null)
                {
                    _settingsService.Settings.StartMinimized = startMinimizedItem.IsChecked;
                    _settingsService.Save();
                }
            };
            contextMenu.Items.Add(startMinimizedItem);

            // 分隔线
            contextMenu.Items.Add(new Separator());

            // 退出
            var exitItem = new MenuItem { Header = "退出" };
            exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenu = contextMenu;

            // 初始隐藏托盘图标
            _notifyIcon.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 获取应用程序图标
        /// </summary>
        private Icon GetApplicationIcon()
        {
            try
            {
                var iconPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "WordFlow.ico");
                
                if (System.IO.File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }
            }
            catch { }

            return SystemIcons.Application;
        }

        /// <summary>
        /// 最小化到托盘
        /// </summary>
        public void MinimizeToTray()
        {
            if (_notifyIcon == null) return;

            _mainWindow.Hide();
            _notifyIcon.Visibility = Visibility.Visible;
            _isMinimizedToTray = true;

            _notifyIcon.ShowBalloonTip("WordFlow", "程序已最小化到系统托盘，双击图标恢复", BalloonIcon.Info);
        }

        /// <summary>
        /// 从托盘恢复窗口
        /// </summary>
        public void ShowMainWindow()
        {
            if (_notifyIcon == null) return;

            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _isMinimizedToTray = false;
        }

        /// <summary>
        /// 是否已最小化到托盘
        /// </summary>
        public bool IsMinimizedToTray => _isMinimizedToTray;

        public void Dispose()
        {
            _notifyIcon?.Dispose();
        }
    }
}
