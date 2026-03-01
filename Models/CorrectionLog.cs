using System;
using System.Collections.Generic;

namespace WordFlow.Models
{
    /// <summary>
    /// 修正记录 - 用户纠正识别错误的数据（训练词典的关键）
    /// </summary>
    public class CorrectionLog
    {
        /// <summary>
        /// 记录ID
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// 关联的输入历史ID
        /// </summary>
        public Guid InputHistoryId { get; set; }
        
        /// <summary>
        /// 修正时间
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 识别错误的词（原文）
        /// </summary>
        public string WrongWord { get; set; } = "";
        
        /// <summary>
        /// 用户修正后的正确词
        /// </summary>
        public string CorrectWord { get; set; } = "";
        
        /// <summary>
        /// 错误词的拼音
        /// </summary>
        public string WrongPinyin { get; set; } = "";
        
        /// <summary>
        /// 正确词的拼音
        /// </summary>
        public string CorrectPinyin { get; set; } = "";
        
        /// <summary>
        /// 完整原始句子
        /// </summary>
        public string OriginalSentence { get; set; } = "";
        
        /// <summary>
        /// 完整修正后句子
        /// </summary>
        public string CorrectedSentence { get; set; } = "";
        
        /// <summary>
        /// 上下文（错误词前后的词）
        /// </summary>
        public string ContextBefore { get; set; } = "";
        public string ContextAfter { get; set; } = "";
        
        /// <summary>
        /// 错误类型分析
        /// </summary>
        public ErrorType ErrorType { get; set; }
        
        /// <summary>
        /// 是否已用于训练
        /// </summary>
        public bool IsUsedForTraining { get; set; } = false;
        
        /// <summary>
        /// 训练后生成的个人词典条目ID
        /// </summary>
        public Guid? GeneratedVocabularyId { get; set; }
    }
    
    /// <summary>
    /// 错误类型分析 - 帮助AI理解为什么会错
    /// </summary>
    public enum ErrorType
    {
        Unknown,            // 未知
        Homophone,          // 同音字错误（部署 vs 步骤）
        SimilarSound,       // 近音字错误（谯城区 vs 乔城区）
        RareWord,           // 生僻字（医学/法律术语）
        Name,               // 人名识别错误
        Place,              // 地名识别错误
        ProfessionalTerm,   // 专业术语
        Acronym,            // 缩写词（AI vs 爱）
        Number,             // 数字/日期
        Punctuation,        // 标点问题
    }
}
