using System;
using System.Drawing;
using System.Windows.Forms;
using WordFlowInstaller.Models;

namespace WordFlowInstaller.Forms
{
    public class WelcomePanel : UserControl
    {
        private readonly InstallConfig config;

        public WelcomePanel(InstallConfig config)
        {
            this.config = config;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(640, 380);
            this.BackColor = Color.White;

            // 欢迎图标
            var iconLabel = new Label
            {
                Text = "🎉",
                Font = new Font("Segoe UI Emoji", 48),
                Location = new Point(280, 30),
                Size = new Size(80, 80),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // 欢迎标题
            var welcomeLabel = new Label
            {
                Text = "欢迎使用 WordFlow",
                Font = new Font("Microsoft YaHei", 20, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 51, 102),
                Location = new Point(50, 110),
                Size = new Size(540, 40),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // 副标题
            var subtitleLabel = new Label
            {
                Text = "语音输入工具 安装向导",
                Font = new Font("Microsoft YaHei", 12),
                ForeColor = Color.FromArgb(100, 100, 100),
                Location = new Point(50, 150),
                Size = new Size(540, 30),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // 说明文本框
            var infoBox = new Panel
            {
                Location = new Point(80, 200),
                Size = new Size(480, 120),
                BackColor = Color.FromArgb(245, 248, 250),
                BorderStyle = BorderStyle.FixedSingle
            };

            var infoText = new Label
            {
                Text = "本安装向导将帮助您安装 WordFlow 语音输入工具。\n\n" +
                       "• 中文实时语音识别\n" +
                       "• 支持全局热键录音\n" +
                       "• 自动输入到任意应用程序\n\n" +
                       "点击\"下一步\"继续安装。",
                Font = new Font("Microsoft YaHei", 9),
                ForeColor = Color.FromArgb(50, 50, 50),
                Location = new Point(20, 15),
                Size = new Size(440, 90),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            infoBox.Controls.Add(infoText);

            // 版本信息
            var versionLabel = new Label
            {
                Text = "版本 1.0.0",
                Font = new Font("Microsoft YaHei", 8),
                ForeColor = Color.FromArgb(150, 150, 150),
                Location = new Point(50, 340),
                Size = new Size(200, 20),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            this.Controls.Add(iconLabel);
            this.Controls.Add(welcomeLabel);
            this.Controls.Add(subtitleLabel);
            this.Controls.Add(infoBox);
            this.Controls.Add(versionLabel);
        }
    }
}
