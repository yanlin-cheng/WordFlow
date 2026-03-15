using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using WordFlow.Infrastructure;
using WordFlow.Services;
using WordFlow.Utils;
using WordFlow.Views;

namespace WordFlow
{
    /// <summary>
    /// 应用入口 - 服务驱动架构
    /// 所有核心服务在此初始化，UI 层只负责显示
    /// </summary>
    public partial class App : Application
    {
        // 核心服务（独立于 UI）
        private GlobalHotkeyServiceV2? _hotkeyService;
        private SpeechRecognitionService? _speechService;
        private HistoryService? _historyService;
        private SettingsService? _settingsService;
        private TrayServiceV2? _trayService;
        private UpdateService? _updateService;

        private const string ASR_SERVICE_URL = "http://127.0.0.1:5000";

        // 状态
        private IntPtr _lastTargetWindow;
        private DateTime _recordingStartTime;

        /// <summary>
        /// 全局设置访问点
        /// </summary>
        public SettingsService? Settings => _settingsService;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 0. 初始化应用程序环境（创建必要的目录结构）
            var initializer = new AppInitializer();
            await initializer.InitializeAsync();

            // 1. 初始化设置服务（优先初始化，因为其他服务依赖它）
            _settingsService = new SettingsService();

            // 2. 初始化 UI 语言（在创建任何 UI 元素之前）
            InitializeUICulture();

            // 3. 初始化历史服务（延迟初始化，避免启动时加载 SQLite 原生库）
            // HistoryService 现在使用延迟初始化，构造函数不会抛出异常
            _historyService = new HistoryService();

            // 4. 初始化语音服务
            _speechService = new SpeechRecognitionService(ASR_SERVICE_URL);
            _speechService.StatusChanged += (s, msg) =>
            {
                EventBus.Publish(new StatusChangedEvent { Message = msg });
            };
            _speechService.RecognitionCompleted += (s, text) =>
            {
                EventBus.Publish(new RecognitionCompletedEvent
                {
                    Text = text,
                    TargetWindow = _lastTargetWindow,
                    TargetWindowTitle = GetWindowTitle(_lastTargetWindow)
                });
            };

            // 5. 初始化热键服务（完全独立）
            _hotkeyService = new GlobalHotkeyServiceV2(_settingsService.Settings.HotkeyCode);

            // 6. 初始化托盘服务（完全独立）- 使用回调方式
            _trayService = new TrayServiceV2(_settingsService);
            _trayService.OnShowMainWindowRequested = () =>
            {
                Logger.Log("App: 收到显示窗口请求");
                _trayService.ShowMainWindow();
            };
            _trayService.OnExitRequested = () =>
            {
                Logger.Log("App: 收到退出请求");
                _settingsService?.Save();
                Shutdown();
            };

            // 7. 订阅核心事件
            SubscribeToEvents();

            // 8. 创建主窗口
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;

            // 9. 传递服务引用到主窗口（用于设置界面）
            mainWindow.SetServices(_settingsService, _hotkeyService, _speechService, _historyService);

            // 10. 订阅 UI 事件
            mainWindow.Loaded += async (s, args) => await InitializeServicesAsync();
            mainWindow.Closing += OnMainWindowClosing;

            // 11. 显示或最小化
            if (ShouldStartMinimized(e))
            {
                mainWindow.Show();
                mainWindow.Hide();
                _trayService.MinimizeToTray();
            }
            else
            {
                mainWindow.Show();
            }

            // 12. 检查是否需要首次运行设置
            await CheckFirstRunSetupAsync(mainWindow);
            
            // 13. 初始化更新服务并检查更新
            InitializeUpdateService(mainWindow);
        }

