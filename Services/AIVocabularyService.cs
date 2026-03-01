using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WordFlow.Models;
using WordFlow.Utils;

namespace WordFlow.Services
{
    /// <summary>
    /// AI智能词典服务 - 利用AI分析用户输入历史，生成个性化词典
    /// 这是一个高级功能，可作为收费点
    /// </summary>
    public class AIVocabularyService
    {
        private readonly HistoryService _historyService;
        private readonly string _aiApiKey; // 用户的OpenAI API Key或我们的服务端API
        
        public AIVocabularyService(HistoryService historyService, string? aiApiKey = null)
        {
            _historyService = historyService;
            _aiApiKey = aiApiKey ?? "";
        }
        
        #region 核心功能：AI分析生成词典
        
        /// <summary>
        /// AI分析用户输入历史，智能生成专业词典
        /// 【收费功能】
        /// </summary>
        public async Task<AIAnalysisResult> AnalyzeUserInputHistoryAsync(int days = 30)
        {
            // 1. 获取最近N天的输入历史
            var recentHistory = await _historyService.GetRecentHistoryAsync(1000);
            var cutoffDate = DateTime.Now.AddDays(-days);
            var filteredHistory = recentHistory.Where(h => h.Timestamp > cutoffDate).ToList();
            
            if (filteredHistory.Count < 10)
            {
                return new AIAnalysisResult 
                { 
                    Success = false, 
                    Message = "输入历史太少，至少需要10条记录才能分析" 
                };
            }
            
            // 2. 提取所有文本
            var allTexts = filteredHistory.Select(h => h.FinalText).ToList();
            var combinedText = string.Join("\n", allTexts);
            
            // 3. 调用AI分析
            var analysis = await CallAIForAnalysis(combinedText);
            
            // 4. 根据AI建议生成词典条目
            var generatedVocabularies = new List<PersonalVocabulary>();
            
            foreach (var item in analysis.SuggestedTerms)
            {
                // 跳过无效数据
                if (string.IsNullOrWhiteSpace(item.Term)) continue;
                
                var vocab = new PersonalVocabulary
                {
                    Word = item.Term.Trim(),
                    Pinyin = item.Pinyin ?? GetPinyin(item.Term),
                    Category = item.Category,
                    Weight = Math.Max(0.5, item.ImportanceScore * 2.0), // AI认为重要的词，给更高权重
                    Source = VocabularySource.AIGenerated,
                    Contexts = new List<string>(item.CommonContexts ?? Array.Empty<string>()),
                    ConfusableWords = new List<string>(item.PotentialConfusions ?? Array.Empty<string>()),
                    RelatedHistoryIds = new List<Guid>()
                };
                
                await _historyService.UpsertVocabularyAsync(vocab);
                generatedVocabularies.Add(vocab);
            }
            
            return new AIAnalysisResult
            {
                Success = true,
                AnalyzedRecords = filteredHistory.Count,
                GeneratedTerms = generatedVocabularies.Count,
                SuggestedCategory = analysis.PrimaryDomain,
                GeneratedVocabularies = generatedVocabularies,
                Insights = analysis.Insights
            };
        }
        
        /// <summary>
        /// 调用AI分析API（可以对接OpenAI、Claude或我们自己的服务）
        /// </summary>
        private async Task<AIAnalysisResponse> CallAIForAnalysis(string textSample)
        {
            // 构建Prompt
            var prompt = $@"
你是一位专业的词汇分析专家。请分析以下用户的语音输入文本，提取出：

1. 专业术语（医学、法律、技术等领域的专业词汇）
2. 高频出现的特定名词（人名、地名、公司名、产品名）
3. 容易识别错误的同音词/近音词
4. 用户的输入领域分类

文本样本（共{ textSample.Split('\n').Length }条）：
---
{textSample.Substring(0, Math.Min(textSample.Length, 5000))}
---

请以JSON格式返回分析结果：
{{
    ""primaryDomain"": ""医疗|法律|编程|商务|教育|其他"",
    ""suggestedTerms"": [
        {{
            ""term"": ""词汇"",
            ""pinyin"": ""拼音"",
            ""category"": ""Medical|Legal|Programming|Name|Place|Product"",
            ""importanceScore"": 0.95,
            ""commonContexts"": [""上下文1"", ""上下文2""],
            ""potentialConfusions"": [""易混淆词1"", ""易混淆词2""]
        }}
    ],
    ""insights"": ""分析洞察：用户的输入特点、领域、建议""
}}
";

            // 这里可以对接OpenAI API或其他AI服务
            // 暂时返回模拟数据
            return await MockAIResponse(textSample);
        }
        
