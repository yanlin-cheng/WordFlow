using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsInput;
using WindowsInput.Native;

using WordFlow.Infrastructure;
using WordFlow.Services;
using WordFlow.Utils;
using WordFlow.Models;
using WordFlow.Views;
using WordFlow.Resources.Strings;
using CorrectionSuggestion = WordFlow.Services.SpeechRecognitionService.CorrectionSuggestion;

namespace WordFlow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        // 服务引用（由 App.xaml.cs 注入）
        private SpeechRecognitionService? _speechService;
        private GlobalHotkeyServiceV2? _hotkeyService;
        private HistoryService? _historyService;
        private SettingsService? _settingsService;

        // UI 相关
        private Views.TranscriptPopupWindow? _transcriptPopup;
        private DispatcherTimer? _recordingTimer;
        private int _recordingSeconds;
        private const string ASR_SERVICE_URL = "http://127.0.0.1:5000";
        private List<SpeechRecognitionService.ModelInfo> _availableModels = [];
        private IntPtr _lastForegroundWindow;
        private DateTime _recordingStartTime;
        
        // 统计相关
        private int _todayCharCount = 0;
        private int _totalCharCount = 0;
        private int _totalSavedSeconds = 0;
        private int _correctCount = 0;
        private int _totalCount = 0;
        
        // 错误检测相关字段
        private string _lastRecognizedText = "";
        private DateTime _lastRecognitionTime;

        #region Windows API
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        private const int SW_SHOWNOACTIVATE = 4;
        private const int SW_SHOW = 5;
        private const int SW_MINIMIZE = 6;
        private const int SW_SHOWMINNOACTIVE = 7;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        private const uint MOUSEEVENTF_LEFTUP = 0x04;
        #endregion

        /// <summary>
        /// 获取窗口标题
        /// </summary>
        private string GetWindowTitle(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "";
            
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public MainWindow()
        {
            InitializeComponent();
            
            // 初始化历史记录服务
            try
            {
                _historyService = new HistoryService();
                Logger.Log("历史记录服务已初始化");
            }
            catch (Exception ex)
            {
                Logger.Log($"历史记录服务初始化失败: {ex.Message}");
            }
            
            // 注册快捷键
            Loaded += OnWindowLoaded;
            Closing += OnWindowClosing;
            
            // 编辑器已移除 - 文字将直接输入到目标窗口

            // 订阅事件总线（新架构）
            SubscribeToEventBus();
        }

        /// <summary>
        /// 设置服务引用（由 App.xaml.cs 调用）
        /// </summary>
        public void SetServices(SettingsService settings, GlobalHotkeyServiceV2 hotkey,
                               SpeechRecognitionService speech, HistoryService? history)
        {
            _settingsService = settings;
            _hotkeyService = hotkey;
            _speechService = speech;
            _historyService = history;

            // 更新热键显示
            UpdateHotkeyDisplay();
            
            // 加载统计数据
            LoadStatisticsAsync();
        }
        
        /// <summary>
        /// 异步加载统计数据
        /// </summary>
        private async void LoadStatisticsAsync()
        {
            if (_historyService == null) return;
            
            try
            {
                // 获取今日输入字数
                var today = DateTime.Now.Date;
                var todayHistory = await _historyService.GetHistoryByDateRangeAsync(today, DateTime.Now);
                _todayCharCount = todayHistory.Sum(h => h.OriginalText?.Length ?? 0);
                
                // 获取总输入字数
                var allHistory = await _historyService.GetAllHistoryAsync();
                _totalCharCount = allHistory.Sum(h => h.OriginalText?.Length ?? 0);
                
                // 估算节省时间（假设每分钟输入 100 字）
                _totalSavedSeconds = _totalCharCount / 100 * 60;
                
                // 更新 UI
                UpdateStatisticsUI();
            }
            catch (Exception ex)
            {
                Logger.Log($"加载统计数据失败：{ex.Message}");
            }
        }
        
        /// <summary>
        /// 更新统计数据显示
        /// </summary>
        private void UpdateStatisticsUI()
        {
            TodayCountText.Text = _todayCharCount.ToString();
            TotalCountText.Text = _totalCharCount.ToString();
            
            // 格式化时间显示
            if (_totalSavedSeconds < 60)
            {
                SavedTimeText.Text = $"{_totalSavedSeconds}秒";
            }
            else if (_totalSavedSeconds < 3600)
            {
                SavedTimeText.Text = $"{_totalSavedSeconds / 60}分钟";
            }
            else
            {
                var hours = _totalSavedSeconds / 3600;
                var mins = (_totalSavedSeconds % 3600) / 60;
                SavedTimeText.Text = $"{hours}小时{mins}分钟";
            }
        }
        
        /// <summary>
        /// 更新统计数据（识别完成后调用）
        /// 节省时间计算：
        /// - 普通打字速度：约 40 字/分钟（中文）
        /// - 语音输入速度：约 200 字/分钟（正常语速）
        /// - 节省时间 = 语音输入字数 / 40 - 语音输入字数 / 200
        ///             = 语音输入字数 × (1/40 - 1/200)
        ///             = 语音输入字数 × 0.02
        ///             = 语音输入字数 × 1.2 秒/字
        /// </summary>
        private void UpdateStatistics(string text)
        {
            var charCount = text.Length;
            _todayCharCount += charCount;
            _totalCharCount += charCount;
            
            // 节省时间计算：
            /// 普通打字：40 字/分钟 = 0.67 字/秒
            /// 语音输入：200 字/分钟 = 3.33 字/秒
            /// 输入 charCount 字，普通打字需要 charCount/40 分钟
            /// 输入 charCount 字，语音输入需要 charCount/200 分钟
            /// 节省时间 = charCount/40 - charCount/200 = charCount × 0.02 分钟 = charCount × 1.2 秒
            var savedSeconds = (int)(charCount * 1.2); // 每字节省约 1.2 秒
            _totalSavedSeconds += savedSeconds;
            
            _totalCount++;
            _correctCount++; // 默认算作正确，用户修正时会减少
            
            Logger.Log($"识别 {charCount} 字，节省 {savedSeconds} 秒");
            
            UpdateStatisticsUI();
        }

        /// <summary>
        /// 订阅事件总线事件（仅 UI 更新）
        /// 注意：录音控制事件（RecordingStartedEvent/RecordingStoppedEvent）由 App 层处理
        /// </summary>
        private void SubscribeToEventBus()
        {
            // 状态更新
            EventBus.Subscribe<StatusChangedEvent>(evt =>
            {
                // 使用 Application.Dispatcher 确保窗口隐藏时也能工作
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    StatusText.Text = evt.Message;
                });
            });

            // 录音开始 - 仅 UI 更新（不控制业务逻辑）
            EventBus.Subscribe<RecordingStartedEvent>(evt =>
            {
                _lastForegroundWindow = evt.TargetWindow;
                _recordingStartTime = DateTime.Now;

                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    StatusText.Text = "正在录音...";
                    RecordButtonIndicator.Fill = Brushes.Red;
                    StartRecordingTimer();
                });
            });

            // 录音结束 - 仅 UI 更新
            EventBus.Subscribe<RecordingStoppedEvent>(evt =>
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    RecordButtonIndicator.Fill = Brushes.Gray;
                    StopRecordingTimer();
                });
            });

            // 识别完成 - 完整处理（文字发送 + 历史保存 + UI 更新）
            EventBus.Subscribe<RecognitionCompletedEvent>(async evt =>
            {
                var targetWindow = evt.TargetWindow;
                var targetWindowTitle = evt.TargetWindowTitle;

                await Application.Current.Dispatcher.BeginInvoke(async () =>
                {
                    if (string.IsNullOrEmpty(evt.Text))
                    {
                        StatusText.Text = "识别完成（无内容）";
                        RecordButtonIndicator.Fill = Brushes.Gray;
                        return;
                    }

                    // 【智能后处理】应用文本优化规则
                    var processedText = TextPostProcessor.Process(evt.Text);
                    Logger.Log($"原文本：{evt.Text}");
                    Logger.Log($"处理后：{processedText}");

                    // 保存目标窗口句柄
                    var myHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    
                    // 检查目标窗口是否有效
                    if (targetWindow != IntPtr.Zero && !IsWindow(targetWindow))
                    {
                        Logger.Log("目标窗口已关闭，文字将只输入到本程序");
                        targetWindow = IntPtr.Zero;
                    }

                    // 发送文字到外部窗口
                    if (targetWindow != IntPtr.Zero && targetWindow != myHandle)
                    {
                        Logger.Log($"准备发送文字到：{targetWindowTitle}");
                        await SendTextToExternalWindowAsync(targetWindow, processedText);
                    }
                    else if (targetWindow == myHandle)
                    {
                        Logger.Log("目标窗口是 WordFlow 自身，文字已输入到编辑器");
                    }

                    // 保存输入历史
                    await SaveInputHistoryAsync(processedText, targetWindowTitle);

                    // 保存识别结果用于错误检测
                    _lastRecognizedText = processedText;
                    _lastRecognitionTime = DateTime.Now;

                    // 显示底部提示框
                    ShowTranscriptPopup(processedText);
                    StatusText.Text = "识别完成";
                    RecordButtonIndicator.Fill = Brushes.Gray;
                });
            });

            // 模型缺失通知（后台检测，仅状态栏提示，不阻塞用户）
            EventBus.Subscribe<ModelNeededEvent>(evt =>
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    // 在状态栏显示提示，不阻塞用户
                    StatusText.Text = evt.Message + " - 请在设置中下载模型";
                    ModelStatusText.Text = "模型：未安装";
                    ModelStatusText.Foreground = Brushes.Orange;
                });
            });

            // 设置变更通知
            EventBus.Subscribe<SettingsChangedEvent>(evt =>
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    if (evt.ChangedProperty == "HotkeyCode")
                    {
                        UpdateHotkeyDisplay();
                    }
                });
            });
        }

        /// <summary>
        /// 更新热键显示
        /// </summary>
        private void UpdateHotkeyDisplay()
        {
            if (_settingsService != null)
            {
                var keyName = SettingsService.GetKeyName(_settingsService.Settings.HotkeyCode);
                var keyDescription = _settingsService.Settings.HotkeyCode == 0xC0 ? Strings.MW_HotkeyKeyDescription : "";
                
                // 使用资源字符串构建提示文本
                var displayKeyName = string.Format(Strings.MW_PressToRecordWithKey, keyName + keyDescription);
                var hotkeyHint = string.Format(Strings.MW_HotkeyHintWithKey, keyName + keyDescription);
                
                RecordButtonText.Text = displayKeyName;
                HotkeyHintText.Text = hotkeyHint;
            }
        }

        private async void OnWindowLoaded(object? sender, RoutedEventArgs e)
        {
            Focus();
            
            // 更新热键显示
            UpdateHotkeyDisplay();
            
            // 更新初始状态
            StatusText.Text = "正在连接服务...";
            
            // 自动连接并启动服务
            await InitializeServiceWithAutoConnectAsync();
        }

        private async Task InitializeServiceWithAutoConnectAsync()
        {
            // 先尝试初始化
            var connected = await InitializeServiceAsync();
            
            // 如果未连接，自动尝试启动服务
            if (!connected && _speechService != null)
            {
                Logger.Log("初始化未连接，尝试自动启动服务...");
                StatusText.Text = "尝试自动启动服务...";
                
                var started = await _speechService.TryStartServerAsync();
                if (started)
                {
                    // 服务启动成功，重新初始化
                    Logger.Log("服务已启动，重新连接...");
                    await Task.Delay(1000); // 等待服务完全启动
                    connected = await InitializeServiceAsync();
                }
            }
            
            if (connected)
            {
                StatusText.Text = "服务已连接，可以开始录音";
            }
        }





        private async Task<bool> InitializeServiceAsync()
        {
            try
            {
                _speechService ??= new SpeechRecognitionService(ASR_SERVICE_URL);
                
                // 注册事件（避免重复注册）
                _speechService.StatusChanged -= OnStatusChanged;
                _speechService.RecognitionCompleted -= OnRecognitionCompleted;
                _speechService.RecordingStateChanged -= OnRecordingStateChanged;
                _speechService.ProcessingStateChanged -= OnProcessingStateChanged;
                
                _speechService.StatusChanged += OnStatusChanged;
                _speechService.RecognitionCompleted += OnRecognitionCompleted;
                _speechService.RecordingStateChanged += OnRecordingStateChanged;
                _speechService.ProcessingStateChanged += OnProcessingStateChanged;

                StatusText.Text = "正在连接 ASR 服务...";
                
                // 初始化并获取模型信息
                var (connected, models, currentModel) = await _speechService.InitializeAsync();
                
                if (connected)
                {
                    _availableModels = models;
                    
                    // 如果没有加载模型，尝试自动加载第一个已安装的
                    if (string.IsNullOrEmpty(currentModel))
                    {
                        Logger.Log("未检测到已加载模型，尝试自动加载...");
                        var loaded = await _speechService.AutoLoadModelAsync();
                        if (loaded)
                        {
                            currentModel = _speechService.CurrentModel;
                            Logger.Log($"自动加载模型成功：{currentModel}");
                        }
                    }
                    
                    UpdateModelStatus(true, currentModel);
                    
                    // 根据模型状态更新状态栏
                    if (string.IsNullOrEmpty(currentModel))
                    {
                        if (!models.Any(m => m.Installed))
                        {
                            StatusText.Text = "未安装模型，请在设置中下载模型";
                            ModelStatusText.Text = "模型：未安装";
                            ModelStatusText.Foreground = Brushes.Orange;
                        }
                        else
                        {
                            StatusText.Text = "已安装模型，请在设置中选择要使用的模型";
                            ModelStatusText.Text = "模型：未加载";
                            ModelStatusText.Foreground = Brushes.Orange;
                        }
                    }
                    else
                    {
                        StatusText.Text = $"服务已连接 - {currentModel}";
                    }
                    
                    return true;
                }
                else
                {
                    StatusText.Text = "未连接到 ASR 服务，点击'连接服务'按钮手动连接";
                    UpdateModelStatus(false, "");
                    return false;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"初始化失败：{ex.Message}";
                UpdateModelStatus(false, "");
                return false;
            }
        }

        #region 错误检测与学习

        #endregion

        #region 事件处理

        private void OnStatusChanged(object? sender, string message)
        {
            Dispatcher.Invoke(() => StatusText.Text = message);
        }

        private void OnRecognitionCompleted(object? sender, string text)
        {
            // 注意：识别完成逻辑已移至事件总线订阅（SubscribeToEventBus 中的 RecognitionCompletedEvent）
            // 此方法保留用于向后兼容，但不再执行实际操作
            Logger.Log($"OnRecognitionCompleted: 识别结果={text}（已由事件总线处理）");
        }

        /// <summary>
        /// 发送文字到外部窗口（使用 InputSimulator - 根据过往经验最终成功的方案 13）
        /// </summary>
        private async Task SendTextToExternalWindowAsync(IntPtr targetWindow, string text)
        {
            Logger.Log("=== 开始输入到外部窗口 ===");
            Logger.Log($"目标窗口句柄：{targetWindow}");
            Logger.Log($"目标窗口标题：{GetWindowTitle(targetWindow)}");
            Logger.Log($"文本长度：{text.Length} 字符");
            Logger.Log($"文本内容：{text.Substring(0, Math.Min(30, text.Length))}...");
            
            if (targetWindow == IntPtr.Zero)
            {
                Logger.Log("✗ 目标窗口句柄为空");
                return;
            }

            // 检查窗口是否有效
            if (!IsWindow(targetWindow))
            {
                Logger.Log("✗ 目标窗口已失效");
                return;
            }

            // 1. 激活目标窗口
            Logger.Log("正在激活目标窗口...");
            BringWindowToForeground(targetWindow);
            await Task.Delay(100);

            // 2. 保存原始剪贴板内容，输入完成后恢复（避免污染用户剪贴板）
            string? originalClipboardContent = null;
            bool hadClipboardContent = false;
            
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    originalClipboardContent = System.Windows.Clipboard.GetText();
                    hadClipboardContent = true;
                    Logger.Log($"步骤 0: 已保存原始剪贴板内容 ({originalClipboardContent.Length} 字符)");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"步骤 0: 保存剪贴板内容失败（可能为空或无法访问）: {ex.Message}");
            }

            // 3. 使用 InputSimulator 发送 Ctrl+V 粘贴
            try
            {
                Logger.Log("步骤 1: 设置剪贴板内容...");
                System.Windows.Clipboard.Clear();
                System.Windows.Clipboard.SetText(text + " ");
                await Task.Delay(50);
                
                Logger.Log("步骤 2: 使用 InputSimulator 发送 Ctrl+V...");
                var simulator = new InputSimulator();
                simulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_V);
                await Task.Delay(100);
                
                Logger.Log("✓ 输入完成");
                
                // 4. 恢复原始剪贴板内容
                if (hadClipboardContent && originalClipboardContent != null)
                {
                    System.Windows.Clipboard.SetText(originalClipboardContent);
                    Logger.Log("✓ 剪贴板已恢复到原始内容");
                }
                else
                {
                    System.Windows.Clipboard.Clear();
                    Logger.Log("✓ 剪贴板已清空（原为空）");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"输入失败：{ex.Message}");
                
                // 即使输入失败，也尝试恢复剪贴板
                try
                {
                    if (hadClipboardContent && originalClipboardContent != null)
                    {
                        System.Windows.Clipboard.SetText(originalClipboardContent);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 推断输入场景
        /// </summary>
        private InputScene InferInputScene(string? appName, string text)
        {
            // 根据应用推断
            if (appName?.Contains("Word") == true || appName?.Contains("WPS") == true)
            {
                // 根据内容推断
                if (text.Contains("法院") || text.Contains("诉讼") || text.Contains("合同"))
                    return InputScene.Legal;
                if (text.Contains("医院") || text.Contains("病历") || text.Contains("诊断"))
                    return InputScene.Medical;
                if (text.Contains("公司") || text.Contains("项目") || text.Contains("客户"))
                    return InputScene.Business;
            }
            
            if (appName?.Contains("Visual Studio") == true || appName?.Contains("VS Code") == true)
            {
                return InputScene.Programming;
            }
            
            if (appName?.Contains("WeChat") == true || appName?.Contains("QQ") == true)
            {
                return InputScene.Chat;
            }
            
            return InputScene.General;
        }

        /// <summary>
        /// 保存输入历史记录
        /// </summary>
        private async Task SaveInputHistoryAsync(string text, string? targetWindow)
        {
            if (_historyService == null) return;
            
            try
            {
                var history = new InputHistory
                {
                    OriginalText = text,
                    CorrectedText = text,
                    Timestamp = DateTime.Now,
                    TargetApplication = targetWindow
                };
                
                await _historyService.SaveInputHistoryAsync(history);
                Logger.Log($"已保存输入历史：{text.Length} 字");
            }
            catch (Exception ex)
            {
                Logger.Log($"保存输入历史失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 发送单个字符（使用 SendInput）
        /// </summary>
        private void SendChar(char c)
        {
            var input = new INPUT[2];
            
            // 按下
            input[0] = new INPUT
            {
                type = 1, // INPUT_KEYBOARD
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = 0x0004, // KEYEVENTF_UNICODE
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };
            
            // 释放
            input[1] = new INPUT
            {
                type = 1,
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = 0x0004 | 0x0002, // KEYEVENTF_UNICODE | KEYEVENTF_KEYUP
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };
            
            SendInput(2, input, Marshal.SizeOf(typeof(INPUT)));
        }

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        /// <summary>
        /// 可靠地将窗口带到前台
        /// </summary>
        private void BringWindowToForeground(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                Logger.Log("错误: 尝试恢复空窗口句柄");
                return;
            }

            // 检查窗口是否有效
            if (!IsWindow(hWnd))
            {
                Logger.Log($"错误: 窗口句柄 {hWnd} 无效");
                return;
            }

            var windowTitle = GetWindowTitle(hWnd);
            Logger.Log($"准备恢复窗口 - 句柄:{hWnd}, 标题:{windowTitle}");

            // 获取当前线程和窗口线程
            uint currentThreadId = GetCurrentThreadId();
            uint windowThreadId = GetWindowThreadProcessId(hWnd, out _);

            Logger.Log($"线程信息 - 目标:{windowThreadId}, 当前:{currentThreadId}");

            // 如果线程不同，附加输入队列
            if (currentThreadId != windowThreadId)
            {
                AttachThreadInput(windowThreadId, currentThreadId, true);
            }

            // 尝试多种方法激活窗口
            var bringResult = BringWindowToTop(hWnd);
            System.Threading.Thread.Sleep(100);
            var setResult = SetForegroundWindow(hWnd);
            System.Threading.Thread.Sleep(100);
            ShowWindow(hWnd, SW_SHOW);
            System.Threading.Thread.Sleep(100);

            Logger.Log($"激活结果 - BringWindowToTop:{bringResult}, SetForegroundWindow:{setResult}");

            // 分离输入队列
            if (currentThreadId != windowThreadId)
            {
                AttachThreadInput(windowThreadId, currentThreadId, false);
            }

            // 验证窗口是否真的到了前台
            System.Threading.Thread.Sleep(200);
            var currentForeground = GetForegroundWindow();
            var currentTitle = GetWindowTitle(currentForeground);
            
            if (currentForeground == hWnd)
            {
                Logger.Log($"✓ 窗口成功恢复到前台: {currentTitle}");
            }
            else
            {
                Logger.Log($"✗ 窗口恢复失败! 当前前台: {currentTitle} ({currentForeground})");
                // 再试一次，使用更激进的方法
                Logger.Log("尝试二次激活...");
                ForceForegroundWindow(hWnd);
            }
        }

        /// <summary>
        /// 强制激活窗口（备用方案）
        /// </summary>
        private void ForceForegroundWindow(IntPtr hWnd)
        {
            // 最小化再恢复，强制 Windows 切换焦点
            ShowWindow(hWnd, SW_MINIMIZE);
            System.Threading.Thread.Sleep(100);
            ShowWindow(hWnd, SW_SHOW);
            System.Threading.Thread.Sleep(100);
            SetForegroundWindow(hWnd);
            System.Threading.Thread.Sleep(200);
            
            var current = GetForegroundWindow();
            Logger.Log($"二次激活后前台窗口: {GetWindowTitle(current)} ({current})");
        }

        private void OnRecordingStateChanged(object? sender, bool isRecording)
        {
            Dispatcher.Invoke(() =>
            {
                var keyName = _settingsService != null 
                    ? SettingsService.GetKeyName(_settingsService.Settings.HotkeyCode) 
                    : "` 键";
                    
                if (isRecording)
                {
                    RecordButtonIndicator.Fill = Brushes.Red;
                    RecordButtonText.Text = $"正在录音（松开 {keyName} 结束）";
                    StartRecordingTimer();
                }
                else
                {
                    RecordButtonIndicator.Fill = Brushes.Gray;
                    RecordButtonText.Text = $"按住 {keyName} 说话";
                    StopRecordingTimer();
                }
            });
        }

        private void OnProcessingStateChanged(object? sender, bool isProcessing)
        {
            Dispatcher.Invoke(() =>
            {
                StatusProgressBar.Visibility = isProcessing ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        #endregion

        #region UI事件

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleRecording();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            await ConnectWithAutoStartAsync();
        }

        /// <summary>
        /// 连接服务（自动启动服务器如果需要）
        /// </summary>
        private async Task ConnectWithAutoStartAsync()
        {
            StatusText.Text = "正在检查服务...";
            
            // 先检查是否已连接
            if (_speechService != null && await _speechService.CheckConnectionAsync())
            {
                StatusText.Text = "服务已连接";
                
                // 如果已连接但没有模型，尝试自动加载
                if (string.IsNullOrEmpty(_speechService.CurrentModel))
                {
                    await _speechService.AutoLoadModelAsync();
                    UpdateModelStatus(true, _speechService.CurrentModel);
                }
                return;
            }

            // 未连接，尝试自动启动
            if (_speechService == null)
            {
                _speechService = new SpeechRecognitionService(ASR_SERVICE_URL);
                _speechService.StatusChanged += OnStatusChanged;
                _speechService.RecognitionCompleted += OnRecognitionCompleted;
                _speechService.RecordingStateChanged += OnRecordingStateChanged;
                _speechService.ProcessingStateChanged += OnProcessingStateChanged;
            }

            // 尝试启动服务器
            var started = await _speechService.TryStartServerAsync();
            
            if (!started)
            {
                var result = MessageBox.Show(
                    "无法自动启动ASR服务。\n\n请手动启动：\n双击 PythonASR/start_server.bat\n\n是否查看使用说明？",
                    "启动失败",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    var readmePath = System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                        "..", "..", "PythonASR", "使用说明.md");
                    if (System.IO.File.Exists(readmePath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(readmePath) { UseShellExecute = true });
                    }
                }
                return;
            }

            // 服务器已启动，现在初始化连接
            var (connected, models, currentModel) = await _speechService.InitializeAsync();
            
            if (connected)
            {
                _availableModels = models;
                UpdateModelStatus(true, currentModel);
                
                // 如果没有加载模型，自动加载第一个已安装的
                if (string.IsNullOrEmpty(currentModel))
                {
                    StatusText.Text = "正在自动加载模型...";
                    var loaded = await _speechService.AutoLoadModelAsync();
                    if (loaded)
                    {
                        UpdateModelStatus(true, _speechService.CurrentModel);
                    }
                    else if (!models.Any(m => m.Installed))
                    {
                        // 没有已安装的模型，在状态栏显示提示（不阻塞用户）
                        StatusText.Text = "未安装模型，请在设置中下载模型";
                        ModelStatusText.Text = "模型：未安装";
                        ModelStatusText.Foreground = Brushes.Orange;
                    }
                    else
                    {
                        // 有已安装的模型但未加载，在状态栏显示提示
                        StatusText.Text = "已安装模型，请在设置中选择要使用的模型";
                        ModelStatusText.Text = "模型：未加载";
                        ModelStatusText.Foreground = Brushes.Orange;
                    }
                }
            }
            else
            {
                StatusText.Text = "连接到服务失败";
                UpdateModelStatus(false, "");
            }
        }

        private async void ModelManagerButton_Click(object sender, RoutedEventArgs e)
        {
            // 打开模型管理窗口
            var modelManager = new Views.ModelManagerWindow
            {
                Owner = this
            };
            modelManager.ShowDialog();
            
            // 刷新模型状态显示
            await RefreshModelStatusAsync();
        }
        
        /// <summary>
        /// 刷新模型状态显示 - 直接从 ASR 服务获取最新状态
        /// </summary>
        private async Task RefreshModelStatusAsync()
        {
            try
            {
                if (_speechService != null)
                {
                    // 先检查连接状态
                    bool isConnected = await _speechService.CheckConnectionAsync();
                    
                    if (isConnected)
                    {
                        // 直接从 ASR 服务获取最新状态
                        var health = await _speechService.GetHealthAsync();
                        string currentModel = health?.current_model ?? _speechService.CurrentModel;
                        
                        Logger.Log($"刷新模型状态 - 从服务获取当前模型：{currentModel}");
                        
                        // 如果当前没有加载模型，尝试自动加载
                        if (string.IsNullOrEmpty(currentModel))
                        {
                            Logger.Log("模型管理器关闭后，尝试自动加载模型...");
                            bool loaded = await _speechService.AutoLoadModelAsync();
                            if (loaded)
                            {
                                currentModel = _speechService.CurrentModel;
                            }
                        }
                        
                        // 更新 UI 状态（使用新的合并显示方法）
                        UpdateModelStatus(isConnected, currentModel);
                        StatusText.Text = $"已连接 - 当前模型：{currentModel}";
                        Logger.Log($"模型状态已更新：{currentModel}");
                    }
                    else
                    {
                        UpdateModelStatus(false, "");
                        StatusText.Text = "未连接到 ASR 服务";
                        Logger.Log("模型状态：未连接");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"刷新模型状态失败：{ex.Message}");
                UpdateModelStatus(false, "");
            }
        }
        
        // 保留旧方法用于其他调用
        private void SwitchModelButton_Click(object sender, RoutedEventArgs e)
        {
            ShowModelSelectionDialog(false);
        }

        /// <summary>
        /// 打开个人词典管理页面（内部导航）
        /// </summary>
        private void VocabularyButton_Click(object sender, RoutedEventArgs e)
        {
            // 切换到词典页面
            MainContent.Visibility = Visibility.Collapsed;
            VocabularyPageControl.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 打开设置窗口
        /// </summary>
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsService = (App.Current as App)?.Settings;
            var hotkeyService = _hotkeyService;
            
            if (settingsService != null && hotkeyService != null)
            {
                var settingsWindow = new Views.SettingsWindow(settingsService, hotkeyService)
                {
                    Owner = this
                };
                settingsWindow.ShowDialog();
            }
        }

        /// <summary>
        /// 从个人词典页面返回主界面
        /// </summary>
        private void OnVocabularyPageBackRequested(object? sender, EventArgs e)
        {
            VocabularyPageControl.Visibility = Visibility.Collapsed;
            MainContent.Visibility = Visibility.Visible;
        }

        private void ImportAudioButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "WAV音频文件 (*.wav)|*.wav|所有文件 (*.*)|*.*",
                Title = "选择音频文件进行识别",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() == true)
            {
                string filePath = dialog.FileName;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _speechService!.RecognizeFromFileAsync(filePath);
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"识别失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });
            }
        }

        #endregion

        #region 模型选择对话框

        private void ShowModelSelectionDialog(bool firstTime)
        {
            var dialog = new Window
            {
                Title = firstTime ? "首次使用 - 选择模型" : "切换模型",
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 标题
            var titleText = new TextBlock
            {
                Text = firstTime 
                    ? "欢迎使用 WordFlow！\n请选择语音识别模型：" 
                    : "选择要使用的模型：",
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 15),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(titleText, 0);
            grid.Children.Add(titleText);

            // 模型列表
            var listBox = new System.Windows.Controls.ListBox { Margin = new Thickness(0, 0, 0, 15) };
            foreach (var model in _availableModels)
            {
                var item = new StackPanel { Margin = new Thickness(5) };
                item.Children.Add(new TextBlock 
                { 
                    Text = $"{model.Name} ({model.Size})", 
                    FontWeight = FontWeights.Bold,
                    Foreground = model.Installed ? Brushes.Black : Brushes.Gray
                });
                item.Children.Add(new TextBlock 
                { 
                    Text = model.Description,
                    FontSize = 12,
                    Foreground = Brushes.Gray
                });
                item.Children.Add(new TextBlock 
                { 
                    Text = model.Installed ? "✓ 已安装" : "⚠ 未安装（需下载）",
                    FontSize = 11,
                    Foreground = model.Installed ? Brushes.Green : Brushes.Orange,
                    Margin = new Thickness(0, 3, 0, 0)
                });
                
                listBox.Items.Add(new { Model = model, Display = item });
            }
            Grid.SetRow(listBox, 1);
            grid.Children.Add(listBox);

            // 按钮
            var buttonPanel = new StackPanel 
            { 
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            
            var cancelButton = new System.Windows.Controls.Button 
            { 
                Content = "取消", 
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(5, 0, 0, 0)
            };
            cancelButton.Click += (s, e) => dialog.Close();
            
            var okButton = new System.Windows.Controls.Button 
            { 
                Content = "确定", 
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(5, 0, 0, 0),
                IsDefault = true
            };
            okButton.Click += async (s, e) =>
            {
                if (listBox.SelectedItem == null)
                {
                    MessageBox.Show("请选择一个模型", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var selected = listBox.SelectedItem.GetType().GetProperty("Model")?.GetValue(listBox.SelectedItem) 
                    as SpeechRecognitionService.ModelInfo;
                
                if (selected != null)
                {
                    dialog.Close();
                    
                    if (!selected.Installed)
                    {
                        MessageBox.Show(
                            $"模型 '{selected.Name}' 需要下载。\n\n" +
                            "请在Python服务端运行以下命令下载：\n" +
                            $"python download_model.py {selected.Id}",
                            "模型未安装",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else if (_speechService != null)
                    {
                        var success = await _speechService.SwitchModelAsync(selected.Id);
                        if (success)
                        {
                            UpdateModelStatus(true, selected.Id);
                        }
                        else
                        {
                            // 切换失败，不更新UI，保持当前状态
                            MessageBox.Show(
                                $"模型 '{selected.Name}' 切换失败。\n\n请检查服务器日志了解详情。",
                                "切换失败",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                }
            };

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(okButton);
            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;
            dialog.ShowDialog();
        }

        #endregion

        #region 私有方法

        private void ToggleRecording()
        {
            if (_speechService == null) return;

            if (_speechService.IsRecording)
            {
                _ = StopRecordingAsync();
            }
            else
            {
                StartRecording();
            }
        }

        private void StartRecording()
        {
            // 关键：先获取焦点窗口，再做任何可能改变焦点的事
            var currentWindow = GetForegroundWindow();
            var myWindowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var currentTitle = GetWindowTitle(currentWindow);
            
            Logger.Log($"录音键按下 - 当前焦点窗口: {currentTitle} ({currentWindow})");
            
            // 保存当前焦点窗口（即使是 WordFlow 自己也保存，后续判断）
            if (currentWindow != IntPtr.Zero)
            {
                _lastForegroundWindow = currentWindow;
                _recordingStartTime = DateTime.Now; // 记录录音开始时间
                
                if (currentWindow != myWindowHandle)
                {
                    Logger.Log($"✓ 保存外部目标窗口: {GetWindowTitle(_lastForegroundWindow)} ({_lastForegroundWindow})");
                }
                else
                {
                    Logger.Log($"⚠ 当前焦点是 WordFlow 自身，文字将输入到本程序");
                }
                
                // 更新 UI
                ShowRecordingNotification();
                
                // 启动录音
                _speechService?.StartRecording();
            }
            else
            {
                Logger.Log($"✗ 无法获取当前焦点窗口");
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    StatusText.Text = "无法获取焦点窗口，请点击一个窗口后再试";
                });
            }
        }

        /// <summary>
        /// 显示录音提示（不窃取焦点）
        /// </summary>
        private void ShowRecordingNotification()
        {
            var keyName = _settingsService != null 
                ? SettingsService.GetKeyName(_settingsService.Settings.HotkeyCode) 
                : "` 键";
                
            // 使用 Application.Dispatcher.BeginInvoke，确保窗口隐藏时也能工作
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                StatusText.Text = $"正在录音... (松开 {keyName} 结束)";
                RecordButtonIndicator.Fill = Brushes.Red;
                RecordButtonText.Text = $"正在录音（松开 {keyName} 结束）";
                StartRecordingTimer();
            });
            
            Logger.Log($"ShowRecordingNotification: UI 已更新 - 录音中");
        }

        private async Task StopRecordingAsync()
        {
            if (_speechService == null) return;
            await _speechService.StopRecordingAndRecognizeAsync();
        }

        private void StartRecordingTimer()
        {
            // 防止重复启动：如果定时器已在运行，先停止
            if (_recordingTimer != null && _recordingTimer.IsEnabled)
            {
                _recordingTimer.Stop();
                _recordingTimer = null;
            }
            
            _recordingSeconds = 0;
            // 使用 Application.Dispatcher 创建定时器，确保窗口隐藏时也能工作
            _recordingTimer = new DispatcherTimer(
                TimeSpan.FromSeconds(1), 
                DispatcherPriority.Normal,
                (s, e) =>
                {
                    _recordingSeconds++;
                    // 同时更新状态栏和录音按钮文本
                    var keyName = _settingsService != null 
                        ? SettingsService.GetKeyName(_settingsService.Settings.HotkeyCode) 
                        : "` 键";
                    StatusText.Text = $"正在录音... {_recordingSeconds}s";
                    RecordButtonText.Text = $"正在录音（{_recordingSeconds}s，松开 {keyName} 结束）";
                },
                Application.Current.Dispatcher);
            _recordingTimer.Start();
            
            Logger.Log($"StartRecordingTimer: 定时器已启动");
        }

        private void StopRecordingTimer()
        {
            _recordingTimer?.Stop();
            _recordingTimer = null;
        }

        /// <summary>
        /// 显示底部转录提示框
        /// </summary>
        private void ShowTranscriptPopup(string text)
        {
            try
            {
                if (_transcriptPopup == null)
                {
                    _transcriptPopup = new Views.TranscriptPopupWindow
                    {
                        Owner = this
                    };
                }

                // 设置窗口位置（屏幕底部中央）
                var screenWidth = System.Windows.SystemParameters.PrimaryScreenWidth;
                var screenHeight = System.Windows.SystemParameters.PrimaryScreenHeight;
                var taskbarHeight = System.Windows.SystemParameters.PrimaryScreenHeight - 
                                    System.Windows.SystemParameters.WorkArea.Height;

                _transcriptPopup.Left = (screenWidth - _transcriptPopup.Width) / 2;
                _transcriptPopup.Top = screenHeight - _transcriptPopup.Height - taskbarHeight - 20;

                _transcriptPopup.ShowTranscript(text);
            }
            catch (Exception ex)
            {
                // 弹窗显示失败时，只在状态栏显示提示，不影响主要功能
                Logger.Log($"显示转录弹窗失败：{ex.Message}");
                StatusText.Text = $"识别完成：{text.Length} 字";
            }
        }

        /// <summary>
        /// 更新模型状态显示（合并连接状态和模型状态）
        /// </summary>
        private void UpdateModelStatus(bool isConnected, string modelName)
        {
            // 获取状态指示器（椭圆）
            var indicator = FindName("ModelStatusIndicator") as System.Windows.Shapes.Ellipse;
            
            if (isConnected && !string.IsNullOrEmpty(modelName))
            {
                // 已连接且有模型 - 显示绿色
                ModelStatusText.Text = modelName;
                ModelStatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Material Green 500
                if (indicator != null)
                    indicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            }
            else if (isConnected)
            {
                // 已连接但未选择模型 - 显示橙色
                ModelStatusText.Text = "未选择模型";
                ModelStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Material Orange 500
                if (indicator != null)
                    indicator.Fill = new SolidColorBrush(Color.FromRgb(255, 152, 0));
            }
            else
            {
                // 未连接 - 显示灰色
                ModelStatusText.Text = "未连接";
                ModelStatusText.Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Material Gray 500
                if (indicator != null)
                    indicator.Fill = new SolidColorBrush(Color.FromRgb(158, 158, 158));
            }
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 注意：服务（hotkeyService, speechService）由 App 层管理
            // 不要在这里 Dispose，因为点击关闭按钮只是最小化到托盘
            // App 的 OnMainWindowClosing 会设置 e.Cancel = true
        }

        #endregion
    }
}
