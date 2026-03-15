using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WordFlow.Infrastructure;
using WordFlow.Resources.Strings;
using WordFlow.Services;

namespace WordFlow.Views
{
    /// <summary>
    /// UpdateDialog.xaml 的交互逻辑
    /// </summary>
    public partial class UpdateDialog : LocalizedWindow
    {
        private readonly UpdateService _updateService;
        private readonly UpdateInfo _updateInfo;
        private string? _downloadedFilePath;
        private CancellationTokenSource? _downloadCts;
        private bool _isDownloading;
        private bool _isInstalling;

        public UpdateDialog(UpdateInfo updateInfo, UpdateService updateService)
        {
            InitializeComponent();
            _updateInfo = updateInfo;
            _updateService = updateService;
            
            InitializeDialog();
        }

        /// <summary>
        /// 初始化对话框
        /// </summary>
        private void InitializeDialog()
        {
            // 设置版本信息
            VersionText.Text = $"v{_updateInfo.Version}";
            ReleaseDateText.Text = $"{Strings.UpdateDialog_ReleaseDate}: {_updateInfo.ReleaseDate:yyyy-MM-dd}";
            
            // 加载变更列表
            LoadChanges();
            
            // 紧急更新提示
            if (_updateInfo.Urgent)
            {
                UrgentBorder.Visibility = Visibility.Visible;
                SkipButton.IsEnabled = false;
                SkipButton.ToolTip = Strings.UpdateDialog_SkipDisabledTooltip;
            }
            
            // 订阅更新服务事件
            _updateService.UpdateCheckFailed += OnUpdateCheckFailed;
        }

        /// <summary>
        /// 加载变更列表
        /// </summary>
        private void LoadChanges()
        {
            ChangesList.Children.Clear();
            
            if (_updateInfo.Changes == null || _updateInfo.Changes.Count == 0)
            {
                var noChangesText = new System.Windows.Controls.TextBlock
                {
                    Text = Strings.UpdateDialog_NoChanges,
                    FontSize = 13,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray)
                };
                ChangesList.Children.Add(noChangesText);
                return;
            }
            
            foreach (var change in _updateInfo.Changes)
            {
                var icon = change.Type switch
                {
                    "feature" => "✨",
                    "improvement" => "🔧",
                    "fix" => "🐛",
                    _ => "📝"
                };
                
                var changePanel = new System.Windows.Controls.StackPanel
                {
                    Margin = new Thickness(0, 0, 0, 10),
                    Orientation = System.Windows.Controls.Orientation.Horizontal
                };
                
                var iconText = new System.Windows.Controls.TextBlock
                {
                    Text = icon,
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                
                var contentPanel = new System.Windows.Controls.StackPanel();
                
                var titleText = new System.Windows.Controls.TextBlock
                {
                    Text = change.Title,
                    FontSize = 13,
                    FontWeight = System.Windows.FontWeights.SemiBold,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 33, 33))
                };
                
