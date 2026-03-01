using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WordFlowInstaller.Models;

namespace WordFlowInstaller.Forms
{
    public class InstallLocationPanel : UserControl
    {
        private readonly InstallConfig config;
        private TextBox pathTextBox;
        private Label errorLabel;

        public InstallLocationPanel(InstallConfig config)
        {
            this.config = config;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(640, 380);
            this.BackColor = Color.White;

            // 标题
            var titleLabel = new Label
            {
                Text = "选择安装位置",
                Font = new Font("Microsoft YaHei", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 51, 102),
                Location = new Point(20, 15),
                Size = new Size(600, 35),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 说明文字
            var descLabel = new Label
            {
                Text = "请选择 WordFlow 的安装位置：",
                Font = new Font("Microsoft YaHei", 10),
                ForeColor = Color.FromArgb(80, 80, 80),
                Location = new Point(20, 55),
                Size = new Size(600, 25),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 路径输入框
            pathTextBox = new TextBox
            {
                Font = new Font("Consolas", 10),
                Location = new Point(20, 90),
                Size = new Size(500, 25),
                Text = config.InstallPath
            };
            pathTextBox.TextChanged += PathTextBox_TextChanged;

            // 浏览按钮
            var browseButton = new Button
            {
                Text = "浏览...",
                Font = new Font("Microsoft YaHei", 9),
                Location = new Point(530, 88),
                Size = new Size(90, 30)
            };
            browseButton.Click += BrowseButton_Click;

            // 推荐路径提示
            var tipPanel = new Panel
            {
                Location = new Point(20, 130),
                Size = new Size(600, 60),
                BackColor = Color.FromArgb(230, 245, 255),
                BorderStyle = BorderStyle.FixedSingle
            };

            var tipIcon = new Label
            {
                Text = "💡",
                Font = new Font("Segoe UI Emoji", 20),
                Location = new Point(10, 10),
                Size = new Size(35, 35),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var tipText = new Label
            {
                Text = "推荐安装到默认位置，除非您有特殊需求。\n程序需要约 200MB 的磁盘空间。",
                Font = new Font("Microsoft YaHei", 9),
                ForeColor = Color.FromArgb(50, 50, 50),
                Location = new Point(50, 10),
                Size = new Size(530, 40),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            tipPanel.Controls.Add(tipIcon);
            tipPanel.Controls.Add(tipText);

            // 错误提示
            errorLabel = new Label
            {
                Text = "",
                Font = new Font("Microsoft YaHei", 9),
                ForeColor = Color.FromArgb(200, 50, 50),
                Location = new Point(20, 200),
                Size = new Size(600, 25),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 磁盘空间信息
            var spaceLabel = new Label
            {
                Text = GetDiskSpaceInfo(),
                Font = new Font("Microsoft YaHei", 8),
                ForeColor = Color.FromArgb(100, 100, 100),
                Location = new Point(20, 230),
                Size = new Size(600, 20),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            this.Controls.Add(titleLabel);
            this.Controls.Add(descLabel);
            this.Controls.Add(pathTextBox);
            this.Controls.Add(browseButton);
            this.Controls.Add(tipPanel);
            this.Controls.Add(errorLabel);
            this.Controls.Add(spaceLabel);

            ValidatePath();
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "选择 WordFlow 安装位置",
                SelectedPath = pathTextBox.Text,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                pathTextBox.Text = dialog.SelectedPath;
            }
        }

        private void PathTextBox_TextChanged(object sender, EventArgs e)
        {
            config.InstallPath = pathTextBox.Text;
            ValidatePath();
        }

        private void ValidatePath()
        {
            var path = pathTextBox.Text;

            if (string.IsNullOrWhiteSpace(path))
            {
                errorLabel.Text = "※ 请输入安装路径";
                this.Tag = "Invalid";
                return;
            }

            // 检查是否是有效路径
            try
            {
                var root = Path.GetPathRoot(path);
                if (!Directory.Exists(root))
                {
                    errorLabel.Text = "※ 指定的驱动器不存在";
                    this.Tag = "Invalid";
                    return;
                }

                // 检查是否有写入权限（尝试创建目录）
                var testPath = Path.Combine(path, ".test_write");
                try
                {
                    Directory.CreateDirectory(testPath);
                    Directory.Delete(testPath);
                }
                catch
                {
                    errorLabel.Text = "※ 没有写入权限，请选择其他路径";
                    this.Tag = "Invalid";
                    return;
                }

                errorLabel.Text = "";
                this.Tag = null;
            }
            catch (Exception ex)
            {
                errorLabel.Text = $"※ 无效的路径：{ex.Message}";
                this.Tag = "Invalid";
            }
        }

        private string GetDiskSpaceInfo()
        {
            try
            {
                var root = Path.GetPathRoot(pathTextBox.Text) ?? "C:\\";
                var drive = new DriveInfo(root);
                var freeSpaceGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                return $"可用磁盘空间：{freeSpaceGB:F1} GB";
            }
            catch
            {
                return "";
            }
        }
    }
}
