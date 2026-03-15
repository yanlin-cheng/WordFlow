using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WordFlow.Infrastructure;
using WordFlow.Services;
using WordFlow.Resources.Strings;

namespace WordFlow.Views
{
    /// <summary>
    /// 首次运行向导 - 卡片式滚动展示
    /// </summary>
    public partial class FirstRunWizard : LocalizedWindow
    {
        private readonly ModelDownloadService _downloadService;
        private int _currentCardIndex = 1; // 1-7 为功能卡片，8 为完成页面
        private bool _modelDownloaded = false;
        
        // 后台下载相关
        private CancellationTokenSource? _downloadCts;
        private bool _isDownloading = false;
        private bool _downloadCompleted = false;
        
        // 卡片总数（不包括完成页面）
        // 新的卡片顺序：1=欢迎，2=语音输入，3=下载模型 (提示), 4=热键设置，5=设置方法，6=个人词库，7=模型管理器
        private const int FeatureCardCount = 7;
        private const int CompleteCardIndex = 8;
        private const int ModelManagerCardIndex = 7; // 模型管理器卡片索引（最后一步）
        private const int DownloadHintCardIndex = 3; // 下载模型提示卡片索引

        /// <summary>
        /// 下载是否成功完成
        /// </summary>
        public bool DownloadCompleted => _modelDownloaded || _downloadCompleted;

        public FirstRunWizard()
        {
            InitializeComponent();
            
            _downloadService = new ModelDownloadService();
            
            Loaded += FirstRunWizard_Loaded;
            Closing += FirstRunWizard_Closing;
        }

        #region 窗口生命周期

        private async void FirstRunWizard_Loaded(object sender, RoutedEventArgs e)
        {
            await CheckModelStatusAsync();
            InitializeCardIndicators();
            UpdateCardDisplay();
        }

        private async void FirstRunWizard_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 如果正在下载，取消下载
            if (_isDownloading)
            {
                _downloadCts?.Cancel();
            }
            
            // 等待下载任务完成
            if (_downloadCts != null)
            {
                try
                {
                    await Task.Delay(100);
                }
                catch { }
            }
        }

        #endregion

        #region 模型状态检查

        /// <summary>
        /// 检查模型状态
        /// </summary>
        private async Task CheckModelStatusAsync()
        {
            try
            {
                var needsSetup = await _downloadService.NeedsFirstRunSetupAsync();
                _modelDownloaded = !needsSetup;
                
                if (_modelDownloaded && ModelStatusText != null)
                {
                    ModelStatusText.Text = Strings.Wizard_Complete_ModelStatus_Installed;
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Log($"检查模型状态失败：{ex.Message}");
            }
        }

        #endregion

        #region 卡片指示器

        /// <summary>
        /// 初始化卡片指示器（小圆点）
        /// </summary>
        private void InitializeCardIndicators()
        {
            CardIndicators.Children.Clear();
            
            for (int i = 1; i <= FeatureCardCount; i++)
            {
                var ellipse = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Margin = new Thickness(5, 0, 5, 0),
                    Fill = i == _currentCardIndex ? Brushes.Blue : Brushes.LightGray
                };
                
                // 存储索引信息（实际索引）
                ellipse.Tag = i;
                
                // 点击切换卡片
                ellipse.MouseLeftButtonUp += (s, e) =>
                {
                    if (s is Ellipse ell && ell.Tag is int index)
                    {
                        _currentCardIndex = index;
                        UpdateCardDisplay();
                    }
                };
                
                // 添加鼠标悬停效果
                ellipse.Cursor = Cursors.Hand;
                
                CardIndicators.Children.Add(ellipse);
            }
        }

        /// <summary>
        /// 更新卡片指示器状态
        /// </summary>
        private void UpdateCardIndicators()
        {
            for (int i = 0; i < CardIndicators.Children.Count; i++)
            {
                if (CardIndicators.Children[i] is Ellipse ellipse)
                {
                    ellipse.Fill = i == _currentCardIndex ? Brushes.Blue : Brushes.LightGray;
                }
            }
        }

        #endregion

        #region 卡片显示控制

