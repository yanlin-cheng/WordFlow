# WordFlow 热词自动生成工具设计

> 文档版本：1.0  
> 创建日期：2026-03-07  
> 状态：设计稿

---

## 目录

1. [数据来源](#1-数据来源)
2. [处理流程](#2-处理流程)
3. [词频分析](#3-词频分析)
4. [NLP 分词](#4-nlp-分词)
5. [候选词筛选](#5-候选词筛选)
6. [用户确认流程](#6-用户确认流程)
7. [热词权重计算](#7-热词权重计算)

---

## 1. 数据来源

### 1.1 数据来源概览

```
┌─────────────────────────────────────────────────────────────┐
│                    热词数据来源                              │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────────┐                                        │
│  │  用户历史记录   │ ←── 主要来源                           │
│  │  (InputHistory) │                                        │
│  └─────────────────┘                                        │
│                                                             │
│  ┌─────────────────┐                                        │
│  │  导入文本语料   │ ←── 批量导入                           │
│  │  (文件/剪贴板)   │                                        │
│  └─────────────────┘                                        │
│                                                             │
│  ┌─────────────────┐                                        │
│  │  AI 辅助生成     │ ←── 智能提取                           │
│  │  (LLM 分析)      │                                        │
│  └─────────────────┘                                        │
│                                                             │
│  ┌─────────────────┐                                        │
│  │  云端词库同步   │ ←── 推荐热词                           │
│  │  (CloudSync)    │                                        │
│  └─────────────────┘                                        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 用户历史记录

**数据表结构**：
```sql
-- 输入历史表
CREATE TABLE input_history (
    id TEXT PRIMARY KEY,
    text TEXT NOT NULL,
    source TEXT,  -- 'voice' | 'keyboard'
    context TEXT,  -- 上下文信息（应用、时间等）
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- 纠错记录表
CREATE TABLE correction_log (
    id TEXT PRIMARY KEY,
    wrong_word TEXT NOT NULL,
    correct_word TEXT NOT NULL,
    count INTEGER DEFAULT 1,
    last_used_at DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

**数据获取**：
```csharp
public class HistoryDataSource
{
    private readonly IDbConnection _db;
    
    /// <summary>
    /// 获取最近 N 天的输入历史
    /// </summary>
    public List<string> GetRecentInputs(int days = 7, int limit = 1000)
    {
        var sql = @"
            SELECT text FROM input_history 
            WHERE created_at >= datetime('now', '-' || @days || ' days')
            ORDER BY created_at DESC
            LIMIT @limit";
        
        return _db.Query<string>(sql, new { days, limit }).ToList();
    }
    
    /// <summary>
    /// 获取纠错记录
    /// </summary>
    public List<CorrectionEntry> GetCorrections(int limit = 500)
    {
        var sql = @"
            SELECT wrong_word, correct_word, count, last_used_at 
            FROM correction_log 
            ORDER BY count DESC 
            LIMIT @limit";
        
        return _db.Query<CorrectionEntry>(sql, new { limit }).ToList();
    }
}
```

### 1.3 导入文本语料

**支持的导入格式**：
- TXT 文本文件
- Word 文档 (.docx)
- PDF 文档
- 剪贴板文本
- 批量导入文件夹

**导入界面**：
```
┌─────────────────────────────────────────────────────────────┐
│  导入文本语料                                    [×]        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  选择导入方式：                                             │
│  ○ 从文件导入  [浏览...]                                    │
│  ○ 从剪贴板导入  [粘贴]                                     │
│  ○ 从文件夹导入  [选择文件夹]                               │
│                                                             │
│  已选择文件：                                               │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ 📄 病历模板.txt (15.2 KB)                     [删除] │   │
│  │ 📄 工作记录.docx (28.5 KB)                    [删除] │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  处理选项：                                                 │
│  ☑ 自动分词                                                 │
│  ☑ 过滤常用词                                               │
│  ☑ 提取专业术语                                             │
│  最小词频：[5] 次                                           │
│                                                             │
│         [取消]        [开始导入]                            │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. 处理流程

### 2.1 整体流程

```
┌─────────────────────────────────────────────────────────────┐
│                热词自动生成流程                              │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. 数据收集                                                 │
│     ↓                                                       │
│  ┌─────────────────┐                                        │
│  │ 输入历史记录     │                                        │
│  │ 导入文本语料     │                                        │
│  │ 纠错记录         │                                        │
│  └─────────────────┘                                        │
│     ↓                                                       │
│  2. 文本预处理                                               │
│     ↓                                                       │
│  ┌─────────────────┐                                        │
│  │ 清洗文本         │                                        │
│  │ 合并内容         │                                        │
│  │ 分句处理         │                                        │
│  └─────────────────┘                                        │
│     ↓                                                       │
│  3. NLP 分词                                                  │
│     ↓                                                       │
│  ┌─────────────────┐                                        │
│  │ Jieba 分词        │                                        │
│  │ 词性标注         │                                        │
│  │ 命名实体识别     │                                        │
│  └─────────────────┘                                        │
│     ↓                                                       │
│  4. 词频统计                                                 │
│     ↓                                                       │
│  ┌─────────────────┐                                        │
│  │ 统计词频         │                                        │
│  │ 过滤停用词       │                                        │
│  │ 计算 TF-IDF      │                                        │
│  └─────────────────┘                                        │
│     ↓                                                       │
│  5. 候选词筛选                                               │
│     ↓                                                       │
│  ┌─────────────────┐                                        │
│  │ 按词频排序       │                                        │
│  │ 按 TF-IDF 排序     │                                        │
│  │ 按类别分组       │                                        │
│  └─────────────────┘                                        │
│     ↓                                                       │
│  6. 用户确认                                                 │
│     ↓                                                       │
│  展示候选词 → 用户选择 → 加入热词表                          │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 文本预处理

```csharp
public class TextPreprocessor
{
    /// <summary>
    /// 预处理文本
    /// </summary>
    public string Preprocess(List<string> texts)
    {
        var combined = string.Join(" ", texts);
        
        // 1. 移除特殊字符（保留中文、英文、数字）
        combined = Regex.Replace(combined, @"[^\u4e00-\u9fa5a-zA-Z0-9，。、；：？！\s]", "");
        
        // 2. 标准化空白
        combined = Regex.Replace(combined, @"\s+", " ");
        
        // 3. 移除过短内容
        if (combined.Length < 10)
        {
            return "";
        }
        
        return combined.Trim();
    }
    
    /// <summary>
    /// 分句处理
    /// </summary>
    public List<string> SplitSentences(string text)
    {
        return Regex.Split(text, "[。！？\\.!?]")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();
    }
}
```

---

## 3. 词频分析

### 3.1 词频统计

```csharp
public class WordFrequencyAnalyzer
{
    private readonly Dictionary<string, int> _wordFreq = new();
    private readonly HashSet<string> _stopWords;
    
    public WordFrequencyAnalyzer()
    {
        _stopWords = LoadStopWords();
    }
    
    /// <summary>
    /// 分析文本词频
    /// </summary>
    public Dictionary<string, int> Analyze(List<string> texts)
    {
        _wordFreq.Clear();
        
        foreach (var text in texts)
        {
            var words = Segment(text);
            
            foreach (var word in words)
            {
                // 过滤停用词
                if (_stopWords.Contains(word)) continue;
                
                // 过滤太短或太长的词
                if (word.Length < 2 || word.Length > 10) continue;
                
                // 过滤纯数字
                if (Regex.IsMatch(word, @"^\d+$")) continue;
                
                if (_wordFreq.ContainsKey(word))
                    _wordFreq[word]++;
                else
                    _wordFreq[word] = 1;
            }
        }
        
        return _wordFreq.OrderByDescending(kvp => kvp.Value)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
    
    /// <summary>
    /// 分词
    /// </summary>
    private List<string> Segment(string text)
    {
        // 使用 Jieba 分词
        return JiebaSegment.Cut(text, false).ToList();
    }
    
    /// <summary>
    /// 加载停用词表
    /// </summary>
    private HashSet<string> LoadStopWords()
    {
        return new HashSet<string>
        {
            "的", "了", "是", "在", "我", "有", "和", "就",
            "不", "人", "都", "一", "一个", "上", "也", "很",
            "到", "说", "要", "去", "你", "会", "着", "没有",
            "看", "好", "自己", "这", "那", "他", "她", "它"
            // ... 更多停用词
        };
    }
}
```

### 3.2 TF-IDF 计算

```csharp
public class TfIdfAnalyzer
{
    /// <summary>
    /// 计算 TF-IDF
    /// </summary>
    public Dictionary<string, double> CalculateTfIdf(
        List<string> document,
        List<List<string>> allDocuments)
    {
        var tf = CalculateTF(document);
        var idf = CalculateIDF(document, allDocuments);
        
        return tf.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value * (idf.ContainsKey(kvp.Key) ? idf[kvp.Key] : 1)
        );
    }
    
    /// <summary>
    /// 计算词频 (TF)
    /// </summary>
    private Dictionary<string, double> CalculateTF(List<string> document)
    {
        var wordCount = new Dictionary<string, int>();
        
        foreach (var word in document)
        {
            if (wordCount.ContainsKey(word))
                wordCount[word]++;
            else
                wordCount[word] = 1;
        }
        
        var totalWords = document.Count;
        
        return wordCount.ToDictionary(
            kvp => kvp.Key,
            kvp => (double)kvp.Value / totalWords
        );
    }
    
    /// <summary>
    /// 计算逆文档频率 (IDF)
    /// </summary>
    private Dictionary<string, double> CalculateIDF(
        List<string> document,
        List<List<string>> allDocuments)
    {
        var wordDocCount = new Dictionary<string, int>();
        var totalDocs = allDocuments.Count;
        
        foreach (var doc in allDocuments)
        {
            var uniqueWords = doc.Distinct();
            
            foreach (var word in uniqueWords)
            {
                if (wordDocCount.ContainsKey(word))
                    wordDocCount[word]++;
                else
                    wordDocCount[word] = 1;
            }
        }
        
        return wordDocCount.ToDictionary(
            kvp => kvp.Key,
            kvp => Math.Log((double)totalDocs / (1 + kvp.Value))
        );
    }
}
```

---

## 4. NLP 分词

### 4.1 Jieba 分词集成

```python
# Python 分词服务
import jieba
import jieba.analyse

class ChineseSegmenter:
    def __init__(self):
        # 加载自定义词典
        self.load_custom_dict()
    
    def load_custom_dict(self):
        """加载自定义词典（医学术语等）"""
        jieba.load_userdict("data/medical_dict.txt")
    
    def segment(self, text: str) -> list:
        """分词"""
        return list(jieba.cut(text))
    
    def segment_with_pos(self, text: str) -> list:
        """分词 + 词性标注"""
        return list(jieba.posseg.cut(text))
    
    def extract_keywords(self, text: str, top_k: int = 20) -> list:
        """提取关键词（基于 TF-IDF）"""
        return jieba.analyse.extract_tags(text, topK=top_k)
    
    def extract_keywords_textrank(self, text: str, top_k: int = 20) -> list:
        """提取关键词（基于 TextRank）"""
        return jieba.analyse.textrank(text, topK=top_k)
```

### 4.2 词性过滤

```csharp
public class PosFilter
{
    /// <summary>
    /// 保留的词性
    /// </summary>
    private readonly HashSet<string> _allowedPos = new()
    {
        "n",    // 名词
        "nr",   // 人名
        "ns",   // 地名
        "nt",   // 机构团体
        "nz",   // 其他专有名词
        "vn",   // 名动词
        "a",    // 形容词
        "d"     // 副词
    };
    
    /// <summary>
    /// 根据词性过滤
    /// </summary>
    public List<string> FilterByPos(List<(string Word, string Pos)> taggedWords)
    {
        return taggedWords
            .Where(w => _allowedPos.Contains(w.Pos))
            .Select(w => w.Word)
            .ToList();
    }
}
```

### 4.3 命名实体识别

```python
# 命名实体识别
import spacy

class NamedEntityExtractor:
    def __init__(self):
        # 加载中文模型
        self.nlp = spacy.load("zh_core_web_sm")
    
    def extract_entities(self, text: str) -> list:
        """提取命名实体"""
        doc = self.nlp(text)
        
        entities = []
        for ent in doc.ents:
            entities.append({
                "text": ent.text,
                "label": ent.label_,
                "start": ent.start_char,
                "end": ent.end_char
            })
        
        return entities
    
    def extract_medical_terms(self, text: str) -> list:
        """提取医学术语（自定义规则）"""
        # 自定义医学术语模式
        patterns = [
            r"[\u4e00-\u9fa5]+症",      # XX 症
            r"[\u4e00-\u9fa5]+炎",      # XX 炎
            r"[\u4e00-\u9fa5]+病",      # XX 病
            r"[\u4e00-\u9fa5]+瘤",      # XX 瘤
            r"[\u4e00-\u9fa5]+癌",      # XX 癌
            r"[\u4e00-\u9fa5]+手术",    # XX 手术
            r"[\u4e00-\u9fa5]+检查",    # XX 检查
            r"[\u4e00-\u9fa5]+测试",    # XX 测试
        ]
        
        terms = []
        for pattern in patterns:
            matches = re.findall(pattern, text)
            terms.extend(matches)
        
        return list(set(terms))
```

---

## 5. 候选词筛选

### 5.1 筛选规则

```csharp
public class CandidateWordFilter
{
    public List<CandidateWord> Filter(
        Dictionary<string, int> wordFreq,
        FilterOptions options)
    {
        var candidates = new List<CandidateWord>();
        
        foreach (var kvp in wordFreq)
        {
            var word = kvp.Key;
            var freq = kvp.Value;
            
            // 1. 词频过滤
            if (freq < options.MinFrequency) continue;
            
            // 2. 词长过滤
            if (word.Length < options.MinLength || word.Length > options.MaxLength) continue;
            
            // 3. 常用词过滤
            if (_commonWords.Contains(word)) continue;
            
            // 4. 已存在热词过滤
            if (_existingHotwords.Contains(word)) continue;
            
            // 计算候选分数
            var score = CalculateScore(word, freq, options);
            
            candidates.Add(new CandidateWord
            {
                Word = word,
                Frequency = freq,
                Score = score,
                SuggestedWeight = CalculateSuggestedWeight(score)
            });
        }
        
        // 按分数排序
        return candidates.OrderByDescending(c => c.Score).ToList();
    }
    
    private double CalculateScore(string word, int freq, FilterOptions options)
    {
        var score = freq;
        
        // 词长加分（2-4 字最佳）
        if (word.Length >= 2 && word.Length <= 4)
        {
            score *= 1.5;
        }
        
        // 专业术语加分
        if (_medicalTerms.Contains(word))
        {
            score *= 2;
        }
        
        // 人名地名加分
        if (_namedEntities.Contains(word))
        {
            score *= 1.8;
        }
        
        return score;
    }
    
    private int CalculateSuggestedWeight(double score)
    {
        if (score >= 100) return 10;
        if (score >= 50) return 8;
        if (score >= 20) return 6;
        if (score >= 10) return 4;
        return 2;
    }
}

public class CandidateWord
{
    public string Word { get; set; }
    public int Frequency { get; set; }
    public double Score { get; set; }
    public int SuggestedWeight { get; set; }
    public string Category { get; set; }
}

public class FilterOptions
{
    public int MinFrequency { get; set; } = 5;
    public int MinLength { get; set; } = 2;
    public int MaxLength { get; set; } = 10;
}
```

### 5.2 分类建议

```csharp
public class WordCategorizer
{
    public string Categorize(string word)
    {
        // 医学术语
        if (_medicalTerms.Contains(word)) return "医疗";
        
        // 人名
        if (IsPersonName(word)) return "人名";
        
        // 地名
        if (IsLocation(word)) return "地名";
        
        // 机构名
        if (IsOrganization(word)) return "机构";
        
        // 技术术语
        if (_techTerms.Contains(word)) return "技术";
        
        return "通用";
    }
    
    private bool IsPersonName(string word)
    {
        // 简单的人名判断（可以结合 NLP）
        return word.Length >= 2 && word.Length <= 4;
    }
    
    private bool IsLocation(string word)
    {
        return word.EndsWith("市") || word.EndsWith("省") || 
               word.EndsWith("区") || word.EndsWith("县");
    }
    
    private bool IsOrganization(string word)
    {
        return word.EndsWith("公司") || word.EndsWith("医院") || 
               word.EndsWith("学校") || word.EndsWith("局");
    }
}
```

---

## 6. 用户确认流程

### 6.1 候选词展示界面

```
┌─────────────────────────────────────────────────────────────┐
│  热词候选词推荐                                  [×]        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  分析结果：从 1,234 条输入记录中发现 56 个候选热词            │
│                                                             │
│  分类筛选：[全部] [医疗] [人名] [地名] [技术] [通用]        │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  候选词                              建议权重  词频   │   │
│  ├─────────────────────────────────────────────────────┤   │
│  │ ☑ 室上性心动过速                        [10]   42   │   │
│  │ ☑ 冠状动脉                              [8]    28   │   │
│  │ ☑ 心电图                                [6]    25   │   │
│  │ ☑ 张三 (患者名)                         [5]    18   │   │
│  │ ☑ 人民医院                              [5]    15   │   │
│  │ ☐ 然后                                  [2]    12   │   │
│  │ ☐ 就是                                  [2]    10   │   │
│  │ ...                                                    │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  批量操作：                                                 │
│  [全选] [取消全选] [全选医疗] [全选人名]                    │
│                                                             │
│  权重设置：○ 使用建议权重  ○ 统一设置为 [5]                 │
│                                                             │
│         [取消]        [添加选中热词 (12)]                   │
└─────────────────────────────────────────────────────────────┘
```

### 6.2 确认逻辑

```csharp
public class HotwordConfirmationDialog : Window
{
    public List<CandidateWord> SelectedCandidates { get; private set; }
    public int DefaultWeight { get; private set; }
    
    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        // 获取选中的候选词
        SelectedCandidates = CandidatesList.Items
            .Cast<CandidateWordItem>()
            .Where(item => item.IsChecked)
            .Select(item => item.Candidate)
            .ToList();
        
        // 获取权重设置
        if (UseSuggestedWeightRadio.IsChecked == true)
        {
            // 使用各自建议权重
        }
        else
        {
            // 使用统一权重
            DefaultWeight = (int)WeightSlider.Value;
        }
        
        DialogResult = true;
        Close();
    }
}
```

---

## 7. 热词权重计算

### 7.1 权重计算公式

```csharp
public class WeightCalculator
{
    /// <summary>
    /// 计算热词权重
    /// </summary>
    public int CalculateWeight(CandidateWord candidate)
    {
        var weight = 5; // 基础权重
        
        // 1. 基于词频
        if (candidate.Frequency >= 50) weight += 3;
        else if (candidate.Frequency >= 20) weight += 2;
        else if (candidate.Frequency >= 10) weight += 1;
        
        // 2. 基于类别
        if (candidate.Category == "医疗") weight += 2;
        else if (candidate.Category == "人名") weight += 1;
        else if (candidate.Category == "地名") weight += 1;
        
        // 3. 基于分数
        if (candidate.Score >= 100) weight += 2;
        else if (candidate.Score >= 50) weight += 1;
        
        // 限制在 1-10 范围
        return Math.Clamp(weight, 1, 10);
    }
}
```

### 7.2 权重调整建议

```csharp
public class WeightAdjuster
{
    /// <summary>
    /// 根据用户反馈调整权重
    /// </summary>
    public void AdjustWeight(string word, bool wasRecognizedCorrectly)
    {
        var hotword = _hotwordRepository.Get(word);
        
        if (wasRecognizedCorrectly)
        {
            // 识别正确，保持当前权重
        }
        else
        {
            // 识别错误，提高权重
            hotword.Weight = Math.Min(hotword.Weight + 1, 10);
        }
        
        _hotwordRepository.Save(hotword);
    }
}
```

---

## 8. 热词与快捷短语联动（新增）

### 8.1 联动机制

**作用**：将热词与快捷短语关联，当语音识别检测到热词时，自动推荐相关的快捷短语。

### 8.2 关联规则

```csharp
public class HotwordShortcutLinker
{
    /// <summary>
    /// 根据热词查找关联的快捷短语
    /// </summary>
    public List<Shortcut> FindRelatedShortcuts(string hotword)
    {
        return _shortcuts
            .Where(s => s.LinkedHotwords.Contains(hotword))
            .OrderByDescending(s => s.Priority)
            .Take(5)
            .ToList();
    }
    
    /// <summary>
    /// 批量查找（多个热词匹配）
    /// </summary>
    public List<Shortcut> FindRelatedShortcuts(List<string> hotwords)
    {
        return _shortcuts
            .Where(s => s.LinkedHotwords.Intersect(hotwords).Any())
            .OrderByDescending(s => 
                s.LinkedHotwords.Intersect(hotwords).Count() * s.Priority
            )
            .Take(5)
            .ToList();
    }
}
```

### 8.3 智能推荐算法

```csharp
public class ShortcutRecommender
{
    /// <summary>
    /// 根据上下文推荐快捷短语
    /// </summary>
    public RecommendationResult Recommend(
        string currentText,      // 当前识别文本
        List<string> hotwords,   // 匹配的热词
        string context)          // 上下文（应用、时间等）
    {
        var candidates = new List<ShortcutCandidate>();
        
        // 1. 基于热词匹配
        var hotwordMatches = FindByHotwords(hotwords);
        foreach (var match in hotwordMatches)
        {
            candidates.Add(new ShortcutCandidate
            {
                Shortcut = match,
                Score = 1.0,
                Reason = "热词匹配"
            });
        }
        
        // 2. 基于上下文推荐
        var contextMatches = FindByContext(context);
        foreach (var match in contextMatches)
        {
            candidates.Add(new ShortcutCandidate
            {
                Shortcut = match,
                Score = 0.8,
                Reason = "上下文匹配"
            });
        }
        
        // 3. 基于使用频率
        var frequentMatches = FindByUsage();
        foreach (var match in frequentMatches)
        {
            candidates.Add(new ShortcutCandidate
            {
                Shortcut = match,
                Score = 0.6,
                Reason = "常用短语"
            });
        }
        
        // 合并排序
        return new RecommendationResult
        {
            Shortcuts = candidates
                .GroupBy(c => c.Shortcut.Id)
                .Select(g => new ShortcutCandidate
                {
                    Shortcut = g.First().Shortcut,
                    Score = g.Sum(c => c.Score),
                    Reasons = g.Select(c => c.Reason).ToList()
                })
                .OrderByDescending(c => c.Score)
                .Take(5)
                .ToList()
        };
    }
}

public class ShortcutCandidate
{
    public Shortcut Shortcut { get; set; }
    public double Score { get; set; }
    public string Reason { get; set; }
    public List<string> Reasons { get; set; }
}
```

### 8.4 防打扰机制

```csharp
public class SuggestionThrottler
{
    private Dictionary<string, DateTime> _lastShowTime = new();
    private Dictionary<string, int> _cancelCount = new();
    
    /// <summary>
    /// 检查是否应该显示建议
    /// </summary>
    public bool ShouldShowSuggestion(List<string> hotwords)
    {
        var now = DateTime.Now;
        
        foreach (var hotword in hotwords)
        {
            // 检查冷却时间（30 秒）
            if (_lastShowTime.ContainsKey(hotword))
            {
                var elapsed = now - _lastShowTime[hotword];
                if (elapsed.TotalSeconds < 30)
                {
                    continue;
                }
            }
            
            // 检查取消次数
            if (_cancelCount.ContainsKey(hotword) && _cancelCount[hotword] >= 3)
            {
                continue;
            }
            
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 记录显示时间
    /// </summary>
    public void RecordShow(List<string> hotwords)
    {
        foreach (var hotword in hotwords)
        {
            _lastShowTime[hotword] = DateTime.Now;
        }
    }
    
    /// <summary>
    /// 记录用户取消
    /// </summary>
    public void RecordCancel(List<string> hotwords)
    {
        foreach (var hotword in hotwords)
        {
            if (!_cancelCount.ContainsKey(hotword))
            {
                _cancelCount[hotword] = 0;
            }
            _cancelCount[hotword]++;
        }
    }
    
    /// <summary>
    /// 重置取消计数（语音输入结束时调用）
    /// </summary>
    public void ResetCancelCount()
    {
        _cancelCount.Clear();
    }
}
```

### 8.5 数据结构扩展

```json
{
  "shortcuts": [
    {
      "id": "sc-001",
      "trigger": "患者主诉",
      "content": "患者主诉：[症状]，持续 [时间]",
      "category": "医疗",
      "linkedHotwords": ["患者", "主诉", "症状"],
      "priority": 10,
      "usageCount": 42,
      "lastUsedAt": "2026-03-07T15:30:00Z"
    }
  ]
}
```

---

## 9. 实施计划

### 9.1 第一阶段：基础功能

- [ ] 历史记录分析
- [ ] 词频统计
- [ ] 基础分词

### 9.2 第二阶段：NLP 增强

- [ ] Jieba 分词集成
- [ ] 词性标注
- [ ] 命名实体识别

### 9.3 第三阶段：用户界面

- [ ] 候选词展示
- [ ] 批量操作
- [ ] 确认流程

### 9.4 第四阶段：优化

- [ ] TF-IDF 分析
- [ ] 智能分类
- [ ] 权重自适应

### 9.5 第五阶段：联动功能（新增）

- [ ] 热词与快捷短语关联
- [ ] 智能推荐算法
- [ ] 防打扰机制
- [ ] 弹窗 UI 实现

---

*本文档为 WordFlow 热词自动生成工具设计，将根据开发进度持续更新。*

*最后更新：2026-03-07*
