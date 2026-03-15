using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WordFlow.Infrastructure;
using WordFlow.Resources.Strings;
using WordFlow.Services;
using WordFlow.Utils;

namespace WordFlow.Views
{
    /// <summary>
    /// 模型管理窗口 - 管理已安装模型和下载新模型
    /// </summary>
    public partial class ModelManagerWindow : LocalizedWindow
    {
        private readonly FirstRunService _firstRunService;
        private readonly ModelDownloadService _downloadService;
        private CancellationTokenSource? _downloadCts;
        private bool _isDownloading = false;
        
        // 记录最后下载的模型 ID
        private string? _lastDownloadedModelId;

        public ModelManagerWindow()
        {
            InitializeComponent();
            _firstRunService = new FirstRunService();
            _downloadService = new ModelDownloadService();
            
            // 修复旧版模型目录名称（sensevoice-small -> sensevoice-small-onnx）
            _downloadService.FixModelDirectoryNames();
            
            // 订阅下载进度事件
            _downloadService.ProgressChanged += OnDownloadProgressChanged;
            _downloadService.StatusChanged += OnDownloadStatusChanged;
            
            Loaded += async (s, e) => await LoadModelsAsync();
            
            // 订阅窗口关闭事件，确保下载被正确取消
            Closing += ModelManagerWindow_Closing;
        }

        /// <summary>
        /// 窗口关闭时清理资源
        /// </summary>
        private void ModelManagerWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 取消下载
            if (_downloadCts != null && !_downloadCts.IsCancellationRequested)
            {
                _downloadCts.Cancel();
            }
            
            // 取消事件订阅，防止内存泄漏和关闭窗口后的 UI 更新
            _downloadService.ProgressChanged -= OnDownloadProgressChanged;
            _downloadService.StatusChanged -= OnDownloadStatusChanged;
        }

        protected override string GetWindowTitleResourceKey() => "ModelManager_Title";

