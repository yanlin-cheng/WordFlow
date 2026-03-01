using System;
using System.Drawing;
using System.Windows.Forms;
using WordFlowInstaller.Models;

namespace WordFlowInstaller.Forms
{
    public class LicensePanel : UserControl
    {
        private readonly InstallConfig config;
        private CheckBox agreeCheckBox;

        public LicensePanel(InstallConfig config)
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
                Text = "许可协议",
                Font = new Font("Microsoft YaHei", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 51, 102),
                Location = new Point(20, 15),
                Size = new Size(600, 35),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 协议文本框
            var licenseTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9),
                Location = new Point(20, 60),
                Size = new Size(600, 220),
                ForeColor = Color.FromArgb(50, 50, 50),
                BackColor = Color.FromArgb(250, 250, 250),
                BorderStyle = BorderStyle.FixedSingle
            };
            licenseTextBox.Text = "WordFlow 语音输入工具 软件许可协议\r\n\r\n" +
                "重要提示：请认真阅读本许可协议\r\n\r\n" +
                "1. 许可授权\r\n" +
                "   本软件供个人非商业使用。安装本软件即表示您同意接受以下条款的约束。\r\n\r\n" +
                "2. 使用限制\r\n" +
                "   - 您仅可将本软件用于个人学习、研究或欣赏目的\r\n" +
                "   - 未经书面许可，不得将本软件用于任何商业目的\r\n" +
                "   - 不得对本软件进行反向工程、反编译或反汇编\r\n" +
                "   - 不得将本软件用于任何违法或有害的活动\r\n\r\n" +
                "3. 免责声明\r\n" +
                "   本软件按原样提供，不提供任何形式的保证。\r\n\r\n" +
                "4. 知识产权\r\n" +
                "   本软件的知识产权归原作者所有。\r\n\r\n" +
                "5. 协议终止\r\n" +
                "   如您违反本许可协议的任何条款，本授权将自动终止。\r\n\r\n" +
                "6. 其他\r\n" +
                "   本许可协议受中华人民共和国法律保护。";

            // 复选框
            agreeCheckBox = new CheckBox
            {
                Text = "我已阅读并接受许可协议",
                Font = new Font("Microsoft YaHei", 9),
                Location = new Point(20, 290),
                Size = new Size(200, 25),
                AutoSize = true
            };
            agreeCheckBox.CheckedChanged += AgreeCheckBox_CheckedChanged;

            // 提示信息
            var infoLabel = new Label
            {
                Text = "※ 请勾选以上复选框以继续安装",
                Font = new Font("Microsoft YaHei", 8),
                ForeColor = Color.FromArgb(150, 50, 50),
                Location = new Point(20, 320),
                Size = new Size(600, 20),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };

            this.Controls.Add(titleLabel);
            this.Controls.Add(licenseTextBox);
            this.Controls.Add(agreeCheckBox);
            this.Controls.Add(infoLabel);

            // 初始设置 Tag 为 "Invalid"，表示需要勾选才能继续
            this.Tag = "Invalid";
        }

        private void AgreeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (agreeCheckBox.Checked)
            {
                this.Tag = null; // 移除 Invalid 标记，允许继续
            }
            else
            {
                this.Tag = "Invalid"; // 添加 Invalid 标记，阻止继续
            }
        }
    }
}