        /// <summary>
        /// 初始化更新服务并检查更新
        /// </summary>
        private async void InitializeUpdateService(Window mainWindow)
        {
            try
            {
                _updateService = new UpdateService();
                _updateService.UpdateAvailable += OnUpdateAvailable;
                _updateService.UpdateCheckFailed += OnUpdateCheckFailed;
                
                // 启动时自动检查更新
                await _updateService.CheckForUpdateAsync(false);
            }
            catch (Exception ex)
            {
                Logger.Log($"初始化更新服务失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 发现新版本时显示更新对话框
        /// </summary>
        private async void OnUpdateAvailable(object? sender, UpdateInfo updateInfo)
        {
            try
            {
                Logger.Log($"显示更新对话框：v{updateInfo.Version}");
                
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    var dialog = new UpdateDialog(updateInfo, _updateService!);
                    dialog.Owner = MainWindow;
                    dialog.ShowDialog();
                });
            }
            catch (Exception ex)
            {
                Logger.Log($"显示更新对话框失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 更新检查失败处理
        /// </summary>
        private void OnUpdateCheckFailed(object? sender, string message)
        {
            // 静默处理，不干扰用户
            Logger.Log($"更新检查失败：{message}");
        }

        /// <summary>
        /// 订阅事件总线事件
        /// </summary>
        private void SubscribeToEvents()
        {
            Logger.Log("App: 开始订阅事件总线事件");

            // 录音开始 -> 调用语音服务
            EventBus.Subscribe<RecordingStartedEvent>(evt =>
            {
                Logger.Log($"App: 收到 RecordingStartedEvent，目标窗口={evt.TargetWindow}");
                _lastTargetWindow = evt.TargetWindow;
                _recordingStartTime = DateTime.Now;
                Logger.Log("App: 准备调用 StartRecording");
                _speechService?.StartRecording();
                Logger.Log("App: StartRecording 调用完成");
            });

            // 录音结束 -> 停止录音
            EventBus.Subscribe<RecordingStoppedEvent>(evt =>
            {
                Logger.Log("App: 收到 RecordingStoppedEvent");
                _speechService?.StopRecordingAndRecognizeAsync();
                Logger.Log("App: StopRecordingAndRecognizeAsync 调用完成");
            });

            // 注意：ShowMainWindowRequest 和 ExitApplicationRequest 已改为回调方式
            // 在 TrayServiceV2 初始化时设置

            Logger.Log("App: 事件总线事件订阅完成");
        }

        /// <summary>
        /// 初始化服务连接
        /// </summary>
        private async System.Threading.Tasks.Task InitializeServicesAsync()
        {
            if (_speechService == null) return;

            // 1. 首先尝试连接服务
            var (connected, models, currentModel) = await _speechService.InitializeAsync();
            if (connected)
            {
                EventBus.Publish(new StatusChangedEvent
                {
                    Message = string.IsNullOrEmpty(currentModel) ? "服务已连接" : $"模型：{currentModel}"
                });

                if (string.IsNullOrEmpty(currentModel))
                {
                    await _speechService.AutoLoadModelAsync();
                }
            }
            else
            {
                // 2. 连接失败，尝试自动启动 Python 服务
                EventBus.Publish(new StatusChangedEvent
                {
                    Message = "未连接到 ASR 服务，正在尝试自动启动..."
                });

                bool serverStarted = await _speechService.TryStartServerAsync();
                if (serverStarted)
                {
                    // 3. 服务启动成功，重新初始化
                    await Task.Delay(1000);  // 等待服务完全启动
                    (connected, models, currentModel) = await _speechService.InitializeAsync();
                    if (connected)
                    {
                        EventBus.Publish(new StatusChangedEvent
                        {
                            Message = string.IsNullOrEmpty(currentModel) ? "服务已连接" : $"模型：{currentModel}"
                        });

                        if (string.IsNullOrEmpty(currentModel))
                        {
                            await _speechService.AutoLoadModelAsync();
                        }
                    }
                }
                else
                {
                    EventBus.Publish(new StatusChangedEvent
                    {
                        Message = "未连接到 ASR 服务，请手动启动 Python 服务"
                    });
                }
            }

            // 4. 启动自动学习定时器（每 24 小时执行一次自动学习）
            StartAutoLearnTimer();
        }

        /// <summary>
        /// 自动学习定时器
        /// </summary>
        private System.Threading.Timer? _autoLearnTimer;

        /// <summary>
        /// 启动自动学习定时器
        /// </summary>
        private void StartAutoLearnTimer()
        {
            try
            {
                // 24 小时 = 86400000 毫秒
                var dueTime = TimeSpan.FromMinutes(1); // 1 分钟后首次执行
                var period = TimeSpan.FromHours(24);   // 之后每 24 小时执行

                _autoLearnTimer = new System.Threading.Timer(
                    async _ => await RunAutoLearnAsync(),
                    null,
                    dueTime,
                    period);

                Logger.Log("自动学习定时器已启动");
            }
            catch (Exception ex)
            {
                Logger.Log($"启动自动学习定时器失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 执行自动学习
        /// </summary>
        private async System.Threading.Tasks.Task RunAutoLearnAsync()
        {
            try
            {
                Logger.Log("开始自动学习...");

                if (_historyService == null)
                {
                    _historyService = new HistoryService();
                }

                var learningEngine = new VocabularyLearningEngine(_historyService);
                var result = await learningEngine.AutoLearnAsync(500);

                if (result.Success && result.TotalLearned > 0)
                {
                    Logger.Log($"自动学习完成：新增 {result.TotalLearned} 个词汇");

                    // 在 UI 线程显示通知
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        EventBus.Publish(new StatusChangedEvent
                        {
                            Message = $"自动学习完成：新增 {result.TotalLearned} 个词汇，请重启 ASR 服务以加载新热词"
                        });
                    });
                }
                else if (result.Success)
                {
                    Logger.Log("自动学习完成：没有新词汇可学习");
                }
                else
                {
                    Logger.Log($"自动学习失败：{result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"自动学习失败：{ex}");
            }
        }

        /// <summary>
        /// 窗口关闭处理 - 只有两个选项：最小化托盘 或 退出
        /// </summary>
        private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 检查用户是否已记住选择
            if (_settingsService != null)
            {
                if (_settingsService.Settings.CloseAction == 0)
                {
                    // 用户选择了"最小化到托盘"并记住
                    e.Cancel = true;
                    _trayService?.MinimizeToTray();
                    return;
                }
                else if (_settingsService.Settings.CloseAction == 1)
                {
                    // 用户选择了"退出程序"并记住
                    _settingsService?.Save();
                    return;
                }
            }

            // 用户未记住选择，显示确认对话框（只有两个选项，没有取消）
            var dialog = new MessageBoxImageAndTextWindow(
                "您想如何处理 WordFlow？\n\n" +
                "• 最小化到托盘：程序继续在后台运行，可按热键语音输入\n" +
                "• 退出：完全关闭程序\n\n" +
                "您可以在设置中更改此行为。",
                "关闭 WordFlow",
                new[] { "最小化到托盘", "退出" });
            
            var result = dialog.ShowDialog();

            if (result == 0)
            {
                // 最小化到托盘
                e.Cancel = true;
                _trayService?.MinimizeToTray();
            }
            else if (result == 1)
            {
                // 退出程序
                _settingsService?.Save();
            }
            // 不再处理 result == 2（取消按钮已移除）
        }

        /// <summary>
        /// 判断是否最小化启动
        /// </summary>
        private bool ShouldStartMinimized(StartupEventArgs e)
        {
            return e.Args.Contains("--minimized") ||
                   e.Args.Contains("-minimized") ||
                   (_settingsService?.Settings.StartMinimized ?? false);
        }

        /// <summary>
        /// 获取窗口标题
        /// </summary>
        private string GetWindowTitle(IntPtr hWnd)
        {
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            return sb.ToString();
        }

        /// <summary>
        /// 检查是否需要首次运行设置向导
        /// </summary>
        private async Task CheckFirstRunSetupAsync(Window mainWindow)
        {
            try
            {
                // 使用 ModelDownloadService 检查是否需要首次设置
                var downloadService = new ModelDownloadService();
                bool needsSetup = await downloadService.NeedsFirstRunSetupAsync();

                if (needsSetup)
                {
                    Logger.Log("首次运行：需要设置向导");
                    
                    // 显示首次运行向导
                    var wizard = new FirstRunWizard
                    {
                        Owner = mainWindow
                    };
                    
                    var result = wizard.ShowDialog();
                    
                    if (result == true)
                    {
                        Logger.Log("首次运行向导：已完成");
                        // 用户完成了设置，可以开始使用
                        EventBus.Publish(new StatusChangedEvent
                        {
                            Message = "欢迎使用 WordFlow！"
                        });
                    }
                    else
                    {
                        Logger.Log("首次运行向导：用户取消或跳过");
                        EventBus.Publish(new StatusChangedEvent
                        {
                            Message = "欢迎使用 WordFlow！请在设置中下载模型"
                        });
                    }
                }
                else
                {
                    Logger.Log("非首次运行：模型已安装");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"检查首次运行设置失败：{ex.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _hotkeyService?.Dispose();
            _trayService?.Dispose();
            _speechService?.Dispose();
            base.OnExit(e);
        }

        /// <summary>
        /// 初始化 UI 语言
        /// 在应用启动时调用，根据设置中的语言代码初始化界面语言
        /// </summary>
        private void InitializeUICulture()
        {
            try
            {
                // 确保设置服务已加载
                _settingsService?.Load();
                
                // 获取语言代码的优先级：
                // 1. 优先使用 InstallerLanguageCode（安装程序选择的语言）
                // 2. 如果 InstallerLanguageCode 为空，则使用 LanguageCode
                // 3. 最后回退到 zh-CN
                string languageCode = "zh-CN";  // 默认值
                
                if (_settingsService != null)
                {
                    var settings = _settingsService.Settings;
                    Logger.Log($"Settings 加载状态：LanguageCode=[{settings.LanguageCode}], InstallerLanguageCode=[{settings.InstallerLanguageCode}]");
                    
                    // 优先使用 InstallerLanguageCode（如果存在）
                    if (!string.IsNullOrEmpty(settings.InstallerLanguageCode))
                    {
                        languageCode = settings.InstallerLanguageCode;
                        Logger.Log($"使用 InstallerLanguageCode: {languageCode}");
                    }
                    // 否则使用 LanguageCode
                    else if (!string.IsNullOrEmpty(settings.LanguageCode))
                    {
                        languageCode = settings.LanguageCode;
                        Logger.Log($"使用 LanguageCode: {languageCode}");
                    }
                    else
                    {
                        Logger.Log("InstallerLanguageCode 和 LanguageCode 都为空，使用默认：zh-CN");
                    }
                }
                else
                {
                    Logger.Log("SettingsService 为空，使用默认语言：zh-CN");
                }

                Logger.Log($"初始化 UI 语言：{languageCode}");

                // 初始化 LocalizationService
                LocalizationService.Instance.Initialize(languageCode);

                // 解析语言代码
                var cultureInfo = new CultureInfo(languageCode);

                // 设置当前线程的文化特性
                System.Threading.Thread.CurrentThread.CurrentCulture = cultureInfo;
                System.Threading.Thread.CurrentThread.CurrentUICulture = cultureInfo;

                // 设置 WPF 的默认语言
                FrameworkElement.LanguageProperty.OverrideMetadata(
                    typeof(FrameworkElement),
                    new FrameworkPropertyMetadata(
                        XmlLanguage.GetLanguage(cultureInfo.IetfLanguageTag)));
                        
                Logger.Log($"UI 语言初始化完成：{languageCode}");
            }
            catch (Exception ex)
            {
                Logger.Log($"初始化 UI 语言失败：{ex.Message}");
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
    }
}