        /// <summary>
        /// 智能词汇提取算法（本地实现，无需API）
        /// </summary>
        private async Task<AIAnalysisResponse> MockAIResponse(string text)
        {
            var terms = new List<AITermSuggestion>();
            var allTexts = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            // 1. 提取重复出现的词组（2-4字）
            var wordFreq = ExtractWordFrequencies(text);
            
            // 2. 提取专业术语模式
            var professionalTerms = ExtractProfessionalTerms(text, wordFreq);
            terms.AddRange(professionalTerms);
            
            // 3. 提取可能的姓名（不常见的2-3字组合）
            var nameTerms = ExtractPotentialNames(text, wordFreq);
            terms.AddRange(nameTerms);
            
            // 4. 提取英文术语
            var englishTerms = ExtractEnglishTerms(text);
            terms.AddRange(englishTerms);
            
            // 去重
            terms = terms.GroupBy(t => t.Term)
                         .Select(g => g.First())
                         .Where(t => !IsCommonStopWord(t.Term))
                         .ToList();
            
            // 确定主要领域
            var primaryDomain = InferPrimaryDomain(terms);
            
            await Task.Delay(500); // 模拟处理延迟
            
            return new AIAnalysisResponse
            {
                PrimaryDomain = primaryDomain,
                SuggestedTerms = terms.Take(50).ToList(), // 最多返回50个
                Insights = $"分析了{allTexts.Length}条记录，发现{terms.Count}个潜在专业词汇。" +
                          $"主要领域：{primaryDomain}。建议将这些词汇加入个人词典以提高识别准确率。"
            };
        }
        
        /// <summary>
        /// 提取词频统计（2-4字词组）
        /// </summary>
        private Dictionary<string, int> ExtractWordFrequencies(string text)
        {
            var freq = new Dictionary<string, int>();
            var lines = text.Split('\n');
            
            foreach (var line in lines)
            {
                // 提取2字词
                for (int i = 0; i < line.Length - 1; i++)
                {
                    var word = line.Substring(i, 2);
                    if (IsValidChineseWord(word))
                    {
                        freq[word] = freq.GetValueOrDefault(word) + 1;
                    }
                }
                
                // 提取3字词
                for (int i = 0; i < line.Length - 2; i++)
                {
                    var word = line.Substring(i, 3);
                    if (IsValidChineseWord(word))
                    {
                        freq[word] = freq.GetValueOrDefault(word) + 1;
                    }
                }
                
                // 提取4字词
                for (int i = 0; i < line.Length - 3; i++)
                {
                    var word = line.Substring(i, 4);
                    if (IsValidChineseWord(word))
                    {
                        freq[word] = freq.GetValueOrDefault(word) + 1;
                    }
                }
            }
            
            return freq;
        }
        
