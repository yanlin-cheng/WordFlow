# WordFlow AI 增强功能设计文档

> 文档版本：1.0  
> 创建日期：2026-03-07  
> 状态：设计稿

---

## 目录

1. [功能概述](#1-功能概述)
2. [AI 服务模式](#2-ai-服务模式)
3. [快捷短语保护机制](#3-快捷短语保护机制)
4. [流式输入与 AI 增强协调](#4-流式输入与 ai-增强协调)
5. [Prompt 工程设计](#5-prompt-工程设计)
6. [成本控制策略](#6-成本控制策略)

---

## 1. 功能概述

### 1.1 设计理念

**核心原则**：不要用云端 API 替代本地引擎，而要用它来赋能和增强本地引擎的输出结果。

| 特性 | 说明 |
|------|------|
| **本地优先** | 基础识别本地完成，保证零延迟 |
| **云端增强** | 识别后的文本可选云端优化 |
| **用户裁决** | 优化结果需用户确认 |
| **持续学习** | 从用户选择中学习优化策略 |

### 1.2 功能架构

```
┌─────────────────────────────────────────────────────────┐
│                    AI 增强模块                           │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  本地识别文本 A ──→ 即时显示（零延迟）                   │
│       ↓                                                 │
│  ┌─────────────────┐                                    │
│  │  用户设置       │                                    │
│  │  □ 自动优化     │                                    │
│  │  □ 手动触发     │                                    │
│  └────────┬────────┘                                    │
│           │                                             │
│           ↓ 开启                                        │
│  ┌─────────────────┐                                    │
│  │  云端优化 API   │                                    │
│  │  - DeepSeek     │                                    │
│  │  - 智谱 AI       │                                    │
│  │  - 通义千问     │                                    │
│  └────────┬────────┘                                    │
│           ↓                                             │
│  优化文本 B ──→ 对比显示 ──→ 用户选择                    │
│                                                         │
│       ↓                                                 │
│  ┌─────────────────┐                                    │
│  │  VocabularyLearningEngine  │                         │
│  │  从差异中学习                 │                       │
│  └─────────────────┘                                    │
└─────────────────────────────────────────────────────────┘
```

### 1.3 适用场景

| 场景 | 说明 | AI 增强效果 |
|------|------|-----------|
| **口语转书面语** | 将口语化表达转为书面语 | 显著 |
| **纠错** | 修正识别错误的字词 | 中等 |
| **标点优化** | 添加/修正标点符号 | 显著 |
| **格式规范化** | 统一数字、日期格式 | 中等 |
| **内容润色** | 优化表达、精简冗余 | 中等 |

---

## 2. AI 服务模式

### 2.1 支持的 AI 服务

| 服务商 | API | 价格 | 延迟 | 说明 |
|-------|-----|------|------|------|
| **DeepSeek** | Chat API | ¥0.002/1K tokens | ~500ms | 性价比高，中文优化 |
| **智谱 AI** | Chat API | ¥0.005/1K tokens | ~400ms | 国内服务，稳定 |
| **通义千问** | Chat API | ¥0.008/1K tokens | ~400ms | 阿里官方 |

### 2.2 AI 服务配置

```json
{
  "aiEnhancement": {
    "enabled": true,
    "provider": "deepseek",
    "apiKey": "sk-xxx",
    "model": "deepseek-chat",
    "autoEnhance": false,
    "maxTokens": 500,
    "timeout": 5000,
    "preserveShortcuts": true,
    "preserveProtected": true
  }
}
```

### 2.3 服务抽象层

```csharp
public interface IAIEnhancementService
{
    Task<EnhancementResult> EnhanceAsync(
        string text, 
        EnhancementOptions options,
        CancellationToken cancellationToken);
    
    Task<bool> ValidateApiKeyAsync(string apiKey);
}

public class DeepSeekService : IAIEnhancementService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl = "https://api.deepseek.com";
    
    public DeepSeekService(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            DefaultRequestHeaders =
            {
                Authorization = new AuthenticationHeaderValue("Bearer", apiKey)
            }
        };
    }
    
    public async Task<EnhancementResult> EnhanceAsync(
        string text, 
        EnhancementOptions options,
        CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(text, options);
        
        var response = await _httpClient.PostAsJsonAsync("/v1/chat/completions", new
        {
            model = "deepseek-chat",
            messages = new[]
            {
                new { role = "system", content = options.SystemPrompt },
                new { role = "user", content = prompt }
            },
            max_tokens = options.MaxTokens,
            temperature = 0.3
        }, cancellationToken);
        
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<DeepSeekResponse>(cancellationToken);
        
        return new EnhancementResult
        {
            EnhancedText = result.Choices.First().Message.Content,
            Usage = new TokenUsage
            {
                PromptTokens = result.Usage.PromptTokens,
                CompletionTokens = result.Usage.CompletionTokens,
                TotalTokens = result.Usage.TotalTokens
            }
        };
    }
    
    private string BuildPrompt(string text, EnhancementOptions options)
    {
        var prompt = new StringBuilder();
        
        prompt.AppendLine("请润色以下语音识别得到的文本：");
        prompt.AppendLine();
        prompt.AppendLine($"原文：{text}");
        prompt.AppendLine();
        prompt.AppendLine("要求：");
        prompt.AppendLine("1. 修正错别字和识别错误");
        prompt.AppendLine("2. 添加合适的标点符号");
        prompt.AppendLine("3. 将口语化表达转为书面语");
        prompt.AppendLine("4. 保持原意不变，不要添加或删除重要信息");
        
        if (options.PreserveShortcuts)
        {
            prompt.AppendLine("5. 不要改动 «» 符号包裹的内容（这是快捷短语）");
        }
        
        prompt.AppendLine();
        prompt.AppendLine("直接输出润色后的文本，不要解释。");
        
        return prompt.ToString();
    }
    
    public async Task<bool> ValidateApiKeyAsync(string apiKey)
    {
        try
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                DefaultRequestHeaders =
                {
                    Authorization = new AuthenticationHeaderValue("Bearer", apiKey)
                }
            };
            
            var response = await client.GetAsync("/v1/models");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

public class EnhancementOptions
{
    public string SystemPrompt { get; set; } = "你是一个专业的文本润色助手，擅长将口语化文本转换为书面语。";
    public int MaxTokens { get; set; } = 500;
    public bool PreserveShortcuts { get; set; } = true;
    public bool PreserveProtected { get; set; } = true;
    public EnhancementMode Mode { get; set; } = EnhancementMode.Standard;
}

public enum EnhancementMode
{
    Standard,       // 标准润色
    Formal,         // 正式书面语
    Casual,         // 保持口语风格
    Simplified      // 精简模式
}
```

---

## 3. 快捷短语保护机制

### 3.1 保护标记

```csharp
public class ShortcutProtector
{
    private const string PROTECT_START = "«";
    private const string PROTECT_END = "»";
    
    /// <summary>
    /// 标记快捷短语内容
    /// </summary>
    public string MarkShortcut(string content)
    {
        return $"{PROTECT_START}{content}{PROTECT_END}";
    }
    
    /// <summary>
    /// 提取被保护的内容
    /// </summary>
    public List<string> ExtractProtected(string text)
    {
        var matches = Regex.Matches(text, $"{PROTECT_START}(.*?){PROTECT_END}");
        return matches.Cast<Match>().Select(m => m.Groups[1].Value).ToList();
    }
    
    /// <summary>
    /// 恢复被保护的内容（AI 可能移除标记）
    /// </summary>
    public string RestoreProtected(
        string enhancedText, 
        List<string> originalProtected)
    {
        // 尝试找到并恢复被保护内容
        foreach (var protectedContent in originalProtected)
        {
            if (!enhancedText.Contains(protectedContent))
            {
                // AI 可能修改了内容，尝试模糊匹配
                // 这里可以使用更复杂的逻辑
            }
        }
        
        return enhancedText;
    }
}
```

### 3.2 AI Prompt 增强

```csharp
public class ProtectedPromptBuilder
{
    public string BuildPrompt(
        string text, 
        List<string> protectedContents,
        EnhancementOptions options)
    {
        var prompt = new StringBuilder();
        
        prompt.AppendLine("请润色以下语音识别得到的文本：");
        prompt.AppendLine();
        prompt.AppendLine($"原文：{text}");
        prompt.AppendLine();
        
        if (protectedContents.Any())
        {
            prompt.AppendLine("⚠️ 重要提示：");
            prompt.AppendLine($"以下 «» 符号包裹的内容是预设的快捷短语，请保持原样，不要修改：");
            foreach (var content in protectedContents)
            {
                prompt.AppendLine($"- {content}");
            }
            prompt.AppendLine();
        }
        
        prompt.AppendLine("润色要求：");
        prompt.AppendLine("1. 修正错别字和识别错误");
        prompt.AppendLine("2. 添加合适的标点符号");
        prompt.AppendLine("3. 将口语化表达转为书面语（适度，不要过度正式）");
        prompt.AppendLine("4. 保持原意不变");
        prompt.AppendLine("5. 不要改动 «» 符号包裹的内容");
        prompt.AppendLine();
        prompt.AppendLine("直接输出润色后的文本，不要解释。");
        
        return prompt.ToString();
    }
}
```

### 3.3 跳过 AI 处理

```csharp
public class AISkipHandler
{
    /// <summary>
    /// 检测是否应该跳过 AI 处理
    /// </summary>
    public bool ShouldSkipAI(string text, List<Shortcut> matchedShortcuts)
    {
        // 检查是否有快捷短语标记为 skipAI
        if (matchedShortcuts.Any(s => s.SkipAI))
        {
            return true;
        }
        
        // 检查是否包含特殊标记
        if (text.Contains("«") && text.Contains("»"))
        {
            var protectedContents = ExtractProtectedContents(text);
            // 如果大部分内容都是受保护的，跳过 AI
            var protectedRatio = (double)protectedContents.Sum(c => c.Length) / text.Length;
            if (protectedRatio > 0.8)
            {
                return true;
            }
        }
        
        return false;
    }
    
    private List<string> ExtractProtectedContents(string text)
    {
        var matches = Regex.Matches(text, "«(.*?)»");
        return matches.Cast<Match>().Select(m => m.Groups[1].Value).ToList();
    }
}
```

---

## 4. 流式输入与 AI 增强协调

### 4.1 两阶段处理方案

```
┌─────────────────────────────────────────────────────────────┐
│                流式输入与 AI 增强协调流程                     │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  阶段一：流式识别                                           │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  用户说话 → 本地 ASR → 实时显示文本 A                │   │
│  │                                                      │   │
│  │  [请][把][格][式][要][求]...  ← 逐字显示             │   │
│  └─────────────────────────────────────────────────────┘   │
│                          ↓                                  │
│  检测停顿/结束                                              │
│                          ↓                                  │
│  阶段二：AI 增强（可选）                                      │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  完整文本 A → AI 服务 → 优化文本 B                    │   │
│  │                                                      │   │
│  │  对比显示：                                          │   │
│  │  原文：请把格式要求说一下                            │   │
│  │  优化：请说明文档格式要求                            │   │
│  │                                                      │   │
│  │  [采用原文]  [采用优化]                              │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 4.2 事后修正模式

```csharp
public class PostCorrectionMode
{
    private readonly IAIEnhancementService _aiService;
    private readonly HistoryService _historyService;
    
    /// <summary>
    /// 事后修正：用户主动触发 AI 增强
    /// </summary>
    public async Task<EnhancementResult> CorrectAsync(
        string text, 
        CorrectionOptions options)
    {
        // 检查是否应该跳过 AI
        if (_skipHandler.ShouldSkipAI(text, options.MatchedShortcuts))
        {
            return new EnhancementResult
            {
                EnhancedText = text,
                Skipped = true,
                Reason = "快捷短语保护"
            };
        }
        
        // 调用 AI 增强
        var result = await _aiService.EnhanceAsync(text, new EnhancementOptions
        {
            PreserveShortcuts = options.PreserveShortcuts,
            Mode = options.Mode
        }, options.CancellationToken);
        
        // 记录历史，用于学习
        await _historyService.RecordCorrectionAsync(
            text, 
            result.EnhancedText, 
            options.Mode);
        
        return result;
    }
    
    /// <summary>
    /// 从用户选择中学习
    /// </summary>
    public async Task LearnFromChoiceAsync(
        string original, 
        string enhanced, 
        string chosen,
        EnhancementMode mode)
    {
        if (chosen == original)
        {
            // 用户选择原文，说明 AI 优化不理想
            await _feedbackService.RecordNegativeFeedbackAsync(enhanced);
        }
        else if (chosen == enhanced)
        {
            // 用户接受 AI 优化
            await _feedbackService.RecordPositiveFeedbackAsync(enhanced);
        }
    }
}
```

### 4.3 自动增强模式

```csharp
public class AutoEnhanceMode
{
    private readonly IAIEnhancementService _aiService;
    private readonly SettingsService _settingsService;
    
    /// <summary>
    /// 自动增强：识别完成后自动调用 AI
    /// </summary>
    public async Task<EnhancementResult> AutoEnhanceAsync(
        string text,
        AutoEnhanceOptions options)
    {
        // 检查设置
        if (!_settingsService.AIEnhancementEnabled)
        {
            return null;
        }
        
        // 检查文本长度（太短不需要增强）
        if (text.Length < options.MinLength)
        {
            return null;
        }
        
        // 检查是否应该跳过
        if (_skipHandler.ShouldSkipAI(text, options.MatchedShortcuts))
        {
            return null;
        }
        
        // 异步增强，不阻塞主流程
        var task = Task.Run(async () =>
        {
            return await _aiService.EnhanceAsync(text, new EnhancementOptions
            {
                Mode = options.Mode
            }, CancellationToken.None);
        });
        
        // 设置超时
        var completed = await Task.WhenAny(task, Task.Delay(options.Timeout));
        
        if (completed == task)
        {
            return await task;
        }
        
        // 超时，返回 null
        return null;
    }
}
```

---

## 5. Prompt 工程设计

### 5.1 系统 Prompt

```csharp
public static class SystemPrompts
{
    public const string Standard = """
        你是一个专业的语音识别文本润色助手。你的任务是将用户口语化的语音识别结果转换为通顺的书面语。
        
        请遵循以下原则：
        1. 修正明显的错别字和识别错误
        2. 添加合适的标点符号
        3. 将过于口语化的表达适度转为书面语，但不要过度正式
        4. 保持原意不变，不要添加或删除重要信息
        5. 保持自然流畅，不要生硬
        
        直接输出润色后的文本，不要有任何解释或说明。
        """;
    
    public const string Formal = """
        你是一个专业的文档编辑助手。请将以下语音识别文本转换为正式的书面语格式。
        
        要求：
        1. 使用正式的书面语表达
        2. 修正所有错别字和语法错误
        3. 使用规范的标点符号
        4. 统一数字、日期、专有名词的格式
        5. 保持专业、严谨的语气
        
        直接输出润色后的文本。
        """;
    
    public const string Medical = """
        你是一个专业的医疗文档编辑助手。请将以下语音识别文本转换为规范的医疗文书格式。
        
        要求：
        1. 使用规范的医学术语
        2. 保持客观、准确的描述
        3. 修正识别错误的医学术语
        4. 使用标准的病历格式
        
        注意：不要改动 «» 符号包裹的预设模板内容。
        
        直接输出润色后的文本。
        """;
}
```

### 5.2 Few-Shot 示例

```csharp
public static class FewShotExamples
{
    public static readonly List<(string Input, string Output)> Examples = new()
    {
        (
            "那个患者就是说他最近这几天吧就是感觉胸口有点闷然后还有点头晕",
            "患者主诉：近几日感觉胸口闷，伴有头晕。"
        ),
        (
            "帮我写个邮件给王总说一下明天开会的事情时间改到下午三点",
            "请帮我写一封邮件给王总，告知明天的会议时间已更改为下午三点。"
        ),
        (
            "今天天气真好啊适合出去散步",
            "今天天气很好，适合出去散步。"
        )
    };
}
```

### 5.3 输出格式约束

```csharp
public class OutputConstraint
{
    /// <summary>
    /// 约束输出格式
    /// </summary>
    public string BuildConstrainedPrompt(string text)
    {
        return $@"
请润色以下文本：

原文：{text}

要求：
1. 只输出润色后的文本
2. 不要添加任何解释、说明或额外内容
3. 不要使用引号包裹输出
4. 保持简洁

润色结果：";
    }
}
```

---

## 6. 成本控制策略

### 6.1 Token 限制

```csharp
public class TokenLimiter
{
    private readonly int _maxTokensPerRequest;
    private readonly int _dailyTokenLimit;
    private int _todayUsedTokens = 0;
    private DateTime _lastResetDate;
    
    public TokenLimiter(int maxPerRequest = 500, int dailyLimit = 10000)
    {
        _maxTokensPerRequest = maxPerRequest;
        _dailyTokenLimit = dailyLimit;
        _lastResetDate = DateTime.Today;
    }
    
    public bool CanProcess(string text)
    {
        // 检查是否需要重置计数
        if (DateTime.Today != _lastResetDate)
        {
            _todayUsedTokens = 0;
            _lastResetDate = DateTime.Today;
        }
        
        // 估算 token 数（中文约 1.5 字符/token）
        var estimatedTokens = (int)Math.Ceiling(text.Length / 1.5);
        
        // 检查单次限制
        if (estimatedTokens > _maxTokensPerRequest)
        {
            return false;
        }
        
        // 检查每日限制
        if (_todayUsedTokens + estimatedTokens > _dailyTokenLimit)
        {
            return false;
        }
        
        return true;
    }
    
    public void RecordUsage(int tokens)
    {
        _todayUsedTokens += tokens;
    }
}
```

### 6.2 智能截断

```csharp
public class SmartTruncator
{
    /// <summary>
    /// 智能截断过长文本
    /// </summary>
    public string Truncate(string text, int maxTokens)
    {
        if (EstimateTokens(text) <= maxTokens)
        {
            return text;
        }
        
        // 按句子分割
        var sentences = Regex.Split(text, "(?<=[.!?。！？])\\s*");
        
        // 累加句子直到达到限制
        var result = new StringBuilder();
        var currentTokens = 0;
        
        foreach (var sentence in sentences)
        {
            var sentenceTokens = EstimateTokens(sentence);
            if (currentTokens + sentenceTokens > maxTokens)
            {
                break;
            }
            
            result.Append(sentence);
            currentTokens += sentenceTokens;
        }
        
        return result.ToString().Trim();
    }
    
    private int EstimateTokens(string text)
    {
        return (int)Math.Ceiling(text.Length / 1.5);
    }
}
```

### 6.3 本地预处理

```csharp
public class LocalPreprocessor
{
    /// <summary>
    /// 本地预处理，减少 AI 调用成本
    /// </summary>
    public PreprocessResult Preprocess(string text)
    {
        var result = new PreprocessResult();
        
        // 1. 基础标点修正（本地完成）
        result.Text = FixPunctuation(text);
        
        // 2. 常见错别字修正（本地完成）
        result.Text = FixCommonTypos(result.Text);
        
        // 3. 判断是否需要 AI 增强
        result.NeedAI = ShouldUseAI(result.Text);
        
        return result;
    }
    
    private string FixPunctuation(string text)
    {
        // 在适当位置添加标点
        text = Regex.Replace(text, "吧 (?!，)", "吧，");
        text = Regex.Replace(text, "啊 (?!，)", "啊，");
        text = Regex.Replace(text, "呢 (?!，)", "呢，");
        
        // 句末添加句号
        if (!text.EndsWith(".") && !text.EndsWith("。"))
        {
            text += ".";
        }
        
        return text;
    }
    
    private string FixCommonTypos(string text)
    {
        var typos = new Dictionary<string, string>
        {
            { "登路", "登录" },
            { "账乎", "账户" },
            { "密马", "密码" },
            // ... 更多常见错别字
        };
        
        foreach (var typo in typos)
        {
            text = text.Replace(typo.Key, typo.Value);
        }
        
        return text;
    }
    
    private bool ShouldUseAI(string text)
    {
        // 如果文本已经比较规范，不需要 AI
        if (text.Length < 10) return false;
        if (text.Contains("«") && text.Contains("»")) return false;
        
        // 检查是否需要更高级的润色
        return true;
    }
}

public class PreprocessResult
{
    public string Text { get; set; }
    public bool NeedAI { get; set; }
}
```

### 6.4 使用统计

```csharp
public class AIUsageStatistics
{
    public int TodayRequests { get; set; }
    public int TodayTokens { get; set; }
    public decimal TodayCost { get; set; }
    public int MonthRequests { get; set; }
    public int MonthTokens { get; set; }
    public decimal MonthCost { get; set; }
    
    public void RecordRequest(int tokens, decimal cost)
    {
        TodayRequests++;
        TodayTokens += tokens;
        TodayCost += cost;
        
        MonthRequests++;
        MonthTokens += tokens;
        MonthCost += cost;
    }
}
```

---

## 7. 实施计划

### 7.1 第一阶段：基础功能

- [ ] AI 服务抽象层
- [ ] DeepSeek 集成
- [ ] 手动触发增强

### 7.2 第二阶段：保护机制

- [ ] 快捷短语保护
- [ ] 跳过 AI 逻辑
- [ ] Prompt 优化

### 7.3 第三阶段：高级功能

- [ ] 自动增强模式
- [ ] 本地预处理
- [ ] 成本控制

### 7.4 第四阶段：学习优化

- [ ] 从用户选择中学习
- [ ] 个性化 Prompt
- [ ] 使用统计

---

*本文档为 WordFlow AI 增强功能设计，将根据开发进度持续更新。*

*最后更新：2026-03-07*
