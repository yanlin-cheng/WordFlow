using System;
using System.Collections.Generic;
using System.IO;

namespace WordFlowInstaller.Models
{
    /// <summary>
    /// 安装配置类 - 存储用户选择的安装选项
    /// </summary>
    public class InstallConfig
    {
        /// <summary>
        /// 安装路径
        /// </summary>
        public string InstallPath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "WordFlow");

        /// <summary>
        /// 是否创建桌面快捷方式
        /// </summary>
        public bool CreateDesktopShortcut { get; set; } = true;

        /// <summary>
        /// 是否开机自启动
        /// </summary>
        public bool AutoStart { get; set; } = false;

        /// <summary>
        /// 是否立即启动程序
        /// </summary>
        public bool LaunchAfterInstall { get; set; } = true;

        /// <summary>
        /// 选择的模型列表
        /// </summary>
        public List<string> SelectedModels { get; set; } = new List<string> { "paraformer-zh" };

        /// <summary>
        /// 使用镜像源下载
        /// </summary>
        public bool UseMirror { get; set; } = true;

        /// <summary>
        /// 安装是否完成
        /// </summary>
        public bool InstallationCompleted { get; set; } = false;

        /// <summary>
        /// 安装完成后的消息
        /// </summary>
        public string CompletionMessage { get; set; } = "";
    }
}
