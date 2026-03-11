using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WordFlow.Services;

namespace WordFlow.Views
{
    /// <summary>
    /// 首次运行向导 - 卡片式滚动展示
    /// </summary>
    public partial class FirstRunWizard : Window
    {
        private readonly ModelDownloadService _downloadService;
        private int _currentCardIndex = 1; // 1-6 为功能卡片，7 为完成页面
        private bool _modelDownloaded = false;
        
        // 卡片总数（不包括完成页面）
        private const int FeatureCardCount = 6;
        private const int CompleteCardIndex = 7;
        private const int ModelManagerCardIndex = 6; // 下载模型卡片索引

        /// <summary>
        /// 下载是否成功完成
        /// </summary>
        public bool DownloadCompleted => _modelDownloaded;

        public FirstRunWizard()
        {
            InitializeComponent();
            
            _downloadService = new ModelDownloadService();
            
            Loaded += FirstRunWizard_Loaded;
        }

        #region 窗口生命周期

        private async void FirstRunWizard_Loaded(object sender, RoutedEventArgs e)
        {
            await CheckModelStatusAsync();
            InitializeCardIndicators();
            UpdateCardDisplay();
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
                    ModelStatusText.Text = "模型已安装，开始使用语音输入吧！";
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
            SetCard6Visibility(Visibility.Collapsed);
            CompleteCard.Visibility = Visibility.Collapsed;

            // 显示当前卡片
            switch (_currentCardIndex)
            {
                case 1:
                    Card1.Visibility = Visibility.Visible;
                    NextButton.Content = "下一步";
                    break;
                case 2:
                    Card2.Visibility = Visibility.Visible;
                    NextButton.Content = "下一步";
                    break;
                case 3:
                    Card3.Visibility = Visibility.Visible;
                    NextButton.Content = "下一步";
                    break;
                case 4:
                    Card4.Visibility = Visibility.Visible;
                    NextButton.Content = "下一步";
                    break;
                case 5:
                    Card5.Visibility = Visibility.Visible;
                    NextButton.Content = "下一步";
                    break;
                case ModelManagerCardIndex:
                    SetCard6Visibility(Visibility.Visible);
                    // 下载模型卡片不显示下一步按钮
                    NextButton.Visibility = Visibility.Collapsed;
                    SetModelManagerButtonVisibility(Visibility.Visible);
                    break;
                case CompleteCardIndex:
                    CompleteCard.Visibility = Visibility.Visible;
                    NextButton.Content = "开始使用";
                    break;
            }

            // 更新按钮状态
            BackButton.Visibility = _currentCardIndex > 1 ? Visibility.Visible : Visibility.Collapsed;
            
            // 在下载模型卡片隐藏下一步按钮，显示打开模型管理按钮
            if (_currentCardIndex == ModelManagerCardIndex)
            {
                NextButton.Visibility = Visibility.Collapsed;
                SetModelManagerButtonVisibility(Visibility.Visible);
            }
            else
            {
                NextButton.Visibility = Visibility.Visible;
                SetModelManagerButtonVisibility(Visibility.Collapsed);
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
        }

        /// <summary>
        /// 设置 Card6 可见性
        /// </summary>
        private void SetCard6Visibility(Visibility visibility)
        {
            if (FindName("Card6") is System.Windows.Controls.Border card6Border)
                card6Border.Visibility = visibility;
        }

        /// <summary>
        /// 设置 ModelManagerButton 可见性
        /// </summary>
        private void SetModelManagerButtonVisibility(Visibility visibility)
        {
            if (FindName("ModelManagerButton") is System.Windows.Controls.Button btn)
                btn.Visibility = visibility;
        }

        #endregion

        #region 导航控制

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentCardIndex == ModelManagerCardIndex)
            {
                // 下载模型卡片，必须打开模型管理
                OpenModelManager();
            }
            else if (_currentCardIndex < CompleteCardIndex)
            {
                _currentCardIndex++;
                UpdateCardDisplay();
                
                // 如果到达下载模型卡片且已有模型，直接跳到完成页面
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
                // 已下载模型，显示完成页面
                _currentCardIndex = CompleteCardIndex;
                UpdateCardDisplay();
            }
            else
            {
                MessageBox.Show(
                    "检测到您还未下载模型，语音输入功能将无法使用。\n\n请返回模型管理下载模型，或稍后在设置中下载。",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                
                // 保持在下载模型卡片
                _currentCardIndex = ModelManagerCardIndex;
                UpdateCardDisplay();
            }
        }

        #endregion
    }
}
