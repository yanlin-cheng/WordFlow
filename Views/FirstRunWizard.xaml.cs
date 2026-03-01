using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using WordFlow.Services;
using WordFlow.Utils;

namespace WordFlow.Views
{
    /// <summary>
    /// 首次运行向导 - 环境检测与模型下载
    /// </summary>
    public partial class FirstRunWizard : Window
    {
        private readonly ModelDownloadService _downloadService;
        private List<ModelInfo> _models = new();
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isDownloading;
        private bool _downloadCompleted;
        private bool _dotNetInstalled;
        private bool _pythonReady;

        /// <summary>
        /// 下载是否成功完成
        /// </summary>
        public bool DownloadCompleted => _downloadCompleted;

        public FirstRunWizard()
        {
            InitializeComponent();
            
            _downloadService = new ModelDownloadService();
            _downloadService.ProgressChanged += OnDownloadProgress;
            _downloadService.StatusChanged += OnDownloadStatus;
            
            Loaded += FirstRunWizard_Loaded;
        }

        #region 初始化与环境检测

        private async void FirstRunWizard_Loaded(object sender, RoutedEventArgs e)
        {
            await CheckEnvironmentAsync();
        }

        /// <summary>
        /// 检查环境
        /// </summary>
        private async Task CheckEnvironmentAsync()
        {
            // 检测 .NET 环境
            await CheckDotNetAsync();
            
            // 检测 Python 环境
            CheckPython();
            
            // 更新 UI
            UpdateEnvironmentUI();
            
            // 如果环境就绪，加载模型列表
            if (_dotNetInstalled && _pythonReady)
            {
                await LoadModelsAsync();
            }
        }

