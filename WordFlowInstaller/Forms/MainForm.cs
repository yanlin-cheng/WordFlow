using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using WordFlowInstaller.Services;
using WordFlowInstaller.Models;

namespace WordFlowInstaller.Forms
{
    public partial class MainForm : Form
    {
        private Panel contentPanel;
        private Panel buttonPanel;
        private Button prevButton;
        private Button nextButton;
        private Button cancelButton;
        private Panel headerPanel;
        private Label titleLabel;
        private ProgressBar progressTracker;
        
        private UserControl[] panels;
        private int currentPanelIndex = 0;
        
        private InstallConfig config;
        private InstallationService installationService;

        public MainForm()
        {
            InitializeComponent();
            InitializePanels();
            UpdateUI();
        }

        private void InitializeComponent()
        {
            this.Text = "WordFlow 安装向导";
            this.Size = new Size(700, 550);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            
            // 启用双缓冲减少闪烁
            this.SetStyle(ControlStyles.DoubleBuffer | 
                         ControlStyles.AllPaintingInWmPaint | 
                         ControlStyles.UserPaint, true);
            this.UpdateStyles();

            // 头部面板
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(0, 51, 102)
            };

            titleLabel = new Label
            {
                Text = "WordFlow 语音输入工具",
                Font = new Font("Microsoft YaHei", 18, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(30, 20),
                Size = new Size(400, 35),
                AutoSize = false
            };

            var subtitleLabel = new Label
            {
                Text = "安装向导",
                Font = new Font("Microsoft YaHei", 10),
                ForeColor = Color.FromArgb(200, 200, 200),
                Location = new Point(30, 50),
                Size = new Size(200, 25),
                AutoSize = false
            };

            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(subtitleLabel);

            // 进度指示器
            progressTracker = new ProgressBar
            {
                Location = new Point(30, 5),
                Size = new Size(620, 3),
                Minimum = 0,
                Maximum = 5,
                Value = 1,
                Style = ProgressBarStyle.Continuous
            };
            headerPanel.Controls.Add(progressTracker);

            // 内容面板
            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(30, 20, 30, 20)
            };

            // 按钮面板
            buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(245, 245, 245)
            };

            prevButton = new Button
            {
                Text = "上一步",
                Font = new Font("Microsoft YaHei", 9),
                Size = new Size(90, 35),
                Location = new Point(380, 12),
                Enabled = false
            };
            prevButton.Click += PrevButton_Click;

            nextButton = new Button
            {
                Text = "下一步",
                Font = new Font("Microsoft YaHei", 9, FontStyle.Bold),
                Size = new Size(90, 35),
                Location = new Point(480, 12),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            nextButton.FlatAppearance.BorderSize = 0;
            nextButton.Click += NextButton_Click;

            cancelButton = new Button
            {
                Text = "取消",
                Font = new Font("Microsoft YaHei", 9),
                Size = new Size(90, 35),
                Location = new Point(580, 12)
            };
            cancelButton.Click += CancelButton_Click;

            buttonPanel.Controls.Add(prevButton);
            buttonPanel.Controls.Add(nextButton);
            buttonPanel.Controls.Add(cancelButton);

            // 添加控件到窗体
            this.Controls.Add(contentPanel);
            this.Controls.Add(buttonPanel);
            this.Controls.Add(headerPanel);

            headerPanel.BringToFront();
        }

        private void InitializePanels()
        {
            config = new InstallConfig();
            installationService = new InstallationService(config);

            panels = new UserControl[]
            {
                new WelcomePanel(config),
                new LicensePanel(config),
                new InstallLocationPanel(config),
                new ModelSelectPanel(config),
                new ProgressPanel(config, installationService),
                new CompletePanel(config, OnInstallationComplete)
            };

            currentPanelIndex = 0;
            ShowPanel(0);
        }

        private void ShowPanel(int index)
        {
            contentPanel.Controls.Clear();
            if (index >= 0 && index < panels.Length)
            {
                var panel = panels[index];
                panel.Dock = DockStyle.Fill;
                contentPanel.Controls.Add(panel);
                progressTracker.Value = index + 1;
            }
        }

        private void UpdateUI()
        {
            prevButton.Enabled = currentPanelIndex > 0;
            
            if (currentPanelIndex == panels.Length - 1)
            {
                nextButton.Text = "完成";
                nextButton.Enabled = false;
            }
            else if (currentPanelIndex == panels.Length - 2)
            {
                nextButton.Text = "安装";
                nextButton.Enabled = true;
            }
            else
            {
                // 检查当前面板是否有效
                bool isValid = true;
                if (panels[currentPanelIndex].Tag != null)
                {
                    isValid = panels[currentPanelIndex].Tag.ToString() != "Invalid";
                }
                nextButton.Enabled = isValid;
            }
        }

        private void PrevButton_Click(object sender, EventArgs e)
        {
            if (currentPanelIndex > 0)
            {
                currentPanelIndex--;
                ShowPanel(currentPanelIndex);
                UpdateUI();
            }
        }

        private async void NextButton_Click(object sender, EventArgs e)
        {
            // 如果是进度面板，开始安装
            if (panels[currentPanelIndex] is ProgressPanel progressPanel)
            {
                nextButton.Enabled = false;
                cancelButton.Enabled = false;
                await progressPanel.StartInstallationAsync();
                return;
            }

            // 检查当前面板是否有效
            bool isValid = true;
            if (panels[currentPanelIndex].Tag != null)
            {
                isValid = panels[currentPanelIndex].Tag.ToString() != "Invalid";
            }
            
            if (!isValid)
            {
                return; // 当前面板无效，不响应
            }

            if (currentPanelIndex < panels.Length - 1)
            {
                currentPanelIndex++;
                ShowPanel(currentPanelIndex);
                UpdateUI();
            }
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "确定要取消安装吗？",
                "确认取消",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                Application.Exit();
            }
        }
        
        /// <summary>
        /// 安装完成后调用
        /// </summary>
        private void OnInstallationComplete()
        {
            // 如果用户选择了安装后启动，启动 WordFlow
            if (config.LaunchAfterInstall)
            {
                try
                {
                    var exePath = System.IO.Path.Combine(config.InstallPath, "WordFlow.exe");
                    if (System.IO.File.Exists(exePath))
                    {
                        Process.Start(exePath);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"启动 WordFlow 失败：{ex.Message}", "提示", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            
            // 关闭安装程序
            Application.Exit();
        }
    }
}