        /// <summary>
        /// 提取专业术语
        /// </summary>
        private List<AITermSuggestion> ExtractProfessionalTerms(string text, Dictionary<string, int> wordFreq)
        {
            var terms = new List<AITermSuggestion>();
            
            // 专业术语后缀模式
            var medicalSuffixes = new[] { "症", "炎", "病", "瘤", "癌", "药", "素", "酶", "蛋白", "细胞", "基因", "病毒", "菌", "手术", "治疗", "诊断", "综合征", "衰竭", "梗死", "硬化" };
            var legalSuffixes = new[] { "法", "律", "权", "罪", "案", "诉讼", "仲裁", "合同", "协议", "条款", "规定", "章程", "证据", "判决", "裁定", "原告", "被告" };
            var techSuffixes = new[] { "系统", "平台", "算法", "模型", "数据", "代码", "程序", "接口", "模块", "架构", "协议", "引擎", "框架", "库", "函数", "变量", "数据库", "服务器", "客户端" };
            var businessSuffixes = new[] { "公司", "集团", "企业", "产品", "项目", "客户", "市场", "销售", "运营", "管理", "战略", "投资", "融资", "股份", "股东", "董事会", "CEO", "经理" };
            
            foreach (var kvp in wordFreq.Where(x => x.Value >= 2)) // 至少出现2次
            {
                var word = kvp.Key;
                var freq = kvp.Value;
                
                VocabularyCategory? category = null;
                double importance = 0.5;
                var confusions = new List<string>();
                
                // 医疗术语
                if (medicalSuffixes.Any(s => word.Contains(s)))
                {
                    category = VocabularyCategory.Medical;
                    importance = 0.9;
                }
                // 法律术语
                else if (legalSuffixes.Any(s => word.Contains(s)))
                {
                    category = VocabularyCategory.Legal;
                    importance = 0.85;
                }
                // 技术术语
                else if (techSuffixes.Any(s => word.Contains(s)))
                {
                    category = VocabularyCategory.Programming;
                    importance = 0.8;
                }
                // 商务术语
                else if (businessSuffixes.Any(s => word.Contains(s)))
                {
                    category = VocabularyCategory.Business;
                    importance = 0.75;
                }
                
                // 如果频次较高（>3次）或者是长词（3-4字），也可能是专业术语
                if (category == null && (freq >= 3 || word.Length >= 3))
                {
                    category = VocabularyCategory.General;
                    importance = 0.6 + Math.Min(freq * 0.05, 0.2);
                }
                
                if (category.HasValue && !IsCommonStopWord(word))
                {
                    // 生成可能的混淆词（同音/近音）
                    confusions = GenerateConfusions(word);
                    
                    terms.Add(new AITermSuggestion
                    {
                        Term = word,
                        Pinyin = GetPinyin(word),
                        Category = category.Value,
                        ImportanceScore = Math.Min(importance, 0.95),
                        CommonContexts = ExtractContexts(text, word),
                        PotentialConfusions = confusions.ToArray()
                    });
                }
            }
            
            return terms;
        }
        
        /// <summary>
        /// 提取可能的姓名
        /// </summary>
        private List<AITermSuggestion> ExtractPotentialNames(string text, Dictionary<string, int> wordFreq)
        {
            var terms = new List<AITermSuggestion>();
            var namePatterns = new[] { "先生", "女士", "医生", "教授", "经理", "总", "老师" };
            
            foreach (var kvp in wordFreq.Where(x => x.Value >= 1))
            {
                var word = kvp.Key;
                
                // 2-3字，且前面或后面有称谓词
                if (word.Length >= 2 && word.Length <= 3 && IsChineseName(word))
                {
                    // 检查上下文是否有称谓
                    var hasTitle = namePatterns.Any(p => text.Contains(word + p) || text.Contains(p + word));
                    
                    if (hasTitle || kvp.Value == 1) // 独特的词可能是人名
                    {
                        terms.Add(new AITermSuggestion
                        {
                            Term = word,
                            Pinyin = GetPinyin(word),
                            Category = VocabularyCategory.Name,
                            ImportanceScore = 0.85,
                            CommonContexts = ExtractContexts(text, word),
                            PotentialConfusions = GenerateConfusions(word).ToArray()
                        });
                    }
                }
            }
            
            return terms;
        }
        
        /// <summary>
        /// 提取英文术语
        /// </summary>
        private List<AITermSuggestion> ExtractEnglishTerms(string text)
        {
            var terms = new List<AITermSuggestion>();
            var words = text.Split(new[] { ' ', '\n', '\t', ',', '.', '，', '。' }, StringSplitOptions.RemoveEmptyEntries);
            
            var englishPattern = new System.Text.RegularExpressions.Regex(@"^[a-zA-Z][a-zA-Z0-9]{2,15}$");
            var commonEnglish = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "the", "and", "for", "are", "but", "not", "you", "all", "can", "had", "her", "was", "one", "our", "out", "day", "get", "has", "him", "his" 
            };
            
            var englishFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var word in words)
            {
                if (englishPattern.IsMatch(word) && !commonEnglish.Contains(word))
                {
                    englishFreq[word] = englishFreq.GetValueOrDefault(word) + 1;
                }
            }
            
