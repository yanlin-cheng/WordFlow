using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using WordFlow.Infrastructure;

namespace WordFlow.Services
{
    /// <summary>
    /// 托盘服务 V2 - 使用 WinForms NotifyIcon（更稳定）
    /// 完全独立于 WPF 窗口生命周期
    /// 使用回调而非 EventBus 订阅，避免重复订阅问题
    /// </summary>
    public class TrayServiceV2 : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private ContextMenuStrip? _contextMenu;
        private SettingsService _settingsService;
        private bool _isDisposed = false;

        // 回调委托
        public Action? OnShowMainWindowRequested;
        public Action? OnExitRequested;

        public TrayServiceV2(SettingsService settingsService)
        {
            _settingsService = settingsService;
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            Icon? trayIcon = null;
            string loadMethod = "unknown";
            
            // 方法 1：优先从当前 EXE 提取关联图标（确保与 EXE 图标一致）
            try
            {
                string exePath = Assembly.GetEntryAssembly()?.Location ?? 
                                 System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? 
                                 string.Empty;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    trayIcon = Icon.ExtractAssociatedIcon(exePath);
                    if (trayIcon != null)
                    {
                        loadMethod = "EXE associated icon";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"方法 1 失败 (EXE associated icon): {ex.Message}");
            }
            
            // 方法 2：如果方法 1 失败，尝试从 Resources 目录加载
            if (trayIcon == null)
            {
                try
                {
                    string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "icon.ico");
                    System.Diagnostics.Debug.WriteLine($"Trying to load icon from: {iconPath}");
                    
                    if (File.Exists(iconPath))
                    {
                        trayIcon = new Icon(iconPath);
                        loadMethod = "Resources/icon.ico file";
                        System.Diagnostics.Debug.WriteLine("Icon loaded from Resources folder");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Icon file not found at: {iconPath}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"方法 2 失败 (Resources/icon.ico): {ex.Message}");
                }
            }
            
            // 方法 3：如果以上都失败，使用系统图标作为后备
            if (trayIcon == null)
            {
                trayIcon = SystemIcons.Application;
                loadMethod = "System default icon";
                System.Diagnostics.Debug.WriteLine("Using system default icon");
            }
            
            System.Diagnostics.Debug.WriteLine($"Tray icon loaded using: {loadMethod}");

            _notifyIcon = new NotifyIcon
            {
                Text = "WordFlow - 智能语音输入\n双击显示窗口",
                Icon = trayIcon,
                Visible = false // 初始不可见，等窗口最小化后才显示
            };

            // 双击显示窗口 - 通过回调
            _notifyIcon.DoubleClick += (s, e) =>
            {
                OnShowMainWindowRequested?.Invoke();
            };

            // 右键菜单 (使用 ContextMenuStrip) - 保存引用以便释放
            _contextMenu = new ContextMenuStrip();

            // 显示主窗口 - 通过回调
            var showItem = new ToolStripMenuItem("显示主窗口", null, (s, e) =>
            {
                OnShowMainWindowRequested?.Invoke();
            });
            _contextMenu.Items.Add(showItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // 开机自启动
            var autoStartItem = new ToolStripMenuItem("开机自启动", null, (s, e) =>
            {
                var item = (ToolStripMenuItem)s;
                item.Checked = !item.Checked;
                if (item.Checked)
                {
                    AutoStartService.EnableAutoStart();
                }
                else
                {
                    AutoStartService.DisableAutoStart();
                }
            })
            {
                Checked = AutoStartService.IsAutoStartEnabled()
            };
            _contextMenu.Items.Add(autoStartItem);

            // 启动时最小化
            var startMinimizedItem = new ToolStripMenuItem("启动时最小化", null, (s, e) =>
            {
                var item = (ToolStripMenuItem)s;
                item.Checked = !item.Checked;
                _settingsService.Settings.StartMinimized = item.Checked;
                _settingsService.Save();
            })
            {
                Checked = _settingsService.Settings.StartMinimized
            };
            _contextMenu.Items.Add(startMinimizedItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // 退出 - 通过回调
            var exitItem = new ToolStripMenuItem("退出", null, (s, e) =>
            {
                OnExitRequested?.Invoke();
            });
            _contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = _contextMenu;
        }

        /// <summary>
        /// 最小化到托盘
        /// </summary>
        public void MinimizeToTray()
        {
            if (_isDisposed || _notifyIcon == null) return;

            System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (System.Windows.Application.Current.MainWindow != null)
                {
                    System.Windows.Application.Current.MainWindow.Hide();
                }
                // 确保只显示一个托盘图标
                _notifyIcon.Visible = true;
                // 移除气球通知，避免打扰用户
                // _notifyIcon.ShowBalloonTip(2000, "WordFlow", "程序已最小化到系统托盘，双击图标恢复", ToolTipIcon.Info);
            });
        }

        /// <summary>
        /// 从托盘恢复窗口
        /// </summary>
        public void ShowMainWindow()
        {
            if (_isDisposed || _notifyIcon == null) return;

            System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (System.Windows.Application.Current.MainWindow != null)
                {
                    System.Windows.Application.Current.MainWindow.Show();
                    System.Windows.Application.Current.MainWindow.WindowState = WindowState.Normal;
                    System.Windows.Application.Current.MainWindow.Activate();
                }
                // 窗口显示后隐藏托盘图标，避免重复显示
                _notifyIcon.Visible = false;
            });
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _notifyIcon?.Dispose();
                _contextMenu?.Dispose();
            }
        }
    }
}
