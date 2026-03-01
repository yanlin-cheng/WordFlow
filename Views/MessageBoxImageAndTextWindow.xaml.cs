using System;
using System.Windows;
using System.Windows.Controls;

namespace WordFlow.Views
{
    /// <summary>
    /// 自定义消息对话框 - 支持自定义按钮文字
    /// </summary>
    public partial class MessageBoxImageAndTextWindow : Window
    {
        private int _selectedButtonIndex = -1;
        
        /// <summary>
        /// 创建自定义消息对话框
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">窗口标题</param>
        /// <param name="buttonTexts">按钮文字数组（最多 3 个）</param>
        public MessageBoxImageAndTextWindow(string message, string title, string[] buttonTexts)
        {
            InitializeComponent();
            
            Title = title;
            MessageTextBlock.Text = message;
            
            // 配置按钮
            if (buttonTexts.Length >= 1)
            {
                Button1.Content = buttonTexts[0];
                Button1.Visibility = Visibility.Visible;
                Button1.Focus(); // 默认聚焦第一个按钮
            }
            
            if (buttonTexts.Length >= 2)
            {
                Button2.Content = buttonTexts[1];
                Button2.Visibility = Visibility.Visible;
            }
            
            if (buttonTexts.Length >= 3)
            {
                Button3.Content = buttonTexts[2];
                Button3.Visibility = Visibility.Visible;
            }
        }
        
        /// <summary>
        /// 显示对话框并返回选中的按钮索引
        /// </summary>
        /// <returns>选中的按钮索引（0=第一个，1=第二个，2=第三个），-1 表示未选择</returns>
        public int ShowDialog()
        {
            base.ShowDialog();
            return _selectedButtonIndex;
        }
        
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender == Button1)
                _selectedButtonIndex = 0;
            else if (sender == Button2)
                _selectedButtonIndex = 1;
            else if (sender == Button3)
                _selectedButtonIndex = 2;
            
            Close();
        }
    }
}
