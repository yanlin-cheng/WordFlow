using System;
using System.Collections.Generic;

namespace WordFlow.Models
{
    /// <summary>
    /// 个人词典条目 - 用户的专业词汇
    /// </summary>
    public class PersonalVocabulary
    {
        /// <summary>
        /// 词汇ID
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// 词汇本身
        /// </summary>
        public string Word { get; set; } = "";
        
        /// <summary>
        /// 词汇拼音（用于音似词匹配）
        /// </summary>
        public string Pinyin { get; set; } = "";
        
        /// <summary>
        /// 使用频率
        /// </summary>
        public int Frequency { get; set; } = 1;
        
        /// <summary>
        /// 首次使用时间
        /// </summary>
        public DateTime FirstUsed { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 最后使用时间
        /// </summary>
        public DateTime LastUsed { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 语音识别权重（越高越优先匹配）
        /// </summary>
        public double Weight { get; set; } = 1.0;
        
        /// <summary>
        /// 词汇分类
        /// </summary>
        public VocabularyCategory Category { get; set; } = VocabularyCategory.General;
        
        /// <summary>
        /// 常见上下文（帮助上下文感知）
        /// </summary>
        public List<string> Contexts { get; set; } = new();
        
        /// <summary>
        /// 易混淆词（哪些词容易识别错成这个词）
        /// </summary>
        public List<string> ConfusableWords { get; set; } = new();
        
        /// <summary>
        /// 来源：手动添加 / 自动学习 / AI生成
        /// </summary>
        public VocabularySource Source { get; set; } = VocabularySource.AutoLearned;
        
        /// <summary>
        /// 是否已同步到服务端
        /// </summary>
        public bool IsSynced { get; set; } = false;
        
        /// <summary>
        /// 关联的输入历史ID列表
        /// </summary>
        public List<Guid> RelatedHistoryIds { get; set; } = new();
        
        /// <summary>
        /// 计算动态权重（基于频率、时效性、来源）
        /// 用于导出热词文件供 ASR 服务使用
        /// </summary>
        public double CalculateDynamicWeight()
        {
            // 基础权重：根据来源设置不同的基础值
            double weight = Weight;
            
            // 来源加成：手动添加的词汇权重更高
            double sourceBonus = Source switch
            {
                VocabularySource.Manual => 5.0,      // 手动添加：+5.0 基础权重
                VocabularySource.AIGenerated => 3.0, // AI 生成：+3.0 基础权重
                VocabularySource.Imported => 2.0,    // 导入：+2.0 基础权重
                VocabularySource.AutoLearned => 0.0, // 自动学习：无加成
                _ => 0.0
            };
            weight += sourceBonus;
            
            // 频率加成：使用越多权重越高（封顶 5 倍）
            double freqBonus = Math.Min(Frequency * 0.1, 5.0);
            weight += freqBonus;
            
            // 时效性衰减：很久没用的词权重降低
            var daysSinceLastUse = (DateTime.Now - LastUsed).TotalDays;
            if (daysSinceLastUse > 30)
            {
                weight *= 0.9; // 30 天未用衰减 10%
            }
            if (daysSinceLastUse > 90)
            {
                weight *= 0.7; // 90 天未用衰减 30%
            }
            
            // 手动添加的词汇最低权重保证
            double minWeight = Source == VocabularySource.Manual ? 8.0 : 0.5;
            return Math.Max(weight, minWeight);
        }
    }
    
    /// <summary>
    /// 词汇分类
    /// </summary>
    public enum VocabularyCategory
    {
        General,        // 通用
        Medical,        // 医疗
        Legal,          // 法律
        Programming,    // 编程/技术
        Business,       // 商务
        Academic,       // 学术
        Name,           // 人名
        Place,          // 地名
        Organization,   // 组织名
        Product,        // 产品名
    }
    
    /// <summary>
    /// 词汇来源
    /// </summary>
    public enum VocabularySource
    {
        Manual,         // 用户手动添加
        AutoLearned,    // 系统自动学习
        AIGenerated,    // AI智能生成
        Imported,       // 从外部导入
    }
}
