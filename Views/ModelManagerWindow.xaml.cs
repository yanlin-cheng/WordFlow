using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WordFlow.Services;
using WordFlow.Utils;

namespace WordFlow.Views
{
    /// <summary>
    /// 模型管理窗口 - 管理已安装模型和下载新模型
    /// </summary>
    public partial class ModelManagerWindow : Window
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
            
            // 订阅下载进度事件
            _downloadService.ProgressChanged += OnDownloadProgressChanged;
            _downloadService.StatusChanged += OnDownloadStatusChanged;
            
            Loaded += async (s, e) => await LoadModelsAsync();
        }

        /// <summary>
        /// 加载模型列表 - 扫描本地目录 + 配置文件中的可下载模型
        /// </summary>
        private async Task LoadModelsAsync()
        {
            try
            {
                ModelListPanel.Children.Clear();
                
                var outputDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var outputModelsDir = Path.Combine(outputDir ?? "", "PythonASR", "models");
                
                Logger.Log($"加载模型列表：输出目录模型路径={outputModelsDir}");
                
                // 扫描本地已安装的模型
                var installedModelIds = new HashSet<string>();
                
                if (Directory.Exists(outputModelsDir))
                {
                    var modelDirs = Directory.GetDirectories(outputModelsDir);
                    foreach (var modelDir in modelDirs)
                    {
                        if (IsValidModel(modelDir))
                        {
                            var modelId = Path.GetFileName(modelDir);
                            installedModelIds.Add(modelId);
                            Logger.Log($"发现本地模型：{modelId}");
                            
                            // 获取模型目录大小
                            var size = GetDirectorySize(modelDir);
                            var sizeStr = FormatSize(size);
                            
                            var modelItem = CreateModelItem(
                                modelId,
                                modelId,
                                "本地已安装模型",
                                sizeStr,
                                true,   // 已安装
                                false); // 不显示下载按钮
                            ModelListPanel.Children.Add(modelItem);
                        }
                    }
                }
                
                Logger.Log($"已安装模型数量：{installedModelIds.Count}");
                
                // 从配置文件获取可下载的模型列表
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
                            true);  // 显示下载按钮
                        ModelListPanel.Children.Add(modelItem);
                    }
                }
                
                // 如果没有任何模型（本地和配置都没有）
                if (installedModelIds.Count == 0 && availableModels.Count == 0)
                {
                    var noModelText = new TextBlock
                    {
                        Text = "暂无可用模型\n\n请检查网络连接或确认模型配置正确",
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Colors.Gray),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(20)
                    };
                    ModelListPanel.Children.Add(noModelText);
                }
                
                Logger.Log($"模型列表加载完成");
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
        /// 创建模型项 UI
        /// </summary>
        /// <param name="modelId">模型 ID</param>
        /// <param name="name">模型名称</param>
        /// <param name="description">模型描述</param>
        /// <param name="size">模型大小</param>
        /// <param name="isInstalled">是否已安装</param>
        /// <param name="showDownloadButton">是否显示下载按钮（用于未安装的模型）</param>
        private Grid CreateModelItem(string modelId, string name, string description, string size, bool isInstalled, bool showDownloadButton = false)
        {
            var grid = new Grid
            {
                Height = 60,
                Margin = new Thickness(0, 2, 0, 2),
                Background = isInstalled ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromRgb(250, 250, 250))
            };
            
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            
            // 模型信息
            var infoPanel = new StackPanel 
            { 
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(15, 0, 15, 0)
            };
            Grid.SetColumn(infoPanel, 0);
            
            var nameText = new TextBlock 
            { 
                Text = name, 
                FontWeight = FontWeights.SemiBold,
                FontSize = 14
            };
            var descText = new TextBlock 
            { 
                Text = description, 
                FontSize = 11, 
                Foreground = new SolidColorBrush(Colors.Gray),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 3, 0, 0)
            };
            
            infoPanel.Children.Add(nameText);
            infoPanel.Children.Add(descText);
            grid.Children.Add(infoPanel);
            
            // 大小
            var sizeText = new TextBlock 
            { 
                Text = size, 
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0)
            };
            Grid.SetColumn(sizeText, 1);
            grid.Children.Add(sizeText);
            
            // 状态
            var statusText = new TextBlock 
            { 
                Text = isInstalled ? "✅ 已安装" : "⬇️ 未下载",
                Foreground = isInstalled ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Orange),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.Medium,
                Margin = new Thickness(0)
            };
            Grid.SetColumn(statusText, 2);
            grid.Children.Add(statusText);
            
            // 操作按钮
            var buttonPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(buttonPanel, 3);
            
            if (isInstalled)
            {
                // 已安装：显示使用和删除按钮
                var useButton = new Button
                {
                    Content = "使用",
                    Style = (Style)FindResource("PrimaryButton"),
                    Padding = new Thickness(12, 6, 12, 6),
                    Margin = new Thickness(0, 0, 8, 0),
                    Tag = modelId
                };
                useButton.Click += UseButton_Click;
                
                var deleteButton = new Button
                {
                    Content = "删除",
                    Style = (Style)FindResource("DangerButton"),
                    Padding = new Thickness(12, 6, 12, 6),
                    Tag = modelId
                };
                deleteButton.Click += DeleteButton_Click;
                
                buttonPanel.Children.Add(useButton);
                buttonPanel.Children.Add(deleteButton);
            }
            else if (showDownloadButton)
            {
                // 未下载：显示下载按钮
                var downloadButton = new Button
                {
                    Content = "📥 下载",
                    Style = (Style)FindResource("PrimaryButton"),
                    Padding = new Thickness(12, 6, 12, 6),
                    Tag = modelId
                };
                downloadButton.Click += DownloadButton_Click;
                buttonPanel.Children.Add(downloadButton);
            }
            
            grid.Children.Add(buttonPanel);
            
            return grid;
        }

        /// <summary>
        /// 使用模型按钮点击
        /// </summary>
        private void UseButton_Click(object sender, RoutedEventArgs e)
        {
            var modelId = (string)((Button)sender).Tag;
            // TODO: 切换到该模型
            MessageBox.Show($"已切换到模型: {modelId}", "提示", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 删除模型按钮点击
        /// </summary>
        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var modelId = (string)((Button)sender).Tag;
            
            var result = MessageBox.Show(
                $"确定要删除模型 {modelId} 吗？\n删除后需要重新下载才能使用。",
                "确认删除",
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
                MessageBox.Show("正在下载中，请等待完成", "提示", 
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
                MessageBox.Show($"未找到模型配置：{modelId}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            // 确认下载
            var confirmResult = MessageBox.Show(
                $"确定要下载 {model.Name} 吗？\n\n大小：{model.Size}\n\n下载完成后将自动尝试加载模型。",
                "确认下载",
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
                    DownloadStatusText.Text = "下载完成！";
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
                    DownloadStatusText.Text = "下载失败";
                    DownloadStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    MessageBox.Show($"下载失败：{result.Error}", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (OperationCanceledException)
            {
                DownloadStatusText.Text = "下载已取消";
                MessageBox.Show("下载已取消", "提示", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DownloadStatusText.Text = "下载失败";
                DownloadStatusText.Foreground = new SolidColorBrush(Colors.Red);
                MessageBox.Show($"下载异常：{ex.Message}", "错误", 
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
            Dispatcher.Invoke(() =>
            {
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

        /// <summary>
        /// 下载状态更新
        /// </summary>
        private void OnDownloadStatusChanged(object? sender, string status)
        {
            Dispatcher.Invoke(() =>
            {
                DownloadStatusText.Text = status;
                Logger.Log($"下载状态：{status}");
            });
        }

        /// <summary>
        /// 后台下载按钮点击（实际为取消）
        /// </summary>
        private void BackgroundDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading && _downloadCts != null)
            {
                _downloadCts.Cancel();
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
                    "下载正在进行中，关闭窗口不会中断下载。\n确定要关闭吗？",
                    "确认关闭",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }
            
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            
            // 取消下载但不等待
            if (_downloadCts != null && !_downloadCts.IsCancellationRequested)
            {
                _downloadCts.Cancel();
            }
        }
    }
}
