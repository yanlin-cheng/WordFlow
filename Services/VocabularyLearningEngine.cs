using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using WordFlow.Models;
using TinyPinyin;

#pragma warning disable IDE0005

namespace WordFlow.Services
{
    /// <summary>
    /// 词汇学习引擎 - 从输入历史和修正记录中自动学习个人词典
    /// </summary>
    public class VocabularyLearningEngine
    {
        private readonly HistoryService _historyService;
        private object? _segmenter;
        private bool _segmenterInitialized = false;
        private bool _useJieba = false;
        
        // 常见停用词（不加入词典）
        private readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "的", "了", "是", "在", "我", "有", "和", "就", "不", "人",
            "都", "一", "一个", "上", "也", "很", "到", "说", "要", "去",
            "你", "会", "着", "没有", "看", "好", "自己", "这", "那",
            "可以", "这个", "那个", "什么", "怎么", "这么", "那么", "一些"
        };
        
        public VocabularyLearningEngine(HistoryService historyService)
        {
            _historyService = historyService;
        }
        
        /// <summary>
        /// 延迟初始化分词器（使用反射避免静态构造函数错误）
        /// </summary>
        private void InitializeSegmenter()
        {
            if (_segmenterInitialized) return;
            
            try
            {
                // 使用反射加载 Jieba，避免静态构造函数在类加载时失败
                var jiebaAssembly = Assembly.Load("JiebaNet.Segmenter");
                var segmenterType = jiebaAssembly.GetType("JiebaNet.Segmenter.JiebaSegmenter");
                
                if (segmenterType != null)
                {
                    _segmenter = Activator.CreateInstance(segmenterType);
                    _useJieba = true;
                    System.Diagnostics.Debug.WriteLine("Jieba 分词器加载成功");
                }
            }
            catch (Exception ex)
            {
                // 如果 Jieba 初始化失败，使用备用方案
                System.Diagnostics.Debug.WriteLine($"Jieba 初始化失败，将使用备用方案: {ex.Message}");
                _useJieba = false;
            }
            
            _segmenterInitialized = true;
        }
        
