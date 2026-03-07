# WordFlow 创作者工具设计文档

> 文档版本：1.0  
> 创建日期：2026-03-07  
> 状态：设计稿

---

## 目录

1. [创作者工具概述](#1-创作者工具概述)
2. [词典包制作工具](#2-词典包制作工具)
3. [一键导出功能](#3-一键导出功能)
4. [AI 辅助制作](#4-ai-辅助制作)
5. [上传与审核](#5-上传与审核)
6. [创作者收益管理](#6-创作者收益管理)

---

## 1. 创作者工具概述

### 1.1 目标用户

| 用户类型 | 说明 | 需求 |
|---------|------|------|
| **行业专家** | 医生、律师、工程师等 | 分享专业术语，获得收益 |
| **资深用户** | WordFlow 重度用户 | 分享个人词库，帮助他人 |
| **专业机构** | 医院、律所、企业 | 制作内部标准词典 |

### 1.2 创作流程

```
┌─────────────────────────────────────────────────────────────┐
│                    创作者流程                                │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. 申请成为创作者 → 实名认证 → 审核通过                    │
│     ↓                                                       │
│  2. 下载创作工具 → WordFlow Creator                         │
│     ↓                                                       │
│  3. 制作词典包                                              │
│     ├── 方式一：从现有数据导出                              │
│     ├── 方式二：手动创建                                    │
│     └── 方式三：AI 辅助生成                                  │
│     ↓                                                       │
│  4. 预览与测试 → 检查内容与格式                              │
│     ↓                                                       │
│  5. 上传至平台 → 填写信息 → 设置价格                         │
│     ↓                                                       │
│  6. 平台审核 → 质量检查 → 上架销售                          │
│     ↓                                                       │
│  7. 持续更新 → 根据反馈优化                                  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 1.3 创作者等级

| 等级 | 条件 | 权益 |
|------|------|------|
| **新手创作者** | 注册认证 | 基础工具、70% 分成 |
| **认证创作者** | 通过 1 个审核作品 | 优先审核、75% 分成 |
| **专业创作者** | 通过 5 个审核作品，好评率>90% | 专属标识、80% 分成、流量扶持 |
| **机构创作者** | 机构认证 | 定制服务、85% 分成 |

---

## 2. 词典包制作工具

### 2.1 工具界面

```
┌─────────────────────────────────────────────────────────────┐
│  WordFlow Creator - 词典包制作工具               [_][□][×]  │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  文件 (F)  编辑 (E)  工具 (T)  帮助 (H)                     │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  词典包信息                                                 │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  名称：[医疗行业标准词典________________]           │   │
│  │  版本：[1.0.0____]  分类：[▼ 医疗_______]          │   │
│  │  描述：[_________________________________________]  │   │
│  │         [_________________________________________]  │   │
│  │  封面：[thumbnail.png] [浏览...]                    │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  [热词] [快捷短语] [术语表] [预览]                  │   │
│  ├─────────────────────────────────────────────────────┤   │
│  │                                                     │   │
│  │  热词列表 (5000 个)                       [+ 添加]   │   │
│  │  ┌─────────────────────────────────────────────┐   │   │
│  │  │ 词汇           | 权重 | 分类    | 操作     │   │   │
│  │  ├─────────────────────────────────────────────┤   │   │
│  │  │ 室上性心动过速 | 10  | 心血管  | [编辑]   │   │   │
│  │  │ 冠状动脉       | 8   | 心血管  | [编辑]   │   │   │
│  │  │ ...                                          │   │   │
│  │  └─────────────────────────────────────────────┘   │   │
│  │                                                     │   │
│  │  批量操作：[导入 Excel] [导出 Excel] [批量编辑]    │   │
│  │                                                     │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  统计：热词 5000 个 | 快捷短语 300 个 | 术语 2000 个         │
│                                                             │
│  [保存]  [测试]  [打包]  [上传]                             │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 数据格式

```csharp
/// <summary>
/// 词典包元数据
/// </summary>
public class VocabularyPackManifest
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Version { get; set; }
    public string Author { get; set; }
    public string AuthorId { get; set; }
    public string Category { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "CNY";
    public bool IsPremium { get; set; }
    public string Thumbnail { get; set; }
    public PackFiles Files { get; set; }
    public PackStats Stats { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PackFiles
{
    public string Hotwords { get; set; }
    public string Shortcuts { get; set; }
    public string Terms { get; set; }
}

public class PackStats
{
    public int HotwordCount { get; set; }
    public int ShortcutCount { get; set; }
    public int TermCount { get; set; }
}
```

### 2.3 批量导入功能

```csharp
public class BatchImporter
{
    /// <summary>
    /// 从 Excel 导入热词
    /// </summary>
    public async Task<ImportResult> ImportHotwordsFromExcel(string filePath)
    {
        var result = new ImportResult();
        
        using var package = new ExcelPackage(new FileInfo(filePath));
        var worksheet = package.Workbook.Worksheets.First();
        
        int row = 2; // 跳过表头
        while (worksheet.Cells[row, 1].Value != null)
        {
            try
            {
                var hotword = new Hotword
                {
                    Word = worksheet.Cells[row, 1].Value?.ToString(),
                    Weight = ParseWeight(worksheet.Cells[row, 2].Value),
                    Category = worksheet.Cells[row, 3].Value?.ToString(),
                    Pinyin = worksheet.Cells[row, 4].Value?.ToString()
                };
                
                // 验证
                if (string.IsNullOrEmpty(hotword.Word))
                {
                    result.Errors.Add($"第{row}行：词汇不能为空");
                    row++;
                    continue;
                }
                
                if (hotword.Word.Length < 2 || hotword.Word.Length > 20)
                {
                    result.Errors.Add($"第{row}行：词汇长度应在 2-20 字之间");
                    row++;
                    continue;
                }
                
                _hotwordRepository.Add(hotword);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"第{row}行：{ex.Message}");
            }
            
            row++;
        }
        
        return result;
    }
    
    private int ParseWeight(object value)
    {
        if (value == null) return 5;
        if (int.TryParse(value.ToString(), out var weight))
        {
            return Math.Clamp(weight, 1, 10);
        }
        return 5;
    }
}

public class ImportResult
{
    public int SuccessCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool HasErrors => Errors.Any();
}
```

---

## 3. 一键导出功能

### 3.1 导出功能

```csharp
public class VocabularyPackExporter
{
    /// <summary>
    /// 一键导出个人词库为词典包
    /// </summary>
    public async Task<string> ExportAsPackAsync(ExportOptions options)
    {
        var packId = $"user-{Guid.NewGuid():N}";
        var packDir = Path.Combine(options.OutputPath, packId);
        
        Directory.CreateDirectory(packDir);
        
        // 1. 导出热词
        var hotwords = await _hotwordRepository.GetAllAsync();
        var hotwordsJson = JsonSerializer.Serialize(new
        {
            hotwords = hotwords.Select(h => new
            {
                h.Word,
                h.Weight,
                h.Category,
                h.Pinyin
            })
        }, new JsonSerializerOptions { WriteIndented = true });
        
        await File.WriteAllTextAsync(
            Path.Combine(packDir, "hotwords.json"), 
            hotwordsJson);
        
        // 2. 导出快捷短语
        var shortcuts = await _shortcutRepository.GetAllAsync();
        var shortcutsJson = JsonSerializer.Serialize(new
        {
            shortcuts = shortcuts.Select(s => new
            {
                s.Trigger,
                s.Aliases,
                s.Content,
                s.Category
            })
        }, new JsonSerializerOptions { WriteIndented = true });
        
        await File.WriteAllTextAsync(
            Path.Combine(packDir, "shortcuts.json"), 
            shortcutsJson);
        
        // 3. 生成 manifest
        var manifest = new VocabularyPackManifest
        {
            Id = packId,
            Name = options.PackName,
            Version = "1.0.0",
            Author = options.AuthorName,
            AuthorId = options.AuthorId,
            Category = options.Category,
            Description = options.Description,
            Price = 0, // 默认免费
            Currency = "CNY",
            IsPremium = false,
            Thumbnail = "thumbnail.png",
            Files = new PackFiles
            {
                Hotwords = "hotwords.json",
                Shortcuts = "shortcuts.json"
            },
            Stats = new PackStats
            {
                HotwordCount = hotwords.Count,
                ShortcutCount = shortcuts.Count,
                TermCount = 0
            },
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        
        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(
            Path.Combine(packDir, "manifest.json"), 
            manifestJson);
        
        // 4. 生成预览文件
        await GeneratePreviewFile(packDir, hotwords, shortcuts);
        
        // 5. 复制默认封面
        await CopyDefaultThumbnail(packDir);
        
        // 6. 打包为 ZIP
        var zipPath = Path.Combine(options.OutputPath, $"{packId}.zip");
        ZipFile.CreateFromDirectory(packDir, zipPath);
        
        // 清理临时目录
        Directory.Delete(packDir, true);
        
        return zipPath;
    }
}

public class ExportOptions
{
    public string PackName { get; set; }
    public string AuthorName { get; set; }
    public string AuthorId { get; set; }
    public string Category { get; set; }
    public string Description { get; set; }
    public string OutputPath { get; set; }
}
```

### 3.2 导出界面

```
┌─────────────────────────────────────────────────────────────┐
│  导出词典包                                      [×]        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  基本信息                                                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  词典包名称：[我的医疗词库________________]         │   │
│  │  分类：[▼ 医疗___________]                          │   │
│  │  描述：[_________________________________________]  │   │
│  │         [_________________________________________]  │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  导出内容                                                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  ☑ 热词 (523 个)                                    │   │
│  │  ☑ 快捷短语 (45 个)                                  │   │
│  │  ☐ 个人术语 (0 个)                                   │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  导出设置                                                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  ○ 仅导出为文件（本地保存）                         │   │
│  │  ● 导出并上传到市场（需要创作者账号）               │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  创作者账号                                                 │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  作者 ID：[creator_123456]                          │   │
│  │  状态：✓ 已认证                                     │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│         [取消]        [导出]                                │
└─────────────────────────────────────────────────────────────┘
```

---

## 4. AI 辅助制作

### 4.1 AI 生成热词

```csharp
public class AIHotwordGenerator
{
    private readonly IAIEnhancementService _aiService;
    
    /// <summary>
    /// 从文本中 AI 提取热词
    /// </summary>
    public async Task<List<Hotword>> ExtractHotwordsFromTextAsync(
        string text, 
        string category)
    {
        var prompt = $@"
请从以下文本中提取专业术语作为语音识别热词，要求：
1. 只提取专业术语、名词、专有名词
2. 每个术语 2-10 个字
3. 过滤常见停用词
4. 按重要性排序，最多提取 100 个

文本类别：{category}
文本内容：
{text}

请直接输出术语列表，每行一个：";

        var response = await _aiService.EnhanceAsync(prompt, new EnhancementOptions
        {
            MaxTokens = 2000
        });
        
        // 解析响应
        var terms = response.EnhancedText
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();
        
        return terms.Select((t, i) => new Hotword
        {
            Word = t,
            Weight = CalculateWeight(i, terms.Count),
            Category = category
        }).ToList();
    }
    
    private int CalculateWeight(int index, int total)
    {
        // 按排序位置分配权重，越靠前权重越高
        var ratio = 1.0 - (double)index / total;
        return (int)Math.Round(ratio * 5 + 5); // 5-10
    }
}
```

### 4.2 AI 生成快捷短语

```csharp
public class AIShortcutGenerator
{
    /// <summary>
    /// AI 辅助生成快捷短语
    /// </summary>
    public async Task<List<Shortcut>> GenerateShortcutsAsync(
        string category,
        List<string> commonScenarios)
    {
        var prompt = $@"
请为{category}行业设计常用快捷短语，包括：
1. 触发词：简短易记，2-6 个字
2. 展开内容：常用模板文本

常见场景：
{string.Join("\n", commonScenarios)}

请按以下格式输出：
触发词 | 展开内容
";

        var response = await _aiService.EnhanceAsync(prompt, new EnhancementOptions
        {
            MaxTokens = 3000
        });
        
        // 解析响应
        var shortcuts = new List<Shortcut>();
        foreach (var line in response.EnhancedText.Split('\n'))
        {
            var parts = line.Split('|');
            if (parts.Length >= 2)
            {
                shortcuts.Add(new Shortcut
                {
                    Trigger = parts[0].Trim(),
                    Content = parts[1].Trim(),
                    Category = category
                });
            }
        }
        
        return shortcuts;
    }
}
```

### 4.3 AI 辅助界面

```
┌─────────────────────────────────────────────────────────────┐
│  AI 辅助制作词典                                 [×]        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  请选择辅助功能：                                           │
│  ○ 从文本提取热词                                           │
│  ○ 生成快捷短语模板                                         │
│  ○ 智能分类建议                                             │
│                                                             │
│  输入文本或上传文件：                                       │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ [粘贴文本内容或拖拽文件到此处]                      │   │
│  │                                                     │   │
│  │                                                     │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  类别：[▼ 医疗___________]                                  │
│                                                             │
│  提取设置：                                                 │
│  最多提取 [100] 个热词                                        │
│  最小词频 [3] 次                                              │
│                                                             │
│         [取消]        [开始 AI 分析]                         │
└─────────────────────────────────────────────────────────────┘
```

---

## 5. 上传与审核

### 5.1 上传流程

```csharp
public class VocabularyPackUploader
{
    private readonly HttpClient _httpClient;
    private readonly string _uploadEndpoint;
    
    /// <summary>
    /// 上传词典包到平台
    /// </summary>
    public async Task<UploadResult> UploadAsync(
        string packPath, 
        UploadOptions options,
        IProgress<UploadProgress> progress)
    {
        // 1. 验证文件格式
        if (!await ValidatePackAsync(packPath))
        {
            return UploadResult.Failed("词典包格式验证失败");
        }
        
        // 2. 准备上传数据
        using var formData = new MultipartFormDataContent();
        
        // 添加元数据
        formData.Add(new StringContent(options.ManifestJson), "manifest");
        formData.Add(new StringContent(options.Price.ToString()), "price");
        formData.Add(new StringContent(options.Category), "category");
        
        // 添加文件
        var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(packPath));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        formData.Add(fileContent, "pack", Path.GetFileName(packPath));
        
        // 3. 上传
        var response = await _httpClient.PostAsync(_uploadEndpoint, formData);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            return UploadResult.Failed($"上传失败：{error}");
        }
        
        var result = await response.Content.ReadFromJsonAsync<UploadResult>();
        return result;
    }
    
    private async Task<bool> ValidatePackAsync(string packPath)
    {
        // 验证 ZIP 结构
        using var archive = ZipFile.OpenRead(packPath);
        
        var requiredFiles = new[] { "manifest.json", "hotwords.json", "shortcuts.json" };
        
        foreach (var file in requiredFiles)
        {
            if (archive.GetEntry(file) == null)
            {
                return false;
            }
        }
        
        return true;
    }
}

public class UploadOptions
{
    public string ManifestJson { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; }
}

public class UploadResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string PackId { get; set; }
    public string ReviewStatus { get; set; } // pending, approved, rejected
    
    public static UploadResult Failed(string message)
    {
        return new UploadResult { Success = false, Message = message };
    }
}
```

### 5.2 审核流程

```
┌─────────────────────────────────────────────────────────────┐
│                    词典包审核流程                            │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  创作者上传                                                  │
│       ↓                                                     │
│  ┌─────────────────┐                                        │
│  │ 自动格式检查     │                                        │
│  │ - 文件完整性     │                                        │
│  │ - JSON 格式验证   │                                        │
│  │ - 病毒扫描       │                                        │
│  └────────┬────────┘                                        │
│           ↓ 通过                                            │
│  ┌─────────────────┐                                        │
│  │ 人工质量审核     │                                        │
│  │ - 内容质量       │                                        │
│  │ - 原创性         │                                        │
│  │ - 定价合理性     │                                        │
│  └────────┬────────┘                                        │
│           ↓                                                 │
│      ┌────┴────┐                                           │
│      ↓         ↓                                           │
│   通过       驳回                                           │
│      ↓         ↓                                           │
│  上架销售   通知创作者                                      │
│              修改后重新提交                                 │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 5.3 审核标准

```csharp
public class ReviewCriteria
{
    /// <summary>
    /// 审核评分
    /// </summary>
    public ReviewResult Review(VocabularyPack pack)
    {
        var score = 0;
        var issues = new List<string>();
        
        // 1. 内容完整性 (20 分)
        if (pack.Stats.HotwordCount >= 100) score += 10;
        if (pack.Stats.ShortcutCount >= 20) score += 5;
        if (!string.IsNullOrEmpty(pack.Description)) score += 5;
        
        // 2. 内容质量 (40 分)
        var qualityScore = EvaluateContentQuality(pack);
        score += qualityScore;
        
        // 3. 原创性 (20 分)
        var originalityScore = CheckOriginality(pack);
        score += originalityScore;
        
        // 4. 定价合理性 (20 分)
        var priceScore = EvaluatePrice(pack.Price, pack.Stats);
        score += priceScore;
        
        // 判定结果
        if (score >= 80)
        {
            return ReviewResult.Approved(score);
        }
        else if (score >= 60)
        {
            return ReviewResult.NeedsRevision(score, issues);
        }
        else
        {
            return ReviewResult.Rejected(score, issues);
        }
    }
    
    private int EvaluateContentQuality(VocabularyPack pack)
    {
        // 检查热词质量
        var hotwordQuality = CheckHotwordQuality(pack.Hotwords);
        
        // 检查快捷短语质量
        var shortcutQuality = CheckShortcutQuality(pack.Shortcuts);
        
        return (hotwordQuality + shortcutQuality) / 2;
    }
    
    private int CheckOriginality(VocabularyPack pack)
    {
        // 与现有词典包对比，检查重复率
        var duplicateRate = _similarityChecker.Check(pack);
        
        if (duplicateRate < 0.1) return 20;
        if (duplicateRate < 0.3) return 10;
        return 0;
    }
    
    private int EvaluatePrice(decimal price, PackStats stats)
    {
        // 根据内容量评估定价合理性
        var expectedPrice = (stats.HotwordCount * 0.01m) + 
                           (stats.ShortcutCount * 0.1m);
        
        var ratio = price / (expectedPrice == 0 ? 1 : expectedPrice);
        
        if (ratio >= 0.5m && ratio <= 2.0m) return 20;
        if (ratio >= 0.3m && ratio <= 3.0m) return 10;
        return 0;
    }
}

public enum ReviewStatus
{
    Approved,
    NeedsRevision,
    Rejected
}

public class ReviewResult
{
    public ReviewStatus Status { get; set; }
    public int Score { get; set; }
    public List<string> Issues { get; set; }
    
    public static ReviewResult Approved(int score)
    {
        return new ReviewResult { Status = ReviewStatus.Approved, Score = score };
    }
    
    public static ReviewResult NeedsRevision(int score, List<string> issues)
    {
        return new ReviewResult { Status = ReviewStatus.NeedsRevision, Score = score, Issues = issues };
    }
    
    public static ReviewResult Rejected(int score, List<string> issues)
    {
        return new ReviewResult { Status = ReviewStatus.Rejected, Score = score, Issues = issues };
    }
}
```

---

## 6. 创作者收益管理

### 6.1 收益统计

```csharp
public class CreatorEarningsService
{
    /// <summary>
    /// 获取创作者收益统计
    /// </summary>
    public async Task<EarningsStatistics> GetEarningsAsync(
        string creatorId, 
        DateTime startDate, 
        DateTime endDate)
    {
        var sales = await _salesRepository.GetByCreatorAsync(
            creatorId, 
            startDate, 
            endDate);
        
        var stats = new EarningsStatistics
        {
            TotalSales = sales.Count,
            GrossRevenue = sales.Sum(s => s.Amount),
            PlatformFee = sales.Sum(s => s.PlatformFee),
            NetEarnings = sales.Sum(s => s.CreatorEarning),
            AverageRating = await GetAverageRatingAsync(creatorId),
            TopPacks = await GetTopSellingPacksAsync(creatorId, 5)
        };
        
        return stats;
    }
    
    /// <summary>
    /// 申请提现
    /// </summary>
    public async Task<WithdrawalResult> RequestWithdrawalAsync(
        string creatorId, 
        decimal amount,
        string paymentMethod)
    {
        // 检查最低提现金额
        if (amount < 50)
        {
            return WithdrawalResult.Failed("最低提现金额为¥50");
        }
        
        // 检查可用余额
        var balance = await GetAvailableBalanceAsync(creatorId);
        if (amount > balance)
        {
            return WithdrawalResult.Failed("余额不足");
        }
        
        // 创建提现记录
        var withdrawal = new Withdrawal
        {
            CreatorId = creatorId,
            Amount = amount,
            PaymentMethod = paymentMethod,
            Status = "pending",
            CreatedAt = DateTime.Now
        };
        
        await _withdrawalRepository.AddAsync(withdrawal);
        
        // 冻结余额
        await _balanceService.FreezeAsync(creatorId, amount);
        
        return WithdrawalResult.Success(withdrawal.Id);
    }
}

public class EarningsStatistics
{
    public int TotalSales { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal PlatformFee { get; set; }
    public decimal NetEarnings { get; set; }
    public double AverageRating { get; set; }
    public List<TopPack> TopPacks { get; set; }
}

public class TopPack
{
    public string PackId { get; set; }
    public string PackName { get; set; }
    public int SalesCount { get; set; }
    public decimal Earnings { get; set; }
}
```

### 6.2 收益管理界面

```
┌─────────────────────────────────────────────────────────────┐
│  创作者中心 - 收益管理                           [_][□][×]  │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  [概览] [收益明细] [提现] [设置]                            │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  收益概览                                                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  本月收益：¥2,580.00    累计收益：¥15,680.00        │   │
│  │  可提现余额：¥1,230.00  待结算：¥850.00             │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  作品表现                                                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  词典包名称        销量   收益     评分            │   │
│  ├─────────────────────────────────────────────────────┤   │
│  │  医疗行业标准词典   128   ¥1,856  ★★★★☆          │   │
│  │  法律术语大全      45    ¥585   ★★★★★          │   │
│  │  IT 行业热词包     12    ¥139   ★★★★☆          │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  收益趋势                                                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  [图表：近 6 个月收益趋势]                            │   │
│  │                                                     │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  [申请提现]                              查看明细 →         │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 7. 实施计划

### 7.1 第一阶段：基础工具

- [ ] 词典包制作工具
- [ ] Excel 导入导出
- [ ] 基础验证

### 7.2 第二阶段：AI 辅助

- [ ] AI 热词提取
- [ ] AI 快捷短语生成
- [ ] 智能分类

### 7.3 第三阶段：上传审核

- [ ] 上传功能
- [ ] 自动格式检查
- [ ] 审核后台

### 7.4 第四阶段：收益管理

- [ ] 收益统计
- [ ] 提现功能
- [ ] 创作者中心

---

*本文档为 WordFlow 创作者工具设计，将根据开发进度持续更新。*

*最后更新：2026-03-07*