        /// <summary>
        /// 加载模型列表 - 优先从 ASR 服务获取已安装模型，再扫描本地目录 + 配置文件
        /// </summary>
        private async Task LoadModelsAsync()
        {
            try
            {
                ModelListPanel.Children.Clear();
                
                // 1. 首先尝试从 ASR 服务获取已安装模型列表（最准确）
                var installedModelIds = new HashSet<string>();
                var currentModelId = "";
                bool serviceConnected = false;
                
                try
                {
                    var speechService = new SpeechRecognitionService("http://127.0.0.1:5000");
                    bool isConnected = await speechService.CheckConnectionAsync();
                    
                    if (isConnected)
                    {
                        serviceConnected = true;
                        Logger.Log("ASR 服务已连接，从服务获取模型列表...");
                        
                        // 获取健康检查信息（包含已安装模型和当前模型）
                        var healthResponse = await speechService.GetHealthAsync();
                        currentModelId = healthResponse?.current_model ?? "";
                        var serviceModels = healthResponse?.installed_models ?? new List<string>();
                        foreach (var modelId in serviceModels)
                        {
                            installedModelIds.Add(modelId);
                        }
                        
                        Logger.Log($"从服务获取到 {serviceModels.Count} 个已安装模型，当前模型：{currentModelId}");
                    }
                    
                    speechService.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Log($"从 ASR 服务获取模型列表失败：{ex.Message}，将扫描本地目录");
                    // 不抛出异常，继续执行本地扫描
                }
                
                // 2. 无论服务是否连接，都扫描本地目录（确保本地模型被检测）
                Logger.Log("扫描本地模型目录...");
                var outputDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var outputModelsDir = Path.Combine(outputDir ?? "", "PythonASR", "models");
                
                Logger.Log($"加载模型列表：输出目录模型路径={outputModelsDir}");
                
                if (Directory.Exists(outputModelsDir))
                {
                    var modelDirs = Directory.GetDirectories(outputModelsDir);
                    foreach (var modelDir in modelDirs)
                    {
                        if (IsValidModel(modelDir))
                        {
                            var modelId = Path.GetFileName(modelDir);
                            if (installedModelIds.Add(modelId))
                            {
                                Logger.Log($"本地扫描发现模型：{modelId}");
                            }
                        }
                    }
                }
                
                Logger.Log($"本地扫描后已安装模型数量：{installedModelIds.Count}");
                
                // 3. 显示已安装的模型
                foreach (var modelId in installedModelIds)
                {
                    // 获取模型详细信息（从配置文件或默认）
                    var modelInfo = await GetModelInfoAsync(modelId);
                    
                    // 如果配置文件中没有大小信息，扫描本地目录计算实际大小
                    string sizeStr;
                    if (modelInfo != null && !string.IsNullOrEmpty(modelInfo.Size) && modelInfo.Size != "未知")
                    {
                        sizeStr = modelInfo.Size;
                    }
                    else
                    {
                        // 扫描本地目录计算大小（使用已声明的变量）
                        var modelDirPath = Path.Combine(outputModelsDir, modelId);
                        
                        if (Directory.Exists(modelDirPath))
                        {
                            var sizeBytes = GetDirectorySize(modelDirPath);
                            sizeStr = FormatSize(sizeBytes);
                        }
                        else
                        {
                            sizeStr = modelInfo?.Size ?? "未知";
                        }
                    }
                    
                    var description = modelInfo?.Description ?? "本地已安装模型";
                    bool isCurrentModel = (modelId == currentModelId);
                    
                    var modelItem = CreateModelItem(
                        modelId,
                        modelInfo?.Name ?? modelId,
                        description,
                        sizeStr,
                        true,       // 已安装
                        false,      // 不显示下载按钮
                        isCurrentModel); // 是否当前模型
                    ModelListPanel.Children.Add(modelItem);
                }
                
                // 4. 从配置文件获取可下载的模型列表
                var availableModels = await _downloadService.GetAvailableModelsAsync();
                Logger.Log($"可用模型配置数量：{availableModels.Count}");
                
                // 添加未安装的模型到列表
                foreach (var model in availableModels)
                {
                    if (!installedModelIds.Contains(model.Id))
                    {
                        Logger.Log($"添加可下载模型：{model.Id}");
                        var modelItem = CreateModelItem(
                            model.Id,
                            model.Name,
                            model.Description,
                            model.Size,
                            false,  // 未安装
                            true,   // 显示下载按钮
                            false); // 不是当前模型
                        ModelListPanel.Children.Add(modelItem);
                    }
                }
                
                // 如果没有任何模型（本地和配置都没有）
                if (installedModelIds.Count == 0 && availableModels.Count == 0)
                {
                    var noModelText = new TextBlock
                    {
                        Text = Strings.ModelManager_NoModelsAvailable,
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Colors.Gray),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(20)
                    };
                    ModelListPanel.Children.Add(noModelText);
                }
                
                Logger.Log($"模型列表加载完成，共 {installedModelIds.Count + availableModels.Count} 个模型");
            }
            catch (Exception ex)
            {
                Logger.Log($"加载模型列表失败：{ex.Message}");
                Logger.Log($"堆栈跟踪：{ex.StackTrace}");
                MessageBox.Show($"加载模型列表失败：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 获取模型信息（从配置文件）
        /// </summary>
        private async Task<ModelInfo?> GetModelInfoAsync(string modelId)
        {
            try
            {
                var availableModels = await _downloadService.GetAvailableModelsAsync();
                return availableModels.FirstOrDefault(m => m.Id == modelId);
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// 获取目录大小
        /// </summary>
        private long GetDirectorySize(string dirPath)
        {
            try
            {
                var dirInfo = new DirectoryInfo(dirPath);
                return dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// 格式化大小显示
        /// </summary>
        private string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        
        /// <summary>
        /// 检查目录是否包含有效的模型文件
        /// </summary>
        private bool IsValidModel(string modelDir)
        {
            var hasModel = File.Exists(Path.Combine(modelDir, "model.onnx")) ||
                          File.Exists(Path.Combine(modelDir, "model.int8.onnx"));
            var hasTokens = File.Exists(Path.Combine(modelDir, "tokens.txt"));
            return hasModel && hasTokens;
        }

        /// <summary>
        /// 创建模型项 UI - 卡片式布局
        /// </summary>
        private Border CreateModelItem(string modelId, string name, string description, string size, bool isInstalled, bool showDownloadButton = false, bool isCurrentModel = false)
        {
            var cardBorder = new Border
            {
                Background = isInstalled ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(15)
            };
            
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            // 左侧信息区域
            var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 20, 0) };
            
            // 名称和状态标签（第一行）
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            
            var nameText = new TextBlock 
            { 
                Text = name, 
                FontWeight = FontWeights.SemiBold,
                FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromRgb(33, 33, 33))
            };
            headerPanel.Children.Add(nameText);
            
            // 状态标签
            string statusTextContent;
            SolidColorBrush statusBadgeBg;
            SolidColorBrush statusBadgeFg;
            
            if (!isInstalled)
            {
                statusTextContent = Strings.ModelManager_Status_NotDownloaded;
                statusBadgeBg = new SolidColorBrush(Color.FromRgb(255, 243, 205));
                statusBadgeFg = new SolidColorBrush(Color.FromRgb(230, 81, 0));
            }
            else if (isCurrentModel)
            {
                statusTextContent = Strings.ModelManager_Status_Current;
                statusBadgeBg = new SolidColorBrush(Color.FromRgb(187, 222, 251));
                statusBadgeFg = new SolidColorBrush(Color.FromRgb(25, 118, 210));
            }
            else
            {
                statusTextContent = Strings.ModelManager_Status_Installed;
                statusBadgeBg = new SolidColorBrush(Color.FromRgb(200, 230, 201));
                statusBadgeFg = new SolidColorBrush(Color.FromRgb(46, 125, 50));
            }
            
            var statusBadge = new Border
            {
                Background = statusBadgeBg,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(10, 0, 0, 0),
                Child = new TextBlock
                {
                    Text = statusTextContent,
                    FontSize = 11,
                    FontWeight = FontWeights.Medium,
                    Foreground = statusBadgeFg
                }
            };
            headerPanel.Children.Add(statusBadge);
            
            infoPanel.Children.Add(headerPanel);
            
            // 描述文字（完整显示）
            var descText = new TextBlock 
            { 
                Text = description, 
                FontSize = 13, 
                Foreground = new SolidColorBrush(Color.FromRgb(117, 117, 117)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20,
                Margin = new Thickness(0, 0, 0, 8)
            };
            infoPanel.Children.Add(descText);
            
            // 大小信息
            var sizeText = new TextBlock 
            { 
                Text = $"💾 {size}", 
                FontSize = 12, 
                Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158))
            };
            infoPanel.Children.Add(sizeText);
            
            Grid.SetColumn(infoPanel, 0);
            grid.Children.Add(infoPanel);
            
            // 右侧按钮区域
            var buttonPanel = new StackPanel 
            { 
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            
            if (isInstalled)
            {
                var useButton = new Button
                {
                    Content = "✓ " + Strings.ModelManager_Button_Use,
                    Style = (Style)FindResource("PrimaryButton"),
                    Padding = new Thickness(20, 10, 20, 10),
                    Margin = new Thickness(0, 0, 0, 8),
                    Tag = modelId,
                    FontSize = 13
                };
                useButton.Click += UseButton_Click;
                
                var deleteButton = new Button
                {
                    Content = "🗑️ " + Strings.ModelManager_Button_Delete,
                    Style = (Style)FindResource("DangerButton"),
                    Padding = new Thickness(20, 10, 20, 10),
                    Tag = modelId,
                    FontSize = 13
                };
                deleteButton.Click += DeleteButton_Click;
                
                buttonPanel.Children.Add(useButton);
                buttonPanel.Children.Add(deleteButton);
            }
            else if (showDownloadButton)
            {
                var downloadButton = new Button
                {
                    Content = "⬇️ " + Strings.ModelManager_Button_Download,
                    Style = (Style)FindResource("PrimaryButton"),
                    Padding = new Thickness(20, 10, 20, 10),
                    Tag = modelId,
                    FontSize = 13
                };
                downloadButton.Click += DownloadButton_Click;
                buttonPanel.Children.Add(downloadButton);
            }
            
            Grid.SetColumn(buttonPanel, 1);
            grid.Children.Add(buttonPanel);
            
            cardBorder.Child = grid;
            return cardBorder;
        }

        /// <summary>
        /// 使用模型按钮点击
        /// </summary>
        private async void UseButton_Click(object sender, RoutedEventArgs e)
        {
            var modelId = (string)((Button)sender).Tag;
            
            try
            {
                // 创建 SpeechRecognitionService 客户端
                var speechService = new SpeechRecognitionService("http://127.0.0.1:5000");
                
                // 检查服务是否连接
                bool isConnected = await speechService.CheckConnectionAsync();
                
                if (!isConnected)
                {
                    // 服务未连接，尝试自动启动
                    var result = MessageBox.Show(
                        string.Format(Strings.ModelManager_ServiceNotRunning, modelId),
                        Strings.ModelManager_ServiceNotRunning,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        var started = await speechService.TryStartServerAsync();
                        if (!started)
                        {
                            MessageBox.Show(
                                Strings.ModelManager_ServiceStartFailed,
                                Strings.ModelManager_ServiceStartFailed,
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                            speechService.Dispose();
                            return;
                        }
                    }
                    else
                    {
                        speechService.Dispose();
                        return;
                    }
                }
                
                // 切换模型
                Logger.Log($"正在切换模型：{modelId}");
                bool loaded = await speechService.SwitchModelAsync(modelId);
                
                if (loaded)
                {
                    Logger.Log($"模型加载成功：{modelId}");
                    MessageBox.Show(
                        string.Format(Strings.ModelManager_SwitchSuccess, modelId),
                        Strings.ModelManager_SwitchSuccess,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    Logger.Log($"模型加载失败：{modelId}");
                    MessageBox.Show(
                        Strings.ModelManager_SwitchFailed,
                        Strings.ModelManager_SwitchFailed,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                
                speechService.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Log($"切换模型失败：{ex.Message}");
                MessageBox.Show(
                    $"切换模型失败：{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 删除模型按钮点击
        /// </summary>
        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var modelId = (string)((Button)sender).Tag;
            
            var result = MessageBox.Show(
                string.Format(Strings.ModelManager_ConfirmDelete, modelId),
                Strings.ModelManager_ConfirmDelete,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var modelsDir = _downloadService.GetModelsDir();
                    var modelPath = Path.Combine(modelsDir, modelId);
                    
                    if (Directory.Exists(modelPath))
                    {
                        Directory.Delete(modelPath, true);
                        Logger.Log($"模型已删除: {modelId}");
                        await LoadModelsAsync(); // 刷新列表
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除失败: {ex.Message}", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 下载按钮点击 - 直接下载点击的模型
        /// </summary>
        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading)
            {
                MessageBox.Show(Strings.ModelManager_DownloadingPrompt, Strings.ModelManager_DownloadingPrompt,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // 直接获取点击的模型 ID
            var modelId = (string)((Button)sender).Tag;
            
            // 从配置文件获取模型信息
            var availableModels = await _downloadService.GetAvailableModelsAsync();
            var model = availableModels.FirstOrDefault(m => m.Id == modelId);
            
            if (model == null)
            {
                MessageBox.Show(string.Format(Strings.ModelManager_ModelNotFound, modelId), Strings.ModelManager_ModelNotFound,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            // 确认下载
            var confirmResult = MessageBox.Show(
                string.Format(Strings.ModelManager_ConfirmDownload, model.Name, model.Size),
                Strings.ModelManager_ConfirmDownload,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (confirmResult == MessageBoxResult.Yes)
            {
                await StartDownloadAsync(model);
            }
        }

        /// <summary>
        /// 开始下载
        /// </summary>
        private async Task StartDownloadAsync(ModelInfo? modelToDownload = null)
        {
            _isDownloading = true;
            _downloadCts = new CancellationTokenSource();
            
            try
            {
                // 显示进度区域
                DownloadProgressGrid.Visibility = Visibility.Visible;
                BackgroundDownloadButton.Visibility = Visibility.Visible;
                BackgroundDownloadButton.Content = "⏸️ 取消下载";
                
                // 获取要下载的模型
                var model = modelToDownload ?? await _downloadService.GetDefaultModelAsync();
                if (model == null)
                {
                    MessageBox.Show("未找到可下载的模型", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                Logger.Log($"开始下载模型：{model.Id} ({model.Name})");
                OnDownloadStatusChanged(this, $"正在下载 {model.Name}...");
                
                // 使用 ModelDownloadService 直接下载
                var result = await _downloadService.DownloadModelAsync(model, true, _downloadCts.Token);
                
                if (result.Success)
                {
                    DownloadStatusText.Text = Strings.ModelManager_DownloadComplete;
                    DownloadProgressText.Text = "100%";
                    
                    // 记录下载的模型 ID，用于关闭窗口后自动加载
                    _lastDownloadedModelId = model.Id;
                    
                    // 刷新列表
                    await LoadModelsAsync();
                    
                    // 隐藏进度
                    DownloadProgressGrid.Visibility = Visibility.Collapsed;
                    BackgroundDownloadButton.Visibility = Visibility.Collapsed;
                    
                    // 尝试自动加载模型
                    await TryAutoLoadModelAsync(model.Id);
                }
                else
                {
                    DownloadStatusText.Text = Strings.ModelManager_DownloadFailed;
                    DownloadStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    MessageBox.Show(Strings.ModelManager_DownloadFailed, Strings.ModelManager_DownloadFailed,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (OperationCanceledException)
            {
                DownloadStatusText.Text = Strings.ModelManager_DownloadCancelled;
                MessageBox.Show(Strings.ModelManager_DownloadCancelled, Strings.ModelManager_DownloadCancelled,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DownloadStatusText.Text = Strings.ModelManager_DownloadFailed;
                DownloadStatusText.Foreground = new SolidColorBrush(Colors.Red);
                MessageBox.Show($"{Strings.ModelManager_DownloadFailed}: {ex.Message}", Strings.ModelManager_DownloadFailed,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isDownloading = false;
                _downloadCts?.Dispose();
                _downloadCts = null;
            }
        }
        
        /// <summary>
        /// 尝试自动加载模型
        /// </summary>
        private async Task TryAutoLoadModelAsync(string modelId)
        {
            try
            {
                Logger.Log($"尝试自动加载模型：{modelId}");
                
                // 创建 SpeechRecognitionService 客户端
                var speechService = new SpeechRecognitionService("http://127.0.0.1:5000");
                
                // 检查服务是否连接
                bool isConnected = await speechService.CheckConnectionAsync();
                
                if (isConnected)
                {
                    // 服务已连接，尝试加载模型
                    Logger.Log("ASR 服务已连接，尝试加载模型...");
                    var loaded = await speechService.SwitchModelAsync(modelId);
                    
                    if (loaded)
                    {
                        Logger.Log($"模型加载成功：{modelId}");
                        MessageBox.Show(
                            $"模型下载并加载成功！\n\n现在可以开始使用语音输入了。",
                            "成功",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        Logger.Log($"模型加载失败：{modelId}");
                        MessageBox.Show(
                            $"模型已下载完成，但加载失败。\n\n请检查 ASR 服务是否正常运行。",
                            "提示",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
                else
                {
                    // 服务未连接，提示用户
                    Logger.Log("ASR 服务未连接，提示用户");
                    var result = MessageBox.Show(
                        $"模型已下载完成！\n\n但 ASR 服务未启动，是否现在启动服务？",
                        "启动服务",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        // 尝试启动服务
                        var started = await speechService.TryStartServerAsync();
                        if (started)
                        {
                            // 服务启动成功，加载模型
                            var loaded = await speechService.SwitchModelAsync(modelId);
                            if (loaded)
                            {
                                MessageBox.Show(
                                    $"服务已启动并加载模型：{modelId}\n\n现在可以开始使用语音输入了。",
                                    "成功",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            }
                        }
                        else
                        {
                            MessageBox.Show(
                                $"服务启动失败，请手动启动 ASR 服务。\n\n启动方法：\n双击 PythonASR/start_server.bat",
                                "提示",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                }
                
                speechService.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Log($"自动加载模型失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 下载进度更新
        /// </summary>
        private void OnDownloadProgressChanged(object? sender, DownloadProgressEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // 检查窗口是否已关闭
                    if (!IsVisible) return;
                    
                    // 更新进度条
                    DownloadProgressBar.Value = e.ProgressPercentage;
                    
                    // 更新进度百分比文本
                    DownloadProgressText.Text = $"{e.ProgressPercentage:F1}%";
                    
                    // 可选：显示速度和剩余时间
                    if (e.Speed > 0 && e.RemainingTime.TotalSeconds > 0)
                    {
                        var speedMB = e.Speed / 1024 / 1024.0;
                        var remainingSec = (int)e.RemainingTime.TotalSeconds;
                        Logger.Log($"下载进度：{e.ProgressPercentage:F1}% - 速度：{speedMB:F2} MB/s - 剩余：{remainingSec}秒");
                    }
                });
            }
            catch (Exception ex)
            {
                // 忽略窗口已关闭时的 UI 更新异常
                Logger.Log($"更新下载进度失败（窗口可能已关闭）: {ex.Message}");
            }
        }

        /// <summary>
        /// 下载状态更新
        /// </summary>
        private void OnDownloadStatusChanged(object? sender, string status)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // 检查窗口是否已关闭
                    if (!IsVisible) return;
                    
                    DownloadStatusText.Text = status;
                    Logger.Log($"下载状态：{status}");
                });
            }
            catch (Exception ex)
            {
                // 忽略窗口已关闭时的 UI 更新异常
                Logger.Log($"更新下载状态失败（窗口可能已关闭）: {ex.Message}");
            }
        }

        /// <summary>
        /// 后台下载按钮点击（实际为取消）
        /// </summary>
        private void BackgroundDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading && _downloadCts != null)
            {
                _downloadCts.Cancel();
                BackgroundDownloadButton.Content = Strings.ModelManager_Button_CancelDownload;
            }
        }

        /// <summary>
        /// 刷新按钮点击
        /// </summary>
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadModelsAsync();
        }

        /// <summary>
        /// 关闭按钮点击
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading)
            {
                var result = MessageBox.Show(
                    Strings.ModelManager_CloseWhileDownloading,
                    Strings.ModelManager_CloseWhileDownloading,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }
            
            Close();
        }

        /// <summary>
        /// 查看日志按钮点击
        /// </summary>
        private void ViewLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logFilePath = Logger.GetLogFilePath();
                var logDirectory = Logger.GetLogDirectory();
                
                if (System.IO.File.Exists(logFilePath))
                {
                    // 显示最近日志内容
                    var recentLogs = Logger.GetRecentLogs(200);
                    
                    var logWindow = new Window
                    {
                        Title = "WordFlow 运行日志",
                        Width = 800,
                        Height = 600,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = this
                    };
                    
                    var grid = new Grid();
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    
                    // 日志文本框
                    var textBox = new TextBox
                    {
                        Text = recentLogs,
                        IsReadOnly = true,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Margin = new Thickness(10, 10, 10, 10),
                        TextWrapping = TextWrapping.NoWrap,
                        AcceptsReturn = true,
                        AcceptsTab = true
                    };
                    Grid.SetRow(textBox, 0);
                    grid.Children.Add(textBox);
                    
                    // 按钮面板
                    var buttonPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(10)
                    };
                    
                    var openFolderButton = new Button
                    {
                        Content = "📂 打开日志文件夹",
                        Padding = new Thickness(15, 8, 15, 8),
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    openFolderButton.Click += (s, args) =>
                    {
                        System.Diagnostics.Process.Start(new ProcessStartInfo
                        {
                            FileName = logDirectory,
                            UseShellExecute = true
                        });
                    };
                    
                    var openFileButton = new Button
                    {
                        Content = "📄 用记事本打开",
                        Padding = new Thickness(15, 8, 15, 8),
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    openFileButton.Click += (s, args) =>
                    {
                        System.Diagnostics.Process.Start(new ProcessStartInfo
                        {
                            FileName = logFilePath,
                            UseShellExecute = true
                        });
                    };
                    
                    var closeButton = new Button
                    {
                        Content = "关闭",
                        Padding = new Thickness(15, 8, 15, 8),
                        IsCancel = true
                    };
                    closeButton.Click += (s, args) => logWindow.Close();
                    
                    buttonPanel.Children.Add(openFolderButton);
                    buttonPanel.Children.Add(openFileButton);
                    buttonPanel.Children.Add(closeButton);
                    
                    Grid.SetRow(buttonPanel, 1);
                    grid.Children.Add(buttonPanel);
                    
                    logWindow.Content = grid;
                    logWindow.ShowDialog();
                }
                else
                {
                    MessageBox.Show("暂无日志记录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开日志失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // OnClosing 已移至 ModelManagerWindow_Closing 方法
    }
}