        /// <summary>
        /// 更新卡片显示
        /// </summary>
        private void UpdateCardDisplay()
        {
            // 隐藏所有卡片
            Card1.Visibility = Visibility.Collapsed;
            Card2.Visibility = Visibility.Collapsed;
            Card3.Visibility = Visibility.Collapsed;
            Card4.Visibility = Visibility.Collapsed;
            Card5.Visibility = Visibility.Collapsed;
            Card6.Visibility = Visibility.Collapsed;
            Card7.Visibility = Visibility.Collapsed;
            CompleteCard.Visibility = Visibility.Collapsed;

            // 显示当前卡片
            // 新顺序：1=欢迎，2=语音输入，3=下载模型 (提示), 4=热键设置，5=设置方法，6=个人词库，7=模型管理器
            switch (_currentCardIndex)
            {
                case 1:
                    Card1.Visibility = Visibility.Visible;
                    NextButton.Content = Strings.Wizard_NextButton;
                    break;
                case 2:
                    Card2.Visibility = Visibility.Visible;
                    NextButton.Content = Strings.Wizard_NextButton;
                    break;
                case DownloadHintCardIndex:
                    Card3.Visibility = Visibility.Visible;
                    NextButton.Content = Strings.Wizard_NextButton;
                    break;
                case 4:
                    Card4.Visibility = Visibility.Visible;
                    NextButton.Content = Strings.Wizard_NextButton;
                    break;
                case 5:
                    Card5.Visibility = Visibility.Visible;
                    NextButton.Content = Strings.Wizard_NextButton;
                    break;
                case 6:
                    Card6.Visibility = Visibility.Visible;
                    NextButton.Content = Strings.Wizard_NextButton;
                    break;
                case ModelManagerCardIndex:
                    Card7.Visibility = Visibility.Visible;
                    // 模型管理器卡片 - 显示下一步按钮，但根据下载状态控制是否可用
                    NextButton.Visibility = Visibility.Visible;
                    NextButton.Content = Strings.Wizard_NextButton;
                    
                    // 如果模型已下载或下载完成，允许进入完成页面
                    if (_modelDownloaded || _downloadCompleted)
                    {
                        NextButton.IsEnabled = true;
                        ModelManagerButton.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        // 未下载时，下一步按钮禁用，显示打开模型管理按钮
                        NextButton.IsEnabled = false;
                        ModelManagerButton.Visibility = Visibility.Visible;
                    }
                    break;
                case CompleteCardIndex:
                    CompleteCard.Visibility = Visibility.Visible;
                    NextButton.Content = Strings.Wizard_StartButton;
                    break;
            }

            // 更新按钮状态
            BackButton.Visibility = _currentCardIndex > 1 ? Visibility.Visible : Visibility.Collapsed;
            
            // 全局按钮可见性控制
            if (_currentCardIndex == ModelManagerCardIndex && !(_modelDownloaded || _downloadCompleted))
            {
                NextButton.Visibility = Visibility.Visible;
                NextButton.IsEnabled = false; // 禁用下一步，直到下载完成
                ModelManagerButton.Visibility = Visibility.Visible;
            }
            else if (_currentCardIndex == ModelManagerCardIndex)
            {
                NextButton.Visibility = Visibility.Visible;
                NextButton.IsEnabled = true;
                ModelManagerButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                NextButton.Visibility = Visibility.Visible;
                NextButton.IsEnabled = true;
                ModelManagerButton.Visibility = Visibility.Collapsed;
            }
            
            // 更新指示器（完成页面不显示指示器）
            if (_currentCardIndex < FeatureCardCount)
            {
                CardIndicators.Visibility = Visibility.Visible;
                UpdateCardIndicators();
            }
            else
            {
                CardIndicators.Visibility = Visibility.Collapsed;
            }
            
            // 更新下载面板 UI 状态
            UpdateDownloadPanel();
        }