            foreach (var kvp in englishFreq.Where(x => x.Value >= 2))
            {
                terms.Add(new AITermSuggestion
                {
                    Term = kvp.Key,
                    Pinyin = kvp.Key.ToLower(),
                    Category = VocabularyCategory.Programming,
                    ImportanceScore = 0.7,
                    CommonContexts = new[] { "技术术语", "英文缩写" },
                    PotentialConfusions = Array.Empty<string>()
                });
            }
            
            return terms;
        }
        
        /// <summary>
        /// 判断是否为有效的中文词
        /// </summary>
        private bool IsValidChineseWord(string word)
        {
            if (string.IsNullOrEmpty(word) || word.Length < 2) return false;
            
            // 至少50%是中文字符
            int chineseCount = word.Count(c => c >= 0x4e00 && c <= 0x9fa5);
            return chineseCount >= word.Length / 2;
        }
        
        /// <summary>
        /// 判断是否为可能的姓名
        /// </summary>
        private bool IsChineseName(string word)
        {
            if (word.Length < 2 || word.Length > 3) return false;
            
            // 所有字符都是中文
            if (!word.All(c => c >= 0x4e00 && c <= 0x9fa5)) return false;
            
            // 排除常见虚词
            var commonWords = new[] { "因为", "所以", "但是", "如果", "虽然", "还是", "或者", "以及", "不仅", "而且" };
            if (commonWords.Contains(word)) return false;
            
            return true;
        }
        
        /// <summary>
        /// 是否为常见停用词
        /// </summary>
        private bool IsCommonStopWord(string word)
        {
            var stopWords = new HashSet<string>
            {
                "可以", "进行", "使用", "通过", "需要", "就是", "这个", "一个", "一下", "如果", "开始", "已经", "作为", "根据", "目前", "表示", "由于", "其中", "主要", "相关", "现在", "时候", "可能", "问题", "情况", "部分", "不同", "最后", "今天", "明天", "昨天", "一些", "一下", "一直", "为了", "这么", "那么", "什么", "怎么", "是不是", "有没有", "能不能"
            };
            return stopWords.Contains(word) || word.All(c => c >= 0x4e00 && c <= 0x9fa5 && "的是在了我有和人这中大为上个国我以要他时来用们生到作地于出就分对成会可主发年动同工也能下过子说产种面而方后多定行学法所民得经十三之进着等部度家电力里如水化高自二理起小物现实加量都两体制机当使点从业本去把性好应开它合还因由其些然前外天政四日那社义事平形相全表间样与关各重新线内数正心反你明看原又么利比或但质气第向道命此变条只没结解问意建月公无系军很情者最立代想已通并提直题党程展五果料象员革位入常文总次品式活设及管特件长求老头基资边流路级少图山统接知较将组见计别她手角期根论运农指几九区强放决西被干做必战先回则任取完举色或".Contains(c));
        }
        
        /// <summary>
        /// 生成可能的混淆词
        /// </summary>
        private List<string> GenerateConfusions(string word)
        {
            var confusions = new List<string>();
            
            // 常见同音字映射
            var homophones = new Dictionary<char, char[]>
            {
                ['长'] = new[] { '常' },
                ['在'] = new[] { '再' },
                ['做'] = new[] { '作' },
                ['的'] = new[] { '得', '地' },
                ['那'] = new[] { '哪' },
                ['他'] = new[] { '她', '它' },
                ['有'] = new[] { '由' },
                ['又'] = new[] { '还' },
                ['以'] = new[] { '已' },
                ['为'] = new[] { '位', '围' }
            };
            
            for (int i = 0; i < word.Length; i++)
            {
                if (homophones.TryGetValue(word[i], out var alternatives))
                {
                    foreach (var alt in alternatives)
                    {
                        var confused = word.Substring(0, i) + alt + word.Substring(i + 1);
                        confusions.Add(confused);
                    }
                }
            }
            
            return confusions.Distinct().Take(3).ToList();
        }
        
        /// <summary>
        /// 提取词的上下文
        /// </summary>
        private string[] ExtractContexts(string text, string word, int maxContexts = 3)
        {
            var contexts = new List<string>();
            int index = 0;
            
            while (index < text.Length && contexts.Count < maxContexts)
            {
                index = text.IndexOf(word, index);
                if (index < 0) break;
                
                int start = Math.Max(0, index - 8);
                int end = Math.Min(text.Length, index + word.Length + 8);
                var context = text.Substring(start, end - start).Replace('\n', ' ').Trim();
                
                if (!string.IsNullOrEmpty(context))
                {
                    contexts.Add(context);
                }
                
                index += word.Length;
            }
            
            return contexts.ToArray();
        }
        
        /// <summary>
        /// 获取拼音（简单实现）
        /// </summary>
        private string GetPinyin(string word)
        {
            // 使用 TinyPinyin 库
            try
            {
                return TinyPinyin.PinyinHelper.GetPinyin(word, " ").ToLower();
            }
            catch
            {
                return word;
            }
        }
        
        /// <summary>
        /// 推断主要领域
        /// </summary>
        private string InferPrimaryDomain(List<AITermSuggestion> terms)
        {
            if (!terms.Any()) return "通用";
            
            var categories = terms.GroupBy(t => t.Category)
                                  .OrderByDescending(g => g.Count())
                                  .Select(g => g.Key)
                                  .FirstOrDefault();
            
            return categories switch
            {
                VocabularyCategory.Medical => "医疗",
                VocabularyCategory.Legal => "法律",
                VocabularyCategory.Programming => "编程/技术",
                VocabularyCategory.Business => "商务",
                VocabularyCategory.Academic => "学术",
                VocabularyCategory.Name => "人名",
                _ => "通用"
            };
        }
        
        #endregion
        
        #region 进阶功能：错误模式学习
        
        /// <summary>
        /// AI分析用户的修正记录，学习错误模式
        /// 【收费功能】
        /// </summary>
        public async Task<ErrorPatternAnalysis> AnalyzeErrorPatternsAsync()
        {
            var corrections = await _historyService.GetUnprocessedCorrectionsAsync(1000);
            
            if (corrections.Count < 5)
            {
                return new ErrorPatternAnalysis 
                { 
                    Message = "修正记录太少，无法分析错误模式" 
                };
            }
            
            // 分组统计错误类型
            var errorGroups = corrections
                .GroupBy(c => c.ErrorType)
                .Select(g => new ErrorTypeStats
                {
                    ErrorType = g.Key,
                    Count = g.Count(),
                    Examples = g.Take(3).ToList()
                })
                .OrderByDescending(s => s.Count)
                .ToList();
            
            // AI生成纠错规则
            var correctionRules = new List<CorrectionRule>();
            
            foreach (var group in errorGroups.Where(g => g.Count >= 3)) // 至少出现3次的错误才生成规则
            {
                var rule = GenerateCorrectionRule(group);
                if (rule != null)
                {
                    correctionRules.Add(rule);
                }
            }
            
            return new ErrorPatternAnalysis
            {
                TotalCorrections = corrections.Count,
                ErrorTypeStats = errorGroups,
                GeneratedRules = correctionRules,
                Recommendations = GenerateRecommendations(errorGroups)
            };
        }
        
        /// <summary>
        /// 生成纠错规则
        /// </summary>
        private CorrectionRule? GenerateCorrectionRule(ErrorTypeStats stats)
        {
            // 提取最常见的错误-正确对
            var commonPair = stats.Examples
                .GroupBy(e => new { e.WrongWord, e.CorrectWord })
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            
            if (commonPair == null) return null;
            
            var example = commonPair.First();
            
            return new CorrectionRule
            {
                WrongPattern = example.WrongWord,
                CorrectPattern = example.CorrectWord,
                ContextPattern = example.ContextBefore + "_" + example.ContextAfter,
                Confidence = Math.Min(stats.Count * 0.1, 0.95),
                ErrorType = stats.ErrorType
            };
        }
        
        private List<string> GenerateRecommendations(List<ErrorTypeStats> stats)
        {
            var recommendations = new List<string>();
            
            var topError = stats.FirstOrDefault();
            if (topError?.ErrorType == ErrorType.Homophone)
            {
                recommendations.Add("您经常遇到同音字错误，建议开启'同音词智能纠错'功能");
            }
            if (topError?.ErrorType == ErrorType.ProfessionalTerm)
            {
                recommendations.Add("检测到大量专业术语，建议导入行业专用词典");
            }
            
            return recommendations;
        }
        
        #endregion
        
        #region 数据导出与共享
        
        /// <summary>
        /// 导出个人词典为可分享的格式
        /// 【收费功能：词典市场】
        /// </summary>
        public async Task<string> ExportVocabularyPackageAsync(string name, string description)
        {
            var vocabList = await _historyService.GetTopVocabularyAsync(500);
            
            var package = new VocabularyPackage
            {
                Name = name,
                Description = description,
                CreatedAt = DateTime.Now,
                Vocabularies = vocabList,
                Statistics = new VocabularyStats
                {
                    TotalWords = vocabList.Count,
                    CategoryDistribution = vocabList
                        .GroupBy(v => v.Category)
                        .ToDictionary(g => g.Key, g => g.Count())
                }
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(package, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            var fileName = $"WordFlow_Vocabulary_{name}_{DateTime.Now:yyyyMMdd}.json";
            var path = Path.Combine(AppPaths.ExportsDirectory, fileName);
            
            await File.WriteAllTextAsync(path, json);
            
            return path;
        }
        
        /// <summary>
        /// 导入他人的词典包
        /// </summary>
        public async Task<int> ImportVocabularyPackageAsync(string filePath)
        {
            var json = await System.IO.File.ReadAllTextAsync(filePath);
            var package = System.Text.Json.JsonSerializer.Deserialize<VocabularyPackage>(json);
            
            if (package?.Vocabularies == null) return 0;
            
            int imported = 0;
            foreach (var vocab in package.Vocabularies)
            {
                vocab.Source = VocabularySource.Imported;
                vocab.Id = Guid.NewGuid(); // 重新生成ID避免冲突
                await _historyService.UpsertVocabularyAsync(vocab);
                imported++;
            }
            
            return imported;
        }
        
        #endregion
    }
    
    #region 数据模型
    
    public class AIAnalysisResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int AnalyzedRecords { get; set; }
        public int GeneratedTerms { get; set; }
        public string SuggestedCategory { get; set; } = "";
        public List<PersonalVocabulary> GeneratedVocabularies { get; set; } = new();
        public string Insights { get; set; } = "";
    }
    
    public class AIAnalysisResponse
    {
        public string PrimaryDomain { get; set; } = "";
        public List<AITermSuggestion> SuggestedTerms { get; set; } = new();
        public string Insights { get; set; } = "";
    }
    
    public class AITermSuggestion
    {
        public string Term { get; set; } = "";
        public string Pinyin { get; set; } = "";
        public VocabularyCategory Category { get; set; } = VocabularyCategory.General;
        public double ImportanceScore { get; set; } = 0.5;
        public string[] CommonContexts { get; set; } = Array.Empty<string>();
        public string[] PotentialConfusions { get; set; } = Array.Empty<string>();
    }
    
    public class ErrorPatternAnalysis
    {
        public string Message { get; set; } = "";
        public int TotalCorrections { get; set; }
        public List<ErrorTypeStats> ErrorTypeStats { get; set; } = new();
        public List<CorrectionRule> GeneratedRules { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }
    
    public class ErrorTypeStats
    {
        public ErrorType ErrorType { get; set; }
        public int Count { get; set; }
        public List<CorrectionLog> Examples { get; set; } = new();
    }
    
    public class CorrectionRule
    {
        public string WrongPattern { get; set; } = "";
        public string CorrectPattern { get; set; } = "";
        public string ContextPattern { get; set; } = "";
        public double Confidence { get; set; }
        public ErrorType ErrorType { get; set; }
    }
    
    public class VocabularyPackage
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public List<PersonalVocabulary> Vocabularies { get; set; } = new();
        public VocabularyStats Statistics { get; set; } = new();
    }
    
    public class VocabularyStats
    {
        public int TotalWords { get; set; }
        public Dictionary<VocabularyCategory, int> CategoryDistribution { get; set; } = new();
    }
    
    #endregion
}
