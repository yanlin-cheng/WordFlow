using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WordFlowInstaller.Models;

namespace WordFlowInstaller.Forms
{
    public class CompletePanel : UserControl
    {
        private readonly InstallConfig config;
        private readonly Action onCompleteCallback;
        
        private CheckBox launchCheckBox;
        private CheckBox shortcutCheckBox;
        private CheckBox autoStartCheckBox;
        private Button finishButton;

        public CompletePanel(InstallConfig config, Action onCompleteCallback)
        {
            this.config = config;
            this.onCompleteCallback = onCompleteCallback;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(640, 380);
            this.BackColor = Color.White;

            // 完成图标
            var iconLabel = new Label
            {
                Text = "🎉",
                Font = new Font("Segoe UI Emoji", 48),
                Location = new Point(280, 20),
                Size = new Size(80, 80),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // 完成标题
            var titleLabel = new Label
            {
                Text = "安装完成！",
                Font = new Font("Microsoft YaHei", 20, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 180, 80),
                Location = new Point(50, 100),
                Size = new Size(540, 40),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // 说明文字
            var descLabel = new Label
            {
                Text = "WordFlow 已成功安装到您的计算机。",
                Font = new Font("Microsoft YaHei", 10),
                ForeColor = Color.FromArgb(80, 80, 80),
                Location = new Point(50, 140),
                Size = new Size(540, 25),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // 选项面板
            var optionsPanel = new Panel
            {
                Location = new Point(100, 180),
                Size = new Size(440, 140),
                BackColor = Color.FromArgb(245, 248, 250),
                BorderStyle = BorderStyle.FixedSingle
            };

            // 立即启动
            launchCheckBox = new CheckBox
            {
                Text = "立即启动 WordFlow",
                Font = new Font("Microsoft YaHei", 9),
                Location = new Point(20, 15),
                Size = new Size(400, 25),
                Checked = config.LaunchAfterInstall,
                AutoSize = true
            };

            // 创建桌面快捷方式
            shortcutCheckBox = new CheckBox
            {
                Text = "创建桌面快捷方式",
                Font = new Font("Microsoft YaHei", 9),
                Location = new Point(20, 45),
                Size = new Size(400, 25),
                Checked = config.CreateDesktopShortcut,
                AutoSize = true
            };
            shortcutCheckBox.CheckedChanged += ShortcutCheckBox_CheckedChanged;

            // 开机自启动
            autoStartCheckBox = new CheckBox
            {
                Text = "开机自动启动",
                Font = new Font("Microsoft YaHei", 9),
                Location = new Point(20, 75),
                Size = new Size(400, 25),
                Checked = config.AutoStart,
                AutoSize = true
            };
            autoStartCheckBox.CheckedChanged += AutoStartCheckBox_CheckedChanged;

            optionsPanel.Controls.Add(launchCheckBox);
            optionsPanel.Controls.Add(shortcutCheckBox);
            optionsPanel.Controls.Add(autoStartCheckBox);

            // 完成按钮（在面板内部）
            finishButton = new Button
            {
                Text = "完成",
                Font = new Font("Microsoft YaHei", 9, FontStyle.Bold),
                Size = new Size(120, 35),
                Location = new Point(260, 330),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            finishButton.FlatAppearance.BorderSize = 0;
            finishButton.Click += FinishButton_Click;

            // 安装路径信息
            var pathLabel = new Label
            {
                Text = $"安装位置：{config.InstallPath}",
                Font = new Font("Microsoft YaHei", 8),
                ForeColor = Color.FromArgb(150, 150, 150),
                Location = new Point(50, 315),
                Size = new Size(540, 20),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // 打开目录按钮
            var openDirButton = new Button
            {
                Text = "打开安装目录",
                Font = new Font("Microsoft YaHei", 8),
                ForeColor = Color.FromArgb(0, 120, 215),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(270, 360),
                Size = new Size(100, 20),
                Cursor = Cursors.Hand
            };
            openDirButton.FlatAppearance.BorderSize = 0;
            openDirButton.Click += OpenDirButton_Click;

            this.Controls.Add(iconLabel);
            this.Controls.Add(titleLabel);
            this.Controls.Add(descLabel);
            this.Controls.Add(optionsPanel);
            this.Controls.Add(pathLabel);
            this.Controls.Add(openDirButton);
            this.Controls.Add(finishButton);
        }

        private void ShortcutCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            config.CreateDesktopShortcut = shortcutCheckBox.Checked;
        }

        private void AutoStartCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            config.AutoStart = autoStartCheckBox.Checked;
        }

        private void FinishButton_Click(object sender, EventArgs e)
        {
            // 保存设置
            config.LaunchAfterInstall = launchCheckBox.Checked;
            
            // 调用完成回调
            onCompleteCallback?.Invoke();
        }

        private void OpenDirButton_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(config.InstallPath))
            {
                Process.Start("explorer.exe", config.InstallPath);
            }
        }

        public void ApplySettings()
        {
            config.LaunchAfterInstall = launchCheckBox.Checked;
            
            if (config.CreateDesktopShortcut != shortcutCheckBox.Checked)
            {
                config.CreateDesktopShortcut = shortcutCheckBox.Checked;
            }
            
            config.AutoStart = autoStartCheckBox.Checked;
        }
    }
}