        /// <summary>
        /// 检测 .NET 运行时
        /// </summary>
        private async Task CheckDotNetAsync()
        {
            DotNetStatusText.Text = "正在检测 .NET 环境...";
            DotNetStatusIcon.Foreground = Brushes.Orange;
            
            await Task.Run(() =>
            {
                _dotNetInstalled = IsDotNet8Installed();
            });
            
            if (_dotNetInstalled)
            {
                DotNetStatusText.Text = ".NET 8.0 运行时：已安装";
                DotNetStatusIcon.Foreground = Brushes.Green;
                EnvironmentProgressBar.Visibility = Visibility.Collapsed;
            }
            else
            {
                DotNetStatusText.Text = ".NET 8.0 运行时：未安装";
                DotNetStatusIcon.Foreground = Brushes.Red;
                InstallDotNetButton.Visibility = Visibility.Visible;
                EnvironmentProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 检查 .NET 8.0 是否已安装
        /// </summary>
        private bool IsDotNet8Installed()
        {
            try
            {
                // 方法 1：检查注册表
                var registryPath = @"SOFTWARE\dotnet\Setup\InstalledVersions\x64";
                var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryPath);
                if (key != null)
                {
                    var version = key.GetValue("Version")?.ToString();
                    if (!string.IsNullOrEmpty(version) && version.StartsWith("8.0"))
                    {
                        return true;
                    }
                }
                
                // 方法 2：检查 dotnet 命令
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                if (process.Start())
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    return output.StartsWith("8.0");
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检测 Python 环境
        /// </summary>
        private void CheckPython()
        {
            var pythonPath = Path.Combine(AppContext.BaseDirectory, "PythonASR", "python", "python.exe");
            
            if (File.Exists(pythonPath))
            {
                PythonStatusText.Text = "Python 环境：已就绪";
                PythonStatusIcon.Foreground = Brushes.Green;
                _pythonReady = true;
            }
            else
            {
                // 尝试其他可能的位置
                var alternatePaths = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "..", "PythonASR", "python", "python.exe"),
                    Path.Combine(AppContext.BaseDirectory, "PythonASR", "venv", "Scripts", "python.exe"),
                };
                
                foreach (var path in alternatePaths)
                {
                    if (File.Exists(path))
                    {
                        PythonStatusText.Text = "Python 环境：已就绪";
                        PythonStatusIcon.Foreground = Brushes.Green;
                        _pythonReady = true;
                        return;
                    }
                }
                
                PythonStatusText.Text = "Python 环境：未找到";
                PythonStatusIcon.Foreground = Brushes.Red;
                _pythonReady = false;
            }
        }

        /// <summary>
        /// 更新环境检测 UI
        /// </summary>
        private void UpdateEnvironmentUI()
        {
            if (_dotNetInstalled && _pythonReady)
            {
                // 环境就绪，更新步骤指示器
                Step1Indicator.Background = Brushes.Green;
                Step2Indicator.Background = Brushes.Green;
                
                // 启用模型选择
                ModelGroupBox.IsEnabled = true;
                ModelDescriptionText.Text = "请选择一个模型";
            }
            else if (_dotNetInstalled)
            {
                Step1Indicator.Background = Brushes.Green;
                ModelDescriptionText.Text = "Python 环境未就绪，请检查安装包完整性";
            }
            else
            {
                Step1Indicator.Background = Brushes.Red;
                ModelDescriptionText.Text = "请先安装 .NET 8.0 运行时";
            }
        }

        #endregion

        #region .NET 安装

        private void InstallDotNetButton_Click(object sender, RoutedEventArgs e)
        {
            InstallDotNetButton.IsEnabled = false;
            DotNetStatusText.Text = "正在下载 .NET 8.0 运行时...";
            EnvironmentProgressBar.Visibility = Visibility.Visible;
            EnvironmentProgressBar.IsIndeterminate = false;
            
            _ = DownloadAndInstallDotNetAsync();
        }

        /// <summary>
        /// 下载并安装 .NET 8.0 运行时
        /// </summary>
        private async Task DownloadAndInstallDotNetAsync()
        {
            try
            {
                // .NET 8.0 Runtime 下载链接（官方 CDN）
                var downloadUrl = "https://download.visualstudio.microsoft.com/download/pr/3f5b8c62-8d2c-4f3e-9c3a-8b7e6d5f4e3d/2e8b7c6d5f4e3d2c1b0a9f8e7d6c5b4a/dotnet-runtime-8.0.0-win-x64.exe";
                
                // 备用链接（.NET 官方）
                var backupUrl = "https://dotnet.microsoft.com/download/dotnet/thankyou/runtime-8.0.0-windows-x64-installer";
                
                var tempPath = Path.Combine(Path.GetTempPath(), "dotnet-runtime-8.0.0-win-x64.exe");
                
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(30);
                
                // 下载文件
                using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var bytesReceived = 0L;
                
                using var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = File.Create(tempPath);
                
                var buffer = new byte[81920];
                var stopwatch = Stopwatch.StartNew();
                var lastProgressTime = DateTime.Now;
                
                while (true)
                {
                    _cancellationTokenSource?.Token.ThrowIfCancellationRequested();
                    
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;
                    
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    bytesReceived += bytesRead;
                    
                    // 更新进度（每秒更新一次）
                    var now = DateTime.Now;
                    if ((now - lastProgressTime).TotalSeconds >= 1 || bytesReceived >= totalBytes)
                    {
                        var progress = totalBytes > 0 ? (bytesReceived * 100.0 / totalBytes) : 0;
                        var elapsed = (now - lastProgressTime).TotalSeconds;
                        var speed = elapsed > 0 ? bytesReceived / elapsed : 0;
                        var remaining = totalBytes > bytesReceived && speed > 0 
                            ? TimeSpan.FromSeconds((totalBytes - bytesReceived) / speed) 
                            : TimeSpan.Zero;
                        
                        Dispatcher.Invoke(() =>
                        {
                            EnvironmentProgressBar.Value = progress;
                            DotNetStatusText.Text = $".NET 下载中：{progress:F1}% - {FormatBytes(bytesReceived)} / {FormatBytes(totalBytes)} - {FormatBytes((long)speed)}/s - 剩余：{remaining:mm\\:ss}";
                        });
                        
                        lastProgressTime = now;
                        bytesReceived = 0;
                    }
                }
                
                stopwatch.Stop();
                
                // 下载完成，运行安装程序
                Dispatcher.Invoke(() =>
                {
                    DotNetStatusText.Text = "正在安装 .NET 8.0 运行时...";
                });
                
                var installProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = tempPath,
                        Arguments = "/install /passive /norestart",
                        UseShellExecute = true
                    }
                };
                
                installProcess.Start();
                await Task.Run(() => installProcess.WaitForExit());
                
                // 清理临时文件
                try
                {
                    File.Delete(tempPath);
                }
                catch { }
                
                // 安装完成
                Dispatcher.Invoke(() =>
                {
                    DotNetStatusText.Text = ".NET 8.0 运行时：安装完成";
                    DotNetStatusIcon.Foreground = Brushes.Green;
                    EnvironmentProgressBar.Visibility = Visibility.Collapsed;
                    InstallDotNetButton.Visibility = Visibility.Collapsed;
                    _dotNetInstalled = true;
                    UpdateEnvironmentUI();
                    
                    if (_pythonReady)
                    {
                        _ = LoadModelsAsync();
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    DotNetStatusText.Text = $".NET 安装失败：{ex.Message}";
                    DotNetStatusIcon.Foreground = Brushes.Red;
                    EnvironmentProgressBar.Visibility = Visibility.Collapsed;
                    InstallDotNetButton.IsEnabled = true;
                });
            }
        }

        #endregion

        #region 加载模型列表

        private async Task LoadModelsAsync()
        {
            try
            {
                StatusText.Text = "正在加载模型列表...";
                
                _models = await _downloadService.GetAvailableModelsAsync();
                
                if (_models.Count == 0)
                {
                    StatusText.Text = "无法加载模型列表，请检查网络连接后重试。";
                    DownloadButton.IsEnabled = false;
                    return;
                }
                
                // 填充模型下拉框
                foreach (var model in _models)
                {
                    var defaultMark = model.Default ? " [推荐]" : "";
                    ModelComboBox.Items.Add($"{model.Name} ({model.Size}){defaultMark}");
                }
                
                // 默认选中第一个推荐模型
                var defaultIndex = _models.FindIndex(m => m.Default);
                ModelComboBox.SelectedIndex = defaultIndex >= 0 ? defaultIndex : 0;
                
                UpdateModelDescription();
                StatusText.Text = "点击\"开始下载\"按钮开始下载模型";
            }
            catch (Exception ex)
            {
                Logger.Log($"加载模型列表失败：{ex.Message}");
                StatusText.Text = $"加载模型列表失败：{ex.Message}";
                DownloadButton.IsEnabled = false;
            }
        }

