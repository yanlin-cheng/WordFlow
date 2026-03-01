using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WordFlowInstaller.Models;
using WordFlowInstaller.Services;

namespace WordFlowInstaller.Forms
{
    public partial class ProgressPanel : UserControl
    {
        private readonly InstallConfig config;
        private readonly InstallationService installationService;
        
        private Label statusLabel;
        private ProgressBar progressBar;
        private Label progressLabel;
        private ListBox logListBox;
        private Button cancelButton;
        private Label completeLabel;
        
        private CancellationTokenSource cancellationTokenSource;
        private bool installationStarted = false;

        public ProgressPanel(InstallConfig config, InstallationService installationService)
        {
            this.config = config;
            this.installationService = installationService;
            InitializeComponent();
            SetupEvents();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(640, 380);
            this.BackColor = Color.White;

            // 标题
            var titleLabel = new Label
            {
                Text = "正在安装 WordFlow",
                Font = new Font("Microsoft YaHei", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 51, 102),
                Location = new Point(20, 15),
                Size = new Size(600, 35),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 状态标签
            statusLabel = new Label
            {
                Text = "准备开始安装...",
                Font = new Font("Microsoft YaHei", 10),
                ForeColor = Color.FromArgb(80, 80, 80),
                Location = new Point(20, 55),
                Size = new Size(600, 25),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 进度条
            progressBar = new ProgressBar
            {
                Location = new Point(20, 90),
                Size = new Size(600, 30),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };

            // 进度百分比
            progressLabel = new Label
            {
                Text = "0%",
                Font = new Font("Microsoft YaHei", 9),
                ForeColor = Color.FromArgb(100, 100, 100),
                Location = new Point(20, 125),
                Size = new Size(600, 20),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // 日志列表
            logListBox = new ListBox
            {
                Location = new Point(20, 155),
                Size = new Size(600, 150),
                Font = new Font("Consolas", 8),
                BorderStyle = BorderStyle.FixedSingle,
                HorizontalScrollbar = true
            };

            // 完成标签（初始隐藏）
            completeLabel = new Label
            {
                Text = "✓ 安装完成！",
                Font = new Font("Microsoft YaHei", 24, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 180, 80),
                Location = new Point(200, 200),
                Size = new Size(240, 60),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };

            // 取消按钮
            cancelButton = new Button
            {
                Text = "取消",
                Font = new Font("Microsoft YaHei", 9),
                Location = new Point(530, 320),
                Size = new Size(90, 35),
                Enabled = false
            };
            cancelButton.Click += CancelButton_Click;

            this.Controls.Add(titleLabel);
            this.Controls.Add(statusLabel);
            this.Controls.Add(progressBar);
            this.Controls.Add(progressLabel);
            this.Controls.Add(logListBox);
            this.Controls.Add(completeLabel);
            this.Controls.Add(cancelButton);
        }

        private void SetupEvents()
        {
            installationService.ProgressChanged += OnProgressChanged;
            installationService.StatusChanged += OnStatusChanged;
        }

        private void OnStatusChanged(object sender, string status)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnStatusChanged(sender, status)));
                return;
            }

            statusLabel.Text = status;
            logListBox.Items.Add($"> {status}");
            logListBox.TopIndex = logListBox.Items.Count - 1;
        }

        private void OnProgressChanged(object sender, InstallProgressEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnProgressChanged(sender, e)));
                return;
            }

            progressBar.Value = Math.Min(100, Math.Max(0, e.ProgressPercentage));
            progressLabel.Text = $"{e.ProgressPercentage}%";
            
            if (!string.IsNullOrEmpty(e.Status))
            {
                statusLabel.Text = e.Status;
            }
        }

        public async Task StartInstallationAsync()
        {
            if (installationStarted) return;
            
            installationStarted = true;
            cancelButton.Enabled = true;
            cancellationTokenSource = new CancellationTokenSource();

            try
            {
                logListBox.Items.Add("========================================");
                logListBox.Items.Add($"安装路径：{config.InstallPath}");
                logListBox.Items.Add($"选中模型：{string.Join(", ", config.SelectedModels)}");
                logListBox.Items.Add("========================================");
                logListBox.Items.Add("");

                var success = await installationService.InstallAsync(cancellationTokenSource.Token);

                if (success)
                {
                    ShowComplete();
                }
            }
            catch (OperationCanceledException)
            {
                logListBox.Items.Add("安装已取消");
                statusLabel.Text = "安装已取消";
                progressBar.Style = ProgressBarStyle.Continuous;
            }
            catch (Exception ex)
            {
                logListBox.Items.Add($"错误：{ex.Message}");
                statusLabel.Text = $"安装失败：{ex.Message}";
                MessageBox.Show($"安装失败：{ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                cancelButton.Enabled = false;
            }
        }

        private void ShowComplete()
        {
            completeLabel.Visible = true;
            statusLabel.Text = "安装完成！";
            progressBar.Value = 100;
            progressLabel.Text = "100%";
            logListBox.Items.Add("========================================");
            logListBox.Items.Add("安装成功完成！");
            logListBox.Items.Add("请点击\"下一步\"完成安装向导。");
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "确定要取消安装吗？\n所有已下载的文件将被删除。",
                "确认取消",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                cancellationTokenSource?.Cancel();
                installationService.CancelInstallation();
            }
        }
    }
}