        /// <summary>
        /// 更新下载面板 UI 状态
        /// </summary>
        private void UpdateDownloadPanel()
        {
            if (_modelDownloaded)
            {
                // 模型已下载，显示完成状态
                DownloadStatusText.Visibility = Visibility.Collapsed;
                DownloadProgressBar.Visibility = Visibility.Collapsed;
                DownloadProgressText.Visibility = Visibility.Collapsed;
                BackgroundDownloadButton.Visibility = Visibility.Collapsed;
                CancelDownloadButton.Visibility = Visibility.Collapsed;
                ProgressHintText.Visibility = Visibility.Collapsed;
                ClickNextHint.Visibility = Visibility.Collapsed;
                DownloadCompleteText.Visibility = Visibility.Visible;
            }
            else if (_isDownloading)
            {
                // 下载中
                DownloadStatusText.Visibility = Visibility.Visible;
                DownloadProgressBar.Visibility = Visibility.Visible;
                DownloadProgressText.Visibility = Visibility.Visible;
                BackgroundDownloadButton.Visibility = Visibility.Collapsed;
                CancelDownloadButton.Visibility = Visibility.Visible;
                ProgressHintText.Visibility = Visibility.Visible;
                ClickNextHint.Visibility = Visibility.Collapsed;
                DownloadCompleteText.Visibility = Visibility.Collapsed;
            }
            else if (_downloadCompleted)
            {
                // 下载完成（后台下载完成）
                DownloadStatusText.Visibility = Visibility.Collapsed;
                DownloadProgressBar.Visibility = Visibility.Collapsed;
                DownloadProgressText.Visibility = Visibility.Collapsed;
                BackgroundDownloadButton.Visibility = Visibility.Collapsed;
                CancelDownloadButton.Visibility = Visibility.Collapsed;
                ProgressHintText.Visibility = Visibility.Collapsed;
                ClickNextHint.Visibility = Visibility.Visible;
                DownloadCompleteText.Visibility = Visibility.Visible;
            }
            else
            {
                // 初始状态 - 等待用户点击下载
                DownloadStatusText.Visibility = Visibility.Collapsed;
                DownloadProgressBar.Visibility = Visibility.Collapsed;
                DownloadProgressText.Visibility = Visibility.Collapsed;
                BackgroundDownloadButton.Visibility = Visibility.Visible;
                CancelDownloadButton.Visibility = Visibility.Collapsed;
                ProgressHintText.Visibility = Visibility.Collapsed;
                ClickNextHint.Visibility = Visibility.Collapsed;
                DownloadCompleteText.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region 导航控制

        /// <summary>
        /// 导航控制
        /// </summary>
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            // 如果模型未下载且未完成后台下载，提示用户
            if (_currentCardIndex == ModelManagerCardIndex && !(_modelDownloaded || _downloadCompleted))
            {
                MessageBox.Show(
                    Strings.Wizard_WaitDownloadComplete,
                    Strings.Wizard_WarningTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            
            if (_currentCardIndex == ModelManagerCardIndex && (_modelDownloaded || _downloadCompleted))
            {
                // 模型已下载，进入完成页面
                _currentCardIndex = CompleteCardIndex;
                UpdateCardDisplay();
            }
            else if (_currentCardIndex < CompleteCardIndex)
            {
                _currentCardIndex++;
                UpdateCardDisplay();
                
                // 如果到达模型管理器卡片且已有模型，直接跳到完成页面
                if (_currentCardIndex == ModelManagerCardIndex && _modelDownloaded)
                {
                    _currentCardIndex = CompleteCardIndex;
                    UpdateCardDisplay();
                }
            }
            else
            {
                // 完成
                DialogResult = true;
                Close();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentCardIndex > 1)
            {
                _currentCardIndex--;
                UpdateCardDisplay();
            }
        }

        /// <summary>
        /// 滚轮翻页支持
        /// </summary>
        private void ContentArea_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 向下滚动（Delta < 0）- 下一页
            if (e.Delta < 0)
            {
                // 在模型管理器卡片且未完成下载时，禁止滚轮跳转
                if (_currentCardIndex == ModelManagerCardIndex && !(_modelDownloaded || _downloadCompleted))
                {
                    return;
                }
                
                if (_currentCardIndex < CompleteCardIndex)
                {
                    _currentCardIndex++;
                    UpdateCardDisplay();
                }
            }
            // 向上滚动（Delta > 0）- 上一页
            else if (e.Delta > 0)
            {
                if (_currentCardIndex > 1)
                {
                    _currentCardIndex--;
                    UpdateCardDisplay();
                }
            }
            
            e.Handled = true;
        }

        #endregion

        #region 后台下载

        /// <summary>
        /// 后台下载按钮点击
        /// </summary>
        private async void BackgroundDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            await StartBackgroundDownloadAsync();
        }

        /// <summary>
        /// 取消下载按钮点击
        /// </summary>
        private void CancelDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            CancelDownload();
        }

        /// <summary>
        /// 开始后台下载
        /// </summary>
        private async Task StartBackgroundDownloadAsync()
        {
            if (_isDownloading) return;
            
            _downloadCts = new CancellationTokenSource();
            _isDownloading = true;
            _downloadCompleted = false;
            
            UpdateDownloadPanel();
            UpdateCardDisplay();
            
            try
            {
                // 获取默认模型
                var defaultModel = await _downloadService.GetDefaultModelAsync();
                if (defaultModel == null)
                {
                    MessageBox.Show(
                        Strings.ModelManager_NoModelsAvailable,
                        Strings.Wizard_WarningTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    
                    _isDownloading = false;
                    UpdateDownloadPanel();
                    UpdateCardDisplay();
                    return;
                }
                
                Utils.Logger.Log($"开始下载模型：{defaultModel.Name}");
                Utils.Logger.Log($"模型 ID: {defaultModel.Id}");
                
                // 注册进度事件（在开始下载前注册）
                _downloadService.ProgressChanged += (s, args) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        DownloadProgressBar.Value = args.ProgressPercentage;
                        DownloadProgressText.Text = $"{args.ProgressPercentage:F1}% - {args.Speed / 1024:F0} KB/s";
                    });
                };
                