        /// <summary>
        /// 分词方法（支持备用方案）
        /// </summary>
        private List<string> CutWords(string text)
        {
            InitializeSegmenter();
            
            if (_useJieba && _segmenter != null)
            {
                try
                {
                    // 使用反射调用 Cut 方法
                    var segmenterType = _segmenter.GetType();
                    var cutMethod = segmenterType.GetMethod("Cut", new[] { typeof(string) });
                    
                    if (cutMethod != null)
                    {
                        var result = cutMethod.Invoke(_segmenter, new object[] { text });
                        if (result is IEnumerable<string> words)
                        {
                            return words.ToList();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Jieba 分词失败: {ex.Message}");
                }
            }
            
            // 备用方案：简单的基于字典的分词
            return SimpleCut(text);
        }
        
        /// <summary>
        /// 简单的分词备用方案（基于字符和常见模式）
        /// </summary>
        private List<string> SimpleCut(string text)
        {
            var words = new List<string>();
            var currentWord = new System.Text.StringBuilder();
            
            foreach (char c in text)
            {
                // 中文字符（4字节范围）
                if (c >= 0x4e00 && c <= 0x9fa5)
                {
                    if (currentWord.Length > 0)
                    {
                        words.Add(currentWord.ToString());
                        currentWord.Clear();
                    }
                    words.Add(c.ToString());
                }
                // 英文字母和数字
                else if (char.IsLetterOrDigit(c))
                {
                    currentWord.Append(c);
                }
                // 其他字符（标点、空格等）作为分隔符
                else
                {
                    if (currentWord.Length > 0)
                    {
                        words.Add(currentWord.ToString());
                        currentWord.Clear();
                    }
                }
            }
            
            if (currentWord.Length > 0)
            {
                words.Add(currentWord.ToString());
            }
            
            // 合并相邻的单字，尝试形成多字词
            return MergeSingleChars(words);
        }
        
        /// <summary>
        /// 合并相邻单字形成可能的多字词
        /// </summary>
        private List<string> MergeSingleChars(List<string> words)
        {
            var result = new List<string>();
            int i = 0;
            
            while (i < words.Count)
            {
                // 如果是中文字符，尝试合并相邻的2-4个字
                if (words[i].Length == 1 && IsChineseChar(words[i][0]))
                {
                    var merged = new System.Text.StringBuilder();
                    int j = i;
                    while (j < words.Count && j < i + 4 && 
                           words[j].Length == 1 && IsChineseChar(words[j][0]))
                    {
                        merged.Append(words[j]);
                        j++;
                    }
                    
                    var mergedStr = merged.ToString();
                    if (mergedStr.Length >= 2)
                    {
                        result.Add(mergedStr);
                    }
                    else
                    {
                        result.Add(words[i]);
                    }
                    i = j;
                }
                else
                {
                    result.Add(words[i]);
                    i++;
                }
            }
            
            return result;
        }
        
        private bool IsChineseChar(char c) => c >= 0x4e00 && c <= 0x9fa5;
        
        #region 核心学习流程
        
        /// <summary>
        /// 执行一次完整的学习迭代
        /// </summary>
        public async Task<LearningResult> LearnAsync(int batchSize = 100)
        {
            var result = new LearningResult();
            
            // 1. 学习输入历史中的高频词
            var historyResult = await LearnFromHistoryAsync(batchSize);
            result.LearnedFromHistory = historyResult.NewVocabularies;
            result.UpdatedFromHistory = historyResult.UpdatedVocabularies;
            
            // 2. 学习修正记录
            var correctionResult = await LearnFromCorrectionsAsync(batchSize);
            result.LearnedFromCorrections = correctionResult.NewVocabularies;
            result.GeneratedRules = correctionResult.NewRules;
            
            result.TotalLearned = result.LearnedFromHistory.Count 
                + result.LearnedFromCorrections.Count;
            result.Success = true;
            
            return result;
        }
        
        /// <summary>
        /// 自动学习 - 从所有未处理的历史记录中学习
        /// 用于定期后台学习，无需用户手动触发
        /// </summary>
        /// <param name="maxBatchSize">最大处理记录数，默认 500</param>
        /// <returns>学习结果</returns>
        public async Task<LearningResult> AutoLearnAsync(int maxBatchSize = 500)
        {
            var result = new LearningResult();
            
            try
            {
                // 1. 学习输入历史中的高频词
                var historyResult = await LearnFromHistoryAsync(maxBatchSize);
                result.LearnedFromHistory = historyResult.NewVocabularies;
                result.UpdatedFromHistory = historyResult.UpdatedVocabularies;
                
                // 2. 学习修正记录
                var correctionResult = await LearnFromCorrectionsAsync(maxBatchSize);
                result.LearnedFromCorrections = correctionResult.NewVocabularies;
                result.GeneratedRules = correctionResult.NewRules;
                
                result.TotalLearned = result.LearnedFromHistory.Count 
                    + result.LearnedFromCorrections.Count;
                result.Success = true;
                
                // 3. 如果学习了新词，导出热词文件到 ASR 服务目录
                if (result.TotalLearned > 0)
                {
                    await ExportHotwordsForASRAsync();
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"[VocabularyLearningEngine] AutoLearnAsync 失败：{ex}");
            }
            
            return result;
        }
        
        /// <summary>
        /// 导出热词文件到 ASR 服务目录
        /// </summary>
        private async Task ExportHotwordsForASRAsync()
        {
            try
            {
                // 获取 ASR 服务目录（假设在 PythonASR 目录下）
                var appPath = AppContext.BaseDirectory;
                var pythonAsrPath = System.IO.Path.Combine(appPath, "..", "..", "PythonASR");
                
                if (!System.IO.Directory.Exists(pythonAsrPath))
                {
                    pythonAsrPath = System.IO.Path.Combine(appPath, "PythonASR");
                }
                
                if (!System.IO.Directory.Exists(pythonAsrPath))
                {
                    System.Diagnostics.Debug.WriteLine("[VocabularyLearningEngine] 未找到 PythonASR 目录");
                    return;
                }
                
                var hotwordsPath = System.IO.Path.Combine(pythonAsrPath, "hotwords.txt");
                
                // 导出热词
                await _historyService.ExportHotwordsFileAsync(hotwordsPath);
                
                System.Diagnostics.Debug.WriteLine($"[VocabularyLearningEngine] 热词已导出到：{hotwordsPath}");
                System.Diagnostics.Debug.WriteLine("[VocabularyLearningEngine] 请重启 ASR 服务以加载新热词");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VocabularyLearningEngine] 导出热词失败：{ex.Message}");
            }
        }
        
        #endregion
        
        #region 从输入历史学习
        
        /// <summary>
        /// 分析输入历史，提取高频专业词汇
        /// </summary>
        private async Task<HistoryLearningResult> LearnFromHistoryAsync(int count)
        {
            var result = new HistoryLearningResult();
            
            // 获取未处理的输入历史
            var histories = await _historyService.GetUnprocessedHistoryAsync(count);
            
            // 统计所有词的频率
            var wordFrequency = new Dictionary<string, WordStats>();
            
            foreach (var history in histories)
            {
                var text = history.FinalText;
                
                // 分词（使用备用方案兼容 Jieba 初始化失败的情况）
                var words = CutWords(text);
                
                foreach (var word in words)
                {
                    // 过滤条件
                    if (ShouldSkipWord(word)) continue;
                    
                    // 更新统计
                    if (!wordFrequency.ContainsKey(word))
                    {
                        wordFrequency[word] = new WordStats 
                        { 
                            Word = word,
                            Pinyin = PinyinHelper.GetPinyin(word, "").ToLower()
                        };
                    }
                    
                    var stats = wordFrequency[word];
                    stats.Frequency++;
                    stats.LastUsed = history.Timestamp;
                    stats.Contexts.Add(GetContext(text, word));
                    
                    // 记录输入场景
                    if (history.TargetApplication != null)
                    {
                        stats.Applications.Add(history.TargetApplication);
                    }
                }
                
                // 标记已处理
                await _historyService.MarkAsTrainedAsync(history.Id);
            }
            
            // 筛选高频词加入词典
            var threshold = Math.Max(2, histories.Count / 20); // 至少出现2次，或超过5%
            
            foreach (var kvp in wordFrequency.Where(x => x.Value.Frequency >= threshold))
            {
                var stats = kvp.Value;
                
                // 检查是否已存在
                var existing = await _historyService.GetVocabularyByWordAsync(stats.Word);
                
                if (existing == null)
                {
                    // 新词
                    var vocab = new PersonalVocabulary
                    {
                        Word = stats.Word,
                        Pinyin = stats.Pinyin,
                        Frequency = stats.Frequency,
                        Weight = CalculateInitialWeight(stats),
                        Category = InferCategory(stats),
                        Contexts = stats.Contexts.Distinct().Take(5).ToList(),
                        ConfusableWords = new List<string>(),
                        Source = VocabularySource.AutoLearned,
                        RelatedHistoryIds = new List<Guid>() // 可以关联具体历史
                    };
                    
                    await _historyService.UpsertVocabularyAsync(vocab);
                    result.NewVocabularies.Add(vocab);
                }
                else
                {
                    // 更新已有词
                    existing.Frequency += stats.Frequency;
                    existing.LastUsed = stats.LastUsed;
                    existing.Weight = Math.Min(existing.Weight + 0.1, 3.0); // 权重递增但封顶
                    existing.Contexts = existing.Contexts
                        .Union(stats.Contexts)
                        .Distinct()
                        .Take(10)
                        .ToList();
                    
                    await _historyService.UpsertVocabularyAsync(existing);
                    result.UpdatedVocabularies.Add(existing);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 判断是否应该跳过这个词
        /// </summary>
        private bool ShouldSkipWord(string word)
        {
            // 太短或太长
            if (word.Length < 2 || word.Length > 8) return true;
            
            // 停用词
            if (_stopWords.Contains(word)) return true;
            
            // 纯数字
            if (word.All(char.IsDigit)) return true;
            
            // 纯英文（可能是代码，单独处理）
            if (word.All(c => char.IsLetter(c) && c <= 127)) return true;
            
            // 包含特殊字符
            if (word.Any(c => !char.IsLetterOrDigit(c) && c != '_')) return true;
            
            return false;
        }
        
        /// <summary>
        /// 获取词的上下文
        /// </summary>
        private string GetContext(string text, string word, int windowSize = 5)
        {
            var index = text.IndexOf(word);
            if (index < 0) return "";
            
            var start = Math.Max(0, index - windowSize);
            var end = Math.Min(text.Length, index + word.Length + windowSize);
            
            return text.Substring(start, end - start);
        }
        
        /// <summary>
        /// 计算初始权重
        /// </summary>
        private double CalculateInitialWeight(WordStats stats)
        {
            double weight = 1.0;
            
            // 频率加成
            weight += Math.Min(stats.Frequency * 0.1, 1.0);
            
            // 长度加成（专业术语通常较长）
            if (stats.Word.Length >= 4) weight += 0.3;
            
            // 生僻字加成
            if (stats.Word.Any(c => c > 0x4e00 && c < 0x9fa5 && IsRareChar(c))) 
            {
                weight += 0.5;
            }
            
            return Math.Min(weight, 2.5);
        }
        
        /// <summary>
        /// 推断词汇类别
        /// </summary>
        private VocabularyCategory InferCategory(WordStats stats)
        {
            var apps = string.Join(" ", stats.Applications).ToLower();
            var contexts = string.Join(" ", stats.Contexts);
            
            // 根据应用场景判断
            if (apps.Contains("word") || apps.Contains("doc") || apps.Contains("wps"))
            {
                if (contexts.Contains("法院") || contexts.Contains("诉讼") || contexts.Contains("合同"))
                    return VocabularyCategory.Legal;
                if (contexts.Contains("医院") || contexts.Contains("病历") || contexts.Contains("诊断"))
                    return VocabularyCategory.Medical;
                if (contexts.Contains("公司") || contexts.Contains("项目") || contexts.Contains("客户"))
                    return VocabularyCategory.Business;
            }
            
            if (apps.Contains("code") || apps.Contains("vs") || apps.Contains("ide"))
            {
                return VocabularyCategory.Programming;
            }
            
            // 根据词本身判断
            if (stats.Word.EndsWith("症") || stats.Word.EndsWith("炎") || stats.Word.EndsWith("病"))
                return VocabularyCategory.Medical;
            
            if (stats.Word.EndsWith("法") || stats.Word.EndsWith("律") || stats.Word.EndsWith("权"))
                return VocabularyCategory.Legal;
            
            return VocabularyCategory.General;
        }
        
        private bool IsRareChar(char c)
        {
            // 简单的生僻字判断：不在常用3500字内
            // 实际实现可以用更完善的字频表
            var commonChars = "的一是在不了有和人这中大为上个国我以要他时来用们生到作地于出就分对成会可主发年动同工也能下过子说产种面而方后多定行学法所民得经十三之进着等部度家电力里如水化高自二理起小物现实加量都两体制机当使点从业本去把性好应开它合还因由其些然前外天政四日那社义事平形相全表间样与关各重新线内数正心反你明看原又么利比或但质气第向道命此变条只没结解问意建月公无系军很情者最立代想已通并提直题党程展五果料象员革位入常文总次品式活设及管特件长求老头基资边流路级少图山统接知较将组见计别她手角期根论运农指几九区强放决西被干做必战先回则任取完举色或";
            return !commonChars.Contains(c);
        }
        
        #endregion
        
        #region 从修正记录学习
        
        /// <summary>
        /// 分析修正记录，学习错误模式
        /// </summary>
        private async Task<CorrectionLearningResult> LearnFromCorrectionsAsync(int count)
        {
            var result = new CorrectionLearningResult();
            
            var corrections = await _historyService.GetUnprocessedCorrectionsAsync(count);
            
            foreach (var correction in corrections)
            {
                // 分析错误类型
                var errorType = AnalyzeErrorType(correction);
                correction.ErrorType = errorType;
                
                // 确保正确词在词典中（高权重）
                var correctVocab = await _historyService.GetVocabularyByWordAsync(correction.CorrectWord);
                if (correctVocab == null)
                {
                    correctVocab = new PersonalVocabulary
                    {
                        Word = correction.CorrectWord,
                        Pinyin = correction.CorrectPinyin,
                        Weight = 2.5, // 用户明确纠正的词，给高权重
                        Category = InferCategoryFromCorrection(correction),
                        ConfusableWords = new List<string> { correction.WrongWord },
                        Contexts = new List<string>(),
                        RelatedHistoryIds = new List<Guid>(),
                        Source = VocabularySource.AutoLearned
                    };
                    result.NewVocabularies.Add(correctVocab);
                }
                else
                {
                    correctVocab.Weight = Math.Min(correctVocab.Weight + 0.5, 3.0);
                    if (!correctVocab.ConfusableWords.Contains(correction.WrongWord))
                    {
                        correctVocab.ConfusableWords.Add(correction.WrongWord);
                    }
                }
                
                await _historyService.UpsertVocabularyAsync(correctVocab);
                
                // 生成纠错规则
                var rule = GenerateRuleFromCorrection(correction);
                if (rule != null)
                {
                    result.NewRules.Add(rule);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 分析错误类型
        /// </summary>
        private ErrorType AnalyzeErrorType(CorrectionLog correction)
        {
            // 拼音完全相同 -> 同音字错误
            if (correction.WrongPinyin == correction.CorrectPinyin)
                return ErrorType.Homophone;
            
            // 拼音相似 -> 近音字错误
            if (IsSimilarPinyin(correction.WrongPinyin, correction.CorrectPinyin))
                return ErrorType.SimilarSound;
            
            // 医学名词
            if (IsMedicalTerm(correction.CorrectWord))
                return ErrorType.ProfessionalTerm;
            
            // 人名（2-3字，不常见）
            if (correction.CorrectWord.Length <= 3 && IsRareChar(correction.CorrectWord[0]))
                return ErrorType.Name;
            
            return ErrorType.Unknown;
        }
        
        private bool IsSimilarPinyin(string pinyin1, string pinyin2)
        {
            // 简单的相似度计算：编辑距离
            if (string.IsNullOrEmpty(pinyin1) || string.IsNullOrEmpty(pinyin2)) 
                return false;
            
            var dist = ComputeLevenshteinDistance(pinyin1, pinyin2);
            return dist <= 2 && dist < Math.Max(pinyin1.Length, pinyin2.Length) / 2;
        }
        
        private bool IsMedicalTerm(string word)
        {
            var medicalSuffixes = new[] { "症", "炎", "病", "瘤", "癌", "炎", "综合征", "衰竭", "梗死" };
            return medicalSuffixes.Any(s => word.Contains(s));
        }
        
        private VocabularyCategory InferCategoryFromCorrection(CorrectionLog correction)
        {
            if (IsMedicalTerm(correction.CorrectWord)) return VocabularyCategory.Medical;
            if (correction.CorrectWord.Contains("公司") || correction.CorrectWord.Contains("集团")) return VocabularyCategory.Organization;
            return VocabularyCategory.General;
        }
        
        private Services.CorrectionRule? GenerateRuleFromCorrection(CorrectionLog correction)
        {
            // 只有特定类型的错误才生成规则
            if (correction.ErrorType != ErrorType.Homophone && 
                correction.ErrorType != ErrorType.SimilarSound)
                return null;
            
            return new Services.CorrectionRule
            {
                WrongPattern = correction.WrongWord,
                CorrectPattern = correction.CorrectWord,
                ContextPattern = correction.ContextBefore + "_" + correction.ContextAfter,
                ErrorType = correction.ErrorType,
                Confidence = 0.8
            };
        }
        
        private int ComputeLevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];
            
            if (n == 0) return m;
            if (m == 0) return n;
            
            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;
            
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            
            return d[n, m];
        }
        
        #endregion
    }
    
    #region 辅助类
    
    public class LearningResult
    {
        public bool Success { get; set; }
        public int TotalLearned { get; set; }
        public string? ErrorMessage { get; set; }
        public List<PersonalVocabulary> LearnedFromHistory { get; set; } = new();
        public List<PersonalVocabulary> UpdatedFromHistory { get; set; } = new();
        public List<PersonalVocabulary> LearnedFromCorrections { get; set; } = new();
        public List<Services.CorrectionRule> GeneratedRules { get; set; } = new();
    }
    
    public class HistoryLearningResult
    {
        public List<PersonalVocabulary> NewVocabularies { get; set; } = new();
        public List<PersonalVocabulary> UpdatedVocabularies { get; set; } = new();
    }
    
    public class CorrectionLearningResult
    {
        public List<PersonalVocabulary> NewVocabularies { get; set; } = new();
        public List<Services.CorrectionRule> NewRules { get; set; } = new();
    }
    
    public class WordStats
    {
        public string Word { get; set; } = "";
        public string Pinyin { get; set; } = "";
        public int Frequency { get; set; }
        public DateTime LastUsed { get; set; }
        public List<string> Contexts { get; set; } = new();
        public HashSet<string> Applications { get; set; } = new();
    }
    
    #endregion
}
