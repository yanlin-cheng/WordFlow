using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using WordFlow.Services;
using WordFlow.Utils;

namespace WordFlow.Views
{
    /// <summary>
    /// 模型下载对话框 - 首次使用时下载语音识别模型
    /// </summary>
    public partial class ModelDownloadDialog : Window
    {
        private readonly FirstRunService _firstRunService;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _downloadCompleted = false;

        public ModelDownloadDialog()
        {
            InitializeComponent();
            _firstRunService = new FirstRunService();
        }

        /// <summary>
        /// 窗口加载时检查是否需要下载
        /// </summary>
        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            
            // 如果不需要首次设置，直接关闭
            if (!_firstRunService.NeedsFirstRunSetup())
            {
                DialogResult = true;
                Close();
                return;
            }
        }

        /// <summary>
        /// 立即下载按钮点击
        /// </summary>
        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            // 禁用按钮，显示进度
            DownloadButton.Visibility = Visibility.Collapsed;
            SkipButton.Visibility = Visibility.Collapsed;
            RetryButton.Visibility = Visibility.Collapsed;
            ProgressGrid.Visibility = Visibility.Visible;
            ErrorText.Visibility = Visibility.Collapsed;

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                // 创建进度报告器
                var progress = new Progress<DownloadProgressEventArgs>(args =>
                {
                    // 更新 UI（在 UI 线程执行）
                    Dispatcher.Invoke(() =>
                    {
                        DownloadProgressBar.Value = args.ProgressPercentage;
                        ProgressText.Text = $"{args.ProgressPercentage:F1}%";
                        
                        // 格式化状态文本
                        if (args.BytesReceived > 0 && args.TotalBytes > 0)
                        {
                            var receivedMB = args.BytesReceived / (1024.0 * 1024.0);
                            var totalMB = args.TotalBytes / (1024.0 * 1024.0);
                            StatusText.Text = $"已下载 {receivedMB:F1} MB / {totalMB:F1} MB";
                        }

                        // 格式化速度和剩余时间
                        if (args.Speed > 0)
                        {
                            var speedKB = args.Speed / 1024.0;
                            var timeText = args.RemainingTime.TotalSeconds > 0 
                                ? $"，剩余约 {args.RemainingTime:mm\\:ss}" 
                                : "";
                            SpeedText.Text = $"速度: {speedKB:F1} KB/s{timeText}";
                        }
                    });
                });

                // 开始下载
                StatusText.Text = "正在连接服务器...";
                var result = await _firstRunService.DownloadDefaultModelAsync(
                    progress, 
                    _cancellationTokenSource.Token);

                if (result.Success)
                {
                    // 下载成功
                    _downloadCompleted = true;
                    StatusText.Text = "下载完成！";
                    StatusText.Foreground = new SolidColorBrush(Colors.Green);
                    ProgressText.Text = "100%";
                    SpeedText.Text = "";
                    
                    // 显示继续按钮
                    ContinueButton.Visibility = Visibility.Visible;
                    
                    Logger.Log("ModelDownloadDialog: 模型下载成功");
                }
                else
                {
                    // 下载失败
                    ShowError($"下载失败: {result.Error}");
                    Logger.Log($"ModelDownloadDialog: 模型下载失败 - {result.Error}");
                }
            }
            catch (OperationCanceledException)
            {
                // 用户取消
                StatusText.Text = "下载已取消";
                ShowError("下载已取消，您可以稍后重新下载。");
                Logger.Log("ModelDownloadDialog: 用户取消下载");
            }
            catch (Exception ex)
            {
                // 其他错误
                ShowError($"发生错误: {ex.Message}");
                Logger.Log($"ModelDownloadDialog: 下载异常 - {ex.Message}");
            }
        }

        /// <summary>
        /// 跳过按钮点击
        /// </summary>
        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            // 取消下载（如果有）
            _cancellationTokenSource?.Cancel();

            // 询问用户是否确定跳过
            var result = MessageBox.Show(
                "跳过下载后，语音识别功能将无法使用。\n\n您可以在设置中随时下载模型。",
                "确认跳过",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // 标记首次运行完成（但不下载模型）
                _firstRunService.MarkFirstRunCompleted();
                
                DialogResult = false;
                Close();
            }
        }

        /// <summary>
        /// 继续使用按钮点击
        /// </summary>
        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 窗口关闭时取消下载
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            
            // 如果正在下载，询问是否取消
            if (_cancellationTokenSource != null && 
                !_cancellationTokenSource.IsCancellationRequested && 
                !_downloadCompleted)
            {
                var result = MessageBox.Show(
                    "下载正在进行中，确定要取消吗？",
                    "确认取消",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                _cancellationTokenSource.Cancel();
            }
        }

        /// <summary>
        /// 显示错误信息
        /// </summary>
        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
            RetryButton.Visibility = Visibility.Visible;
            SkipButton.Visibility = Visibility.Visible;
            
            StatusText.Text = "下载失败";
            StatusText.Foreground = new SolidColorBrush(Colors.Red);
        }
    }
}