                _downloadService.StatusChanged += (s, status) =>
                {
                    Utils.Logger.Log($"下载状态：{status}");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        DownloadStatusText.Text = status;
                        DownloadStatusText.Visibility = Visibility.Visible;
                    });
                };
                
                // 使用 ModelDownloadService 下载模型
                var result = await _downloadService.DownloadModelAsync(defaultModel, false, _downloadCts.Token);
                
                if (result.Success)
                {
                    _downloadCompleted = true;
                    _modelDownloaded = true;
                    
                    Utils.Logger.Log($"下载成功：{result.ModelPath}");
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            "模型下载成功！\n\n" + result.ModelPath,
                            Strings.Wizard_DownloadComplete,
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        
                        UpdateDownloadPanel();
                        UpdateCardDisplay();
                    });
                }
                else
                {
                    Utils.Logger.Log($"下载失败：{result.Error}");
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        string errorMsg = result.Error ?? "未知错误";
                        if (errorMsg.Contains("网络") || errorMsg.Contains("connection") || errorMsg.Contains("timeout"))
                        {
                            errorMsg += "\n\n请检查网络连接，或尝试在模型管理窗口手动下载。";
                        }
                        
                        MessageBox.Show(
                            $"下载失败：{errorMsg}",
                            Strings.Wizard_WarningTitle,
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        
                        _isDownloading = false;
                        UpdateDownloadPanel();
                        UpdateCardDisplay();
                    });
                }
            }
            catch (OperationCanceledException)
            {
                Utils.Logger.Log("下载被取消");
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        Strings.Wizard_DownloadCancelled,
                        Strings.Wizard_WarningTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    
                    _isDownloading = false;
                    UpdateDownloadPanel();
                    UpdateCardDisplay();
                });
            }
            catch (HttpRequestException httpEx)
            {
                Utils.Logger.Log($"HTTP 请求失败：{httpEx.Message}");
                Utils.Logger.Log($"堆栈跟踪：{httpEx.StackTrace}");
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"网络连接失败：{httpEx.Message}\n\n" +
                        $"可能原因：\n" +
                        $"1. 网络连接不稳定\n" +
                        $"2. 下载服务器暂时不可用\n" +
                        $"3. 防火墙阻止了下载\n\n" +
                        $"建议：\n" +
                        $"- 点击底部「打开模型管理」按钮\n" +
                        $"- 在模型管理窗口尝试手动下载\n" +
                        $"- 或手动下载模型文件到 PythonASR/models 目录",
                        "网络连接失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    
                    _isDownloading = false;
                    UpdateDownloadPanel();
                    UpdateCardDisplay();
                });
            }
            catch (Exception ex)
            {
                Utils.Logger.Log($"后台下载失败：{ex.Message}");
                Utils.Logger.Log($"堆栈跟踪：{ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Utils.Logger.Log($"内部异常：{ex.InnerException.Message}");
                }
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"下载失败：{ex.Message}\n\n" +
                        $"请检查网络连接，或尝试在模型管理窗口手动下载。\n\n" +
                        $"详细信息已记录到日志。",
                        Strings.Wizard_WarningTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    
                    _isDownloading = false;
                    UpdateDownloadPanel();
                    UpdateCardDisplay();
                });
            }
        }

        /// <summary>
        /// 取消下载
        /// </summary>
        private void CancelDownload()
        {
            if (_isDownloading)
            {
                _downloadCts?.Cancel();
                _isDownloading = false;
                UpdateDownloadPanel();
                UpdateCardDisplay();
            }
        }

        #endregion

        #region 打开模型管理窗口

        /// <summary>
        /// 打开模型管理窗口
        /// </summary>
        private void OpenModelManager()
        {
            var modelManager = new ModelManagerWindow();
            modelManager.Owner = this;
            
            // 模态显示，等待用户关闭模型管理窗口
            modelManager.ShowDialog();
            
            // 用户关闭模型管理窗口后，检查是否下载了模型
            _ = CheckModelStatusAfterManagerAsync();
        }

        private void ModelManagerButton_Click(object sender, RoutedEventArgs e)
        {
            OpenModelManager();
        }

        /// <summary>
        /// 模型管理窗口关闭后检查模型状态
        /// </summary>
        private async Task CheckModelStatusAfterManagerAsync()
        {
            await CheckModelStatusAsync();
            
            if (_modelDownloaded)
            {
                // 已下载模型，显示完成状态
                _downloadCompleted = true;
                UpdateDownloadPanel();
                UpdateCardDisplay();
            }
            else
            {
                // 未下载，保持在当前状态
                _isDownloading = false;
                UpdateDownloadPanel();
                UpdateCardDisplay();
            }
        }

        #endregion
    }
}
