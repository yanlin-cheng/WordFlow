using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WordFlow.Views
{
    /// <summary>
    /// 录音指示器窗口 - 显示录音状态和波形
    /// </summary>
    public partial class RecordingIndicatorWindow : Window
    {
        private readonly DispatcherTimer _animationTimer;
        private readonly Random _random = new();
        private double _baseLineY;

        public RecordingIndicatorWindow()
        {
            InitializeComponent();
            
            _baseLineY = WaveformCanvas.ActualHeight / 2;
            if (_baseLineY == 0) _baseLineY = 25;

            // 动画定时器
            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _animationTimer.Tick += OnAnimationTick;
        }

        /// <summary>
        /// 设置状态文本（用于显示模型切换等信息）
        /// </summary>
        /// <param name="message">状态消息，为空则清除显示</param>
        public void SetStatusMessage(string? message)
        {
            Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrEmpty(message))
                {
                    StatusText.Text = "";
                    StatusText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    StatusText.Text = message;
                    StatusText.Visibility = Visibility.Visible;
                }
            });
        }

        /// <summary>
        /// 设置标题文本
        /// </summary>
        public void SetTitleMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                TitleText.Text = message;
            });
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            _baseLineY = WaveformCanvas.ActualHeight / 2;
            if (_baseLineY == 0) _baseLineY = 25;
            
            // 更新基准线位置
            BaseLine.Y1 = _baseLineY;
            BaseLine.Y2 = _baseLineY;
        }

        /// <summary>
        /// 显示在鼠标位置附近
        /// </summary>
        public void ShowAtCursor()
        {
            var mousePos = System.Windows.Forms.Cursor.Position;
            // 显示在鼠标上方一点
            Left = mousePos.X - Width / 2;
            Top = mousePos.Y - Height - 20;
            
            // 确保不超出屏幕
            var screen = System.Windows.Forms.Screen.FromPoint(mousePos);
            if (Left < screen.WorkingArea.Left) Left = screen.WorkingArea.Left + 10;
            if (Left + Width > screen.WorkingArea.Right) Left = screen.WorkingArea.Right - Width - 10;
            if (Top < screen.WorkingArea.Top) Top = mousePos.Y + 20;
            
            Show();
            _animationTimer.Start();
        }

        /// <summary>
        /// 显示在屏幕中央
        /// </summary>
        public void ShowAtCenter()
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            Left = screen.WorkingArea.Left + (screen.WorkingArea.Width - Width) / 2;
            Top = screen.WorkingArea.Top + (screen.WorkingArea.Height - Height) / 2;
            
            Show();
            _animationTimer.Start();
        }

        public new void Hide()
        {
            _animationTimer.Stop();
            base.Hide();
        }

        private void OnAnimationTick(object? sender, EventArgs e)
        {
            // 模拟波形动画
            var amplitude = _random.NextDouble() * 20 + 5; // 5-25 的高度
            var speed = _random.NextDouble() * 0.5 + 0.5;
            
            AnimateWaveBar(WaveBar, amplitude, speed);
            AnimateWaveBar(WaveBarLeft1, amplitude * 0.7, speed * 0.8);
            AnimateWaveBar(WaveBarLeft2, amplitude * 0.4, speed * 0.6);
            AnimateWaveBar(WaveBarRight1, amplitude * 0.7, speed * 0.8);
            AnimateWaveBar(WaveBarRight2, amplitude * 0.4, speed * 0.6);
        }

        private void AnimateWaveBar(System.Windows.Shapes.Rectangle bar, double targetHeight, double speed)
        {
            var currentHeight = bar.Height;
            var newHeight = currentHeight + (targetHeight - currentHeight) * 0.3;
            
            bar.Height = Math.Max(2, Math.Min(40, newHeight));
            bar.Width = Math.Max(2, bar.Height * 0.4);
            bar.RadiusX = bar.Width / 2;
            bar.RadiusY = bar.Height / 2;
            
            // 垂直居中
            Canvas.SetTop(bar, _baseLineY - bar.Height / 2);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _animationTimer?.Stop();
            base.OnClosing(e);
        }
    }
}
