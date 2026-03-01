using System;
using System.Collections.Generic;

namespace WordFlow.Models
{
    /// <summary>
    /// 输入历史记录 - 每次语音输入的完整记录
    /// </summary>
    public class InputHistory
    {
        /// <summary>
        /// 唯一ID
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// 输入时间戳
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 语音识别结果（原始）
        /// </summary>
        public string OriginalText { get; set; } = "";
        
        /// <summary>
        /// 用户修正后的文字（如果用户修改了）
        /// </summary>
        public string? CorrectedText { get; set; }
        
        /// <summary>
        /// 是否被用户修正过
        /// </summary>
        public bool IsCorrected => !string.IsNullOrEmpty(CorrectedText) 
            && CorrectedText != OriginalText;
        
        /// <summary>
        /// 最终输入的文字（优先用修正版）
        /// </summary>
        public string FinalText => CorrectedText ?? OriginalText;
        
        /// <summary>
        /// 目标窗口标题
        /// </summary>
        public string? TargetWindowTitle { get; set; }
        
        /// <summary>
        /// 目标应用程序名称
        /// </summary>
        public string? TargetApplication { get; set; }
        
        /// <summary>
        /// 录音时长（秒）
        /// </summary>
        public double RecordingDuration { get; set; }
        
        /// <summary>
        /// 语音识别置信度（0-1）
        /// </summary>
        public double? Confidence { get; set; }
        
        /// <summary>
        /// 输入场景分类（自动识别）
        /// </summary>
        public InputScene Scene { get; set; } = InputScene.General;
        
        /// <summary>
        /// 关联的标签（用户手动添加或AI自动打标）
        /// </summary>
        public List<string> Tags { get; set; } = new();
        
        /// <summary>
        /// 是否已同步到云端（付费功能）
        /// </summary>
        public bool IsSynced { get; set; } = false;
        
        /// <summary>
        /// 是否已用于训练个人词库
        /// </summary>
        public bool IsUsedForTraining { get; set; } = false;
        
        /// <summary>
        /// 音频文件路径（可选保存录音）
        /// </summary>
        public string? AudioFilePath { get; set; }
    }
    
    /// <summary>
    /// 输入场景分类
    /// </summary>
    public enum InputScene
    {
        General,        // 通用
        Medical,        // 医疗
        Legal,          // 法律
        Programming,    // 编程
        Business,       // 商务
        Academic,       // 学术
        Chat,           // 聊天
    }
}