        private void ModelComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateModelDescription();
        }

        private void UpdateModelDescription()
        {
            if (ModelComboBox.SelectedIndex >= 0 && ModelComboBox.SelectedIndex < _models.Count)
            {
                var model = _models[ModelComboBox.SelectedIndex];
                ModelDescriptionText.Text = model.Description;
            }
        }

        #endregion

        #region 下载控制

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading)
            {
                CancelDownload();
                return;
            }
            
            await StartDownloadAsync();
        }

        private async Task StartDownloadAsync()
        {
            if (ModelComboBox.SelectedIndex < 0 || ModelComboBox.SelectedIndex >= _models.Count)
            {
                MessageBox.Show("请先选择一个模型", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            var model = _models[ModelComboBox.SelectedIndex];
            var useMirror = UseMirrorCheckBox.IsChecked ?? true;
            
            // 确认对话框
            var result = MessageBox.Show(
                $"即将下载模型：{model.Name}（{model.Size}）\n\n" +
                "下载可能需要几分钟时间，请确保网络连接稳定。\n\n" +
                "是否继续？",
                "确认下载",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (result != MessageBoxResult.Yes)
                return;
            
            _isDownloading = true;
            _cancellationTokenSource = new CancellationTokenSource();
            
            // 更新 UI 状态
            DownloadButton.Content = "取消下载";
            CancelButton.IsEnabled = false;
            ModelComboBox.IsEnabled = false;
            UseMirrorCheckBox.IsEnabled = false;
            
            // 更新步骤指示器
            Step2Indicator.Background = Brushes.Green;
            
            try
            {
                var downloadResult = await _downloadService.DownloadModelAsync(
                    model, 
                    useMirror, 
                    _cancellationTokenSource.Token);
                
                if (downloadResult.Success)
                {
                    _downloadCompleted = true;
                    StatusText.Text = "模型下载安装成功！点击\"完成\"继续。";
                    
                    // 更新按钮状态
                    DownloadButton.Content = "完成";
                    DownloadButton.IsEnabled = true;
                    CancelButton.Content = "完成";
                    CancelButton.IsEnabled = true;
                }
                else
                {
                    StatusText.Text = $"下载失败：{downloadResult.Error}";
                    ResetDownloadUI();
                }
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "下载已取消";
                ResetDownloadUI();
            }
            catch (Exception ex)
            {
                Logger.Log($"下载异常：{ex.Message}");
                StatusText.Text = $"下载异常：{ex.Message}";
                ResetDownloadUI();
            }
        }

        private void CancelDownload()
        {
            _cancellationTokenSource?.Cancel();
            StatusText.Text = "正在取消下载...";
        }

        private void ResetDownloadUI()
        {
            _isDownloading = false;
            DownloadButton.Content = "开始下载";
            DownloadButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            CancelButton.Content = "取消";
            ModelComboBox.IsEnabled = true;
            UseMirrorCheckBox.IsEnabled = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_downloadCompleted)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                var result = MessageBox.Show(
                    "尚未下载模型，无法使用语音识别功能。\n\n确定要退出吗？",
                    "确认退出",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    DialogResult = false;
                    Close();
                }
            }
        }

        #endregion

        #region 进度更新

        private void OnDownloadProgress(object? sender, DownloadProgressEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                DownloadProgressBar.Value = e.ProgressPercentage;
                ProgressPercentText.Text = $"{e.ProgressPercentage:F1}%";
                ProgressSizeText.Text = $"{FormatBytes(e.BytesReceived)} / {FormatBytes(e.TotalBytes)}";
                ProgressSpeedText.Text = $"{FormatBytes((long)e.Speed)}/s";
                ProgressTimeText.Text = e.RemainingTime.TotalSeconds > 0 
                    ? $"剩余：{e.RemainingTime:mm\\:ss}" 
                    : "";
            });
        }

        private void OnDownloadStatus(object? sender, string status)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = status;
            });
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }

        #endregion

        #region 窗口关闭

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (_isDownloading)
            {
                var result = MessageBox.Show(
                    "正在下载模型，确定要取消吗？\n\n已下载的部分将保留，下次可继续下载。",
                    "确认退出",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _cancellationTokenSource?.Cancel();
                }
                else
                {
                    e.Cancel = true;
                }
            }
            else if (!_downloadCompleted)
            {
                var result = MessageBox.Show(
                    "尚未下载模型，无法使用语音识别功能。\n\n确定要退出吗？",
                    "确认退出",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                }
            }
        }

        #endregion
    }
}