                var descText = new System.Windows.Controls.TextBlock
                {
                    Text = change.Description,
                    FontSize = 12,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(117, 117, 117)),
                    TextWrapping = System.Windows.TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 0)
                };
                
                contentPanel.Children.Add(titleText);
                contentPanel.Children.Add(descText);
                
                changePanel.Children.Add(iconText);
                changePanel.Children.Add(contentPanel);
                
                ChangesList.Children.Add(changePanel);
            }
        }

        /// <summary>
        /// 更新按钮点击 - 开始下载并安装
        /// </summary>
        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading || _isInstalling)
            {
                return;
            }

            UpdateButton.IsEnabled = false;
            SkipButton.IsEnabled = false;
            LaterButton.IsEnabled = false;
            
            // 显示进度区域
            ProgressBorder.Visibility = Visibility.Visible;
            
            try
            {
                _downloadCts = new CancellationTokenSource();
                _isDownloading = true;
                
                var progress = new Progress<DownloadProgress>(OnDownloadProgress);
                
                // 开始下载
                _downloadedFilePath = await _updateService.DownloadUpdateAsync(_updateInfo, progress, _downloadCts.Token);
                
                _isDownloading = false;
                
                // 验证下载
                ProgressText.Text = Strings.UpdateDialog_Verifying;
                
                var isValid = await _updateService.ValidatePackageAsync(_downloadedFilePath, _updateInfo.SHA256);
                
                if (!isValid)
                {
                    MessageBox.Show(
                        Strings.UpdateDialog_VerificationFailed,
                        Strings.UpdateDialog_Error,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    ResetButtons();
                    return;
                }
                
                // 确认安装
                var result = MessageBox.Show(
                    Strings.UpdateDialog_ReadyToInstall,
                    Strings.UpdateDialog_Install,
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.OK)
                {
                    _isInstalling = true;
                    ProgressText.Text = Strings.UpdateDialog_Installing;
                    
                    // 执行安装
                    var installed = await _updateService.InstallAsync(_downloadedFilePath);
                    
                    if (installed)
                    {
                        MessageBox.Show(
                            Strings.UpdateDialog_InstallSuccess,
                            Strings.UpdateDialog_Success,
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        MessageBox.Show(
                            Strings.UpdateDialog_InstallFailed,
                            Strings.UpdateDialog_Error,
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        ResetButtons();
                    }
                }
                else
                {
                    ResetButtons();
                }
            }
            catch (OperationCanceledException)
            {
                ProgressText.Text = Strings.UpdateDialog_Cancelled;
                ResetButtons();
            }
            catch (Exception ex)
            {
                ProgressText.Text = Strings.UpdateDialog_DownloadFailed;
                MessageBox.Show(
                    $"{Strings.UpdateDialog_Error}: {ex.Message}",
                    Strings.UpdateDialog_Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                ResetButtons();
            }
        }

        /// <summary>
        /// 下载进度更新
        /// </summary>
        private void OnDownloadProgress(DownloadProgress progress)
        {
            Dispatcher.Invoke(() =>
            {
                DownloadProgressBar.Value = progress.Percentage;
                ProgressText.Text = $"{progress.Percentage:F1}%";
                SpeedText.Text = progress.Speed;
                RemainingTimeText.Text = string.Format(
                    Strings.UpdateDialog_RemainingTime,
                    progress.RemainingTime.TotalSeconds < 60 
                        ? $"{(int)progress.RemainingTime.TotalSeconds}{Strings.UpdateDialog_Seconds}"
                        : $"{(int)progress.RemainingTime.TotalMinutes}分{(int)progress.RemainingTime.Seconds}秒");
            });
        }

        /// <summary>
        /// 跳过按钮点击
        /// </summary>
        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading || _isInstalling)
            {
                return;
            }
            
            _updateService.SkipVersion(_updateInfo.Version);
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 稍后按钮点击
        /// </summary>
        private void LaterButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading || _isInstalling)
            {
                return;
            }
            
            // 设置 24 小时后提醒
            _updateService.RemindLater(TimeSpan.FromHours(24));
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 更新检查失败处理
        /// </summary>
        private void OnUpdateCheckFailed(object? sender, string message)
        {
            // 静默处理，不干扰用户
        }

        /// <summary>
        /// 重置按钮状态
        /// </summary>
        private void ResetButtons()
        {
            Dispatcher.Invoke(() =>
            {
                UpdateButton.IsEnabled = true;
                SkipButton.IsEnabled = !_updateInfo.Urgent;
                LaterButton.IsEnabled = true;
            });
            _isDownloading = false;
            _isInstalling = false;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            // 取消订阅
            _updateService.UpdateCheckFailed -= OnUpdateCheckFailed;
            
            // 取消下载
            if (_isDownloading && _downloadCts != null)
            {
                _downloadCts.Cancel();
            }
        }
    }
}
