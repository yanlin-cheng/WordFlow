using System;
using System.Collections.Generic;

namespace WordFlowInstaller.Models
{
    /// <summary>
    /// 模型信息类 - 描述可用语音识别模型
    /// </summary>
    public class ModelInfo
    {
        /// <summary>
        /// 模型唯一标识符
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// 模型显示名称
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// 模型描述
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// 模型大小（人类可读格式，如 "206 MB"）
        /// </summary>
        public string Size { get; set; } = "";

        /// <summary>
        /// 模型大小（字节）
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// 模型文件列表（支持分割文件）
        /// </summary>
        public List<string> Files { get; set; } = new List<string>();

        /// <summary>
        /// 是否是默认模型
        /// </summary>
        public bool IsDefault { get; set; } = false;

        /// <summary>
        /// 模型状态：Available, Developing, Deprecated
        /// </summary>
        public string Status { get; set; } = "Available";

        /// <summary>
        /// 模型语言
        /// </summary>
        public string Language { get; set; } = "";

        /// <summary>
        /// 是否已选中
        /// </summary>
        public bool IsSelected { get; set; } = false;
    }

    /// <summary>
    /// 镜像源信息
    /// </summary>
    public class MirrorInfo
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsDefault { get; set; } = false;
    }
}
