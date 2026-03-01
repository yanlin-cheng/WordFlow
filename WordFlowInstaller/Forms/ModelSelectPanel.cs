using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using WordFlowInstaller.Models;

namespace WordFlowInstaller.Forms
{
    public class ModelSelectPanel : UserControl
    {
        private readonly InstallConfig config;
        private List<ModelInfo> models;
        private Panel modelsPanel;
        private Label tipLabel;

        public ModelSelectPanel(InstallConfig config)
        {
            this.config = config;
            InitializeComponent();
            LoadModels();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(640, 380);
            this.BackColor = Color.White;

            // 标题
            var titleLabel = new Label
            {
                Text = "选择语音识别模型",
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
                Text = "请选择您要安装的语音识别模型（至少选择一个）：",
                Font = new Font("Microsoft YaHei", 10),
                ForeColor = Color.FromArgb(80, 80, 80),
                Location = new Point(20, 55),
                Size = new Size(600, 25),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 模型列表面板
            modelsPanel = new Panel
            {
                Location = new Point(20, 90),
                Size = new Size(600, 200),
                AutoScroll = true,
                BackColor = Color.FromArgb(250, 250, 250),
                BorderStyle = BorderStyle.FixedSingle
            };

            // 提示信息
            tipLabel = new Label
            {
                Text = "※ 请至少选择一个模型",
                Font = new Font("Microsoft YaHei", 9),
                ForeColor = Color.FromArgb(200, 50, 50),
                Location = new Point(20, 300),
                Size = new Size(600, 25),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Name = "tipLabel",
                Visible = true
            };

            this.Controls.Add(titleLabel);
            this.Controls.Add(descLabel);
            this.Controls.Add(modelsPanel);
            this.Controls.Add(tipLabel);

            // 初始默认选择第一个模型，所以初始为有效状态
            this.Tag = null;
        }

        private void LoadModels()
        {
            models = new List<ModelInfo>
            {
                new ModelInfo
                {
                    Id = "paraformer-zh",
                    Name = "中文语音识别（推荐）",
                    Description = "阿里巴巴达摩院开源的 Paraformer 中文模型",
                    Size = "206 MB",
                    SizeBytes = 216000000,
                    Language = "中文普通话",
                    Status = "Available",
                    IsDefault = true,
                    IsSelected = true, // 默认选中
                    Files = new List<string>
                    {
                        "paraformer-zh.tar.bz2.part1",
                        "paraformer-zh.tar.bz2.part2",
                        "paraformer-zh.tar.bz2.part3"
                    }
                },
                new ModelInfo
                {
                    Id = "paraformer-en",
                    Name = "英文语音识别（开发中）",
                    Description = "Paraformer 英文语音识别模型",
                    Size = "180 MB",
                    SizeBytes = 189000000,
                    Language = "英语",
                    Status = "Developing",
                    IsDefault = false,
                    IsSelected = false,
                    Files = new List<string>()
                }
            };

            RenderModels();
            
            // 初始更新配置和验证
            UpdateConfig();
            ValidateSelection();
        }

        private void RenderModels()
        {
            modelsPanel.Controls.Clear();

            int y = 10;
            foreach (var model in models)
            {
                var modelCard = CreateModelCard(model, y);
                modelsPanel.Controls.Add(modelCard);
                y += 90;
            }

            modelsPanel.Height = Math.Max(200, y - 10);
        }

        private Panel CreateModelCard(ModelInfo model, int y)
        {
            var card = new Panel
            {
                Location = new Point(10, y),
                Size = new Size(570, 80),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            // 复选框
            var checkBox = new CheckBox
            {
                Text = "",
                Checked = model.IsSelected,
                Enabled = model.Status == "Available",
                Location = new Point(10, 10),
                Size = new Size(25, 25),
                Tag = model,
                Name = $"checkBox_{model.Id}"
            };
            checkBox.CheckedChanged += CheckBox_CheckedChanged;

            // 模型名称
            var nameLabel = new Label
            {
                Text = model.Name,
                Font = new Font("Microsoft YaHei", 11, FontStyle.Bold),
                ForeColor = model.Status == "Available" ? Color.FromArgb(0, 51, 102) : Color.Gray,
                Location = new Point(40, 8),
                Size = new Size(300, 25),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 模型描述
            var descLabel = new Label
            {
                Text = model.Description,
                Font = new Font("Microsoft YaHei", 9),
                ForeColor = Color.FromArgb(100, 100, 100),
                Location = new Point(40, 30),
                Size = new Size(400, 20),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 模型大小和语言
            var infoLabel = new Label
            {
                Text = $"大小：{model.Size}  语言：{model.Language}",
                Font = new Font("Microsoft YaHei", 8),
                ForeColor = Color.FromArgb(150, 150, 150),
                Location = new Point(40, 52),
                Size = new Size(300, 20),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 状态标签
            var statusLabel = new Label
            {
                Text = model.Status == "Available" ? "可用" : "开发中",
                Font = new Font("Microsoft YaHei", 8),
                ForeColor = model.Status == "Available" ? Color.White : Color.Gray,
                BackColor = model.Status == "Available" ? Color.FromArgb(0, 180, 80) : Color.FromArgb(200, 200, 200),
                Location = new Point(450, 10),
                Size = new Size(60, 22),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // 下载大小提示
            var downloadLabel = new Label
            {
                Text = model.Status == "Available" ? "将从 Gitee 下载" : "暂不可用",
                Font = new Font("Microsoft YaHei", 8),
                ForeColor = Color.FromArgb(0, 120, 215),
                Location = new Point(450, 40),
                Size = new Size(100, 20),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter
            };

            card.Controls.Add(checkBox);
            card.Controls.Add(nameLabel);
            card.Controls.Add(descLabel);
            card.Controls.Add(infoLabel);
            card.Controls.Add(statusLabel);
            card.Controls.Add(downloadLabel);

            return card;
        }

        private void CheckBox_CheckedChanged(object sender, EventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox != null)
            {
                var model = checkBox.Tag as ModelInfo;
                if (model != null)
                {
                    model.IsSelected = checkBox.Checked;
                    UpdateConfig();
                    ValidateSelection();
                }
            }
        }

        private void UpdateConfig()
        {
            config.SelectedModels = new List<string>();
            foreach (var model in models)
            {
                if (model.IsSelected)
                {
                    config.SelectedModels.Add(model.Id);
                }
            }
        }

        private void ValidateSelection()
        {
            var hasSelection = models.Exists(m => m.IsSelected);
            
            if (tipLabel != null)
            {
                tipLabel.Visible = !hasSelection;
            }

            // 有选择则有效（Tag = null），无选择则无效（Tag = "Invalid"）
            this.Tag = hasSelection ? null : "Invalid";
            
            // 强制刷新 UI 状态
            this.Invalidate();
            this.Update();
        }
    }
}
