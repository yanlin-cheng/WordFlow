using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace WordFlow.Views
{
    /// <summary>
    /// TranscriptPopupWindow.xaml 的交互逻辑
    /// 用于在屏幕底部显示识别结果，支持复制功能
    /// </summary>
    public partial class TranscriptPopupWindow : Window
    {
        private string _transcriptText = "";
        private System.Windows.Threading.DispatcherTimer? _autoHideTimer;

        public TranscriptPopupWindow()
        {
            InitializeComponent();
            
            // 初始化自动隐藏定时器
            _autoHideTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10) // 10 秒后自动隐藏
            };
            _autoHideTimer.Tick += (s, e) => Hide();
            
            // 鼠标悬停时重置定时器
            MouseEnter += (s, e) => _autoHideTimer?.Stop();
            MouseLeave += (s, e) => _autoHideTimer?.Start();
        }

        /// <summary>
        /// 设置识别文本并显示窗口
        /// </summary>
        public void ShowTranscript(string text)
        {
            _transcriptText = text;
            TranscriptText.Text = text;
            
            // 重置并启动定时器
            _autoHideTimer?.Stop();
            _autoHideTimer?.Start();
            
            // 确保窗口显示
            Show();
        }

        /// <summary>
        /// 隐藏窗口
        /// </summary>
        public new void Hide()
        {
            base.Hide();
            _autoHideTimer?.Stop();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_transcriptText))
            {
                Clipboard.SetText(_transcriptText);
                
                // 显示复制成功提示
                CopyButton.Content = "已复制!";
                
                // 1 秒后恢复按钮文字
                System.Windows.Threading.DispatcherTimer timer = new();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += (s, e) =>
                {
                    CopyButton.Content = "复制";
                    timer.Stop();
                    Hide();
                };
                timer.Start();
            }
        }

        /// <summary>
        /// 允许拖动窗口
        /// </summary>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            DragMove();
        }

        #region Windows API - 点击穿透（可选功能）

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            
            // 添加工具窗口样式（不显示在任务栏）
            int extendedStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, extendedStyle | WS_EX_TOOLWINDOW);
        }

        #endregion
    }
}
