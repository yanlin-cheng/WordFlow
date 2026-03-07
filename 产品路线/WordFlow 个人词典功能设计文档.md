# WordFlow 个人词典功能设计文档

> 文档版本：1.0  
> 创建日期：2026-03-07  
> 状态：设计稿

---

## 目录

1. [功能概述](#1-功能概述)
2. [热词功能设计](#2-热词功能设计)
3. [快捷短语功能设计](#3-快捷短语功能设计)
4. [个人术语学习](#4-个人术语学习)
5. [数据结构设计](#5-数据结构设计)
6. [API 接口设计](#6-api-接口设计)
7. [UI 设计](#7-ui-设计)

---

## 1. 功能概述

### 1.1 产品定位

个人词典是 WordFlow 的**核心免费功能**，包括：
- **热词**：提高语音识别对特定词汇的敏感度
- **快捷短语**：语音说出触发词，展开成预设文本
- **个人术语**：从用户习惯中自动学习的专业词汇

### 1.2 设计理念

| 原则 | 说明 |
|------|------|
| **基础功能免费** | 热词、快捷短语作为软件基础功能，不收费 |
| **内容可交易** | 专业词典包由创作者制作，用户购买 |
| **本地优先** | 核心功能本地运行，保护用户隐私 |
| **易于创作** | 降低创作门槛，鼓励用户分享 |

### 1.3 功能架构

```
┌─────────────────────────────────────────────────────────┐
│                    个人词典模块                          │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌─────────────────┐    ┌─────────────────┐            │
│  │   HotwordMgr    │    │   ShortcutMgr   │            │
│  │   热词管理      │    │   快捷短语管理  │            │
│  └─────────────────┘    └─────────────────┘            │
│           ↓                      ↓                      │
│  ┌─────────────────────────────────────────┐           │
│  │         LocalVocabularyStore            │           │
│  │         (SQLite + JSON Files)           │           │
│  └─────────────────────────────────────────┘           │
│           ↓                      ↓                      │
│  ┌─────────────────┐    ┌─────────────────┐            │
│  │   PackImporter  │    │   PackExporter  │            │
│  │   安装词典包    │    │   打包上传      │            │
│  └─────────────────┘    └─────────────────┘            │
│           ↓                                              │
│  ┌─────────────────┐                                     │
│  │   TermLearner   │                                     │
│  │   术语学习引擎  │                                     │
│  └─────────────────┘                                     │
└─────────────────────────────────────────────────────────┘
```

---

## 2. 热词功能设计

### 2.1 功能说明

**作用**：提高语音识别对特定词汇的敏感度，通过给 ASR 引擎加权，使特定词汇更容易被识别出来。

**适用场景**：
- 专业术语（医学术语、法律术语、技术名词）
- 人名地名（客户名、供应商名）
- 品牌产品名
- 生僻字词

### 2.2 热词权重机制

| 权重 | 说明 | 适用场景 |
|------|------|---------|
| 1-3 | 轻微加权 | 常用词、普通名词 |
| 4-6 | 中等加权 | 专业词汇、行业术语 |
| 7-10 | 强加权 | 核心术语、易错词 |

### 2.3 热词数据结构

```json
{
  "hotwords": [
    {
      "id": "hw-001",
      "word": "室上性心动过速",
      "weight": 10,
      "category": "医疗",
      "pinyin": "shì shàng xìng xīn dòng guò sù",
      "usageCount": 42,
      "createdAt": "2026-03-01T10:00:00Z",
      "updatedAt": "2026-03-07T15:30:00Z"
    },
    {
      "id": "hw-002",
      "word": "冠状动脉",
      "weight": 8,
      "category": "医疗",
      "usageCount": 28,
      "createdAt": "2026-03-01T10:00:00Z",
      "updatedAt": "2026-03-07T15:30:00Z"
    }
  ]
}
```

### 2.4 ASR 集成

```python
# Sherpa-ONNX 热词支持
def recognize_with_hotwords(audio_data, hotwords_dict):
    """
    使用热词进行语音识别
    
    Args:
        audio_data: 音频数据
        hotwords_dict: 热词字典 {"词": 权重}
    """
    stream = recognizer.create_stream(
        hotwords=hotwords_dict
    )
    stream.accept_waveform(sample_rate, audio_data)
    recognizer.decode_stream(stream)
    return stream.result.text
```

### 2.5 热词管理功能

| 功能 | 说明 |
|------|------|
| 添加热词 | 手动添加或从历史记录导入 |
| 编辑热词 | 修改权重、分类 |
| 删除热词 | 移除不需要的热词 |
| 批量导入 | 从 Excel/CSV 文件导入 |
| 批量导出 | 导出为 JSON/CSV |
| 分类管理 | 按类别组织热词 |
| 使用统计 | 显示热词使用次数 |

---

## 3. 快捷短语功能设计

### 3.1 功能说明

**作用**：语音说出触发词，自动展开成一大段预设文本，提高输入效率。

**适用场景**：
- 常用回复模板
- 病历/法律文书模板
- 邮件签名
- 格式化文本

### 3.2 快捷短语数据结构

```json
{
  "shortcuts": [
    {
      "id": "sc-001",
      "trigger": "格式要求",
      "aliases": ["格式设置", "排版要求"],
      "content": "请将文档格式修改如下：\n1. 字体：宋体 小四号\n2. 行间距：1.5 倍\n3. 标题格式：黑体 三号 加粗",
      "category": "工作",
      "usageCount": 42,
      "isProtected": true,
      "skipAI": true,
      "showPrompt": false,
      "createdAt": "2026-03-01T10:00:00Z",
      "updatedAt": "2026-03-07T15:30:00Z"
    }
  ]
}
```

### 3.3 字段说明

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 唯一标识符 |
| `trigger` | string | 主触发词 |
| `aliases` | array | 别名列表 |
| `content` | string | 展开内容 |
| `category` | string | 分类 |
| `usageCount` | int | 使用次数统计 |
| `isProtected` | bool | 是否受保护（AI 不修改） |
| `skipAI` | bool | 是否跳过 AI 处理 |
| `showPrompt` | bool | 是否显示确认框 |

### 3.4 触发流程

```
语音识别 → "请把格式要求说一下"
    ↓
检测是否包含触发词 → "格式要求"
    ↓
匹配到快捷短语
    ↓
检查 showPrompt
    ├─ false → 直接展开 → 输出完整内容
    └─ true → 弹出选择框 → 用户确认后展开
```

### 3.5 保护机制

**保护类型**：
- `isProtected=true`：标记为受保护内容，AI 增强不修改
- `skipAI=true`：跳过 AI 处理
- 特殊符号标记：«请将文档格式修改如下...»

**AI Prompt 示例**：
```
请润色以下文本，但不要改动 «» 符号包裹的内容：

原文：请把格式要求说一下，然后再说下注意事项

«请将文档格式修改如下：1. 字体：宋体 小四号 2. 行间距：1.5 倍»

另外需要注意以下事项...
```

---

### 3.6 热词联动快捷短语建议（新增）

#### 功能说明

**作用**：在语音识别过程中，当检测到热词匹配时，自动弹出关联的快捷短语建议窗口，用户可通过快捷键快速插入，无需中断语音输入流程。

**核心价值**：
- ✅ 不中断语音输入流程
- ✅ 快捷键操作，无需鼠标
- ✅ 智能推荐，提高输入效率

#### 交互流程图

```
┌─────────────────────────────────────────────────────────────┐
│              热词匹配 → 快捷短语插入流程                     │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. 语音识别中...                                            │
│     ↓                                                       │
│  实时文本："请帮我创建一个 [患者] 的病历"                    │
│                ↑                                            │
│           检测到热词"患者"                                   │
│     ↓                                                       │
│  2. 查询关联的快捷短语                                       │
│     ↓                                                       │
│  找到 3 个相关快捷短语：                                      │
│     - "患者主诉" → "患者主诉：[症状]，持续 [时间]"           │
│     - "患者信息" → "患者姓名：[name]，性别：[sex]，年龄：[age]"  │
│     - "患者诊断" → "初步诊断：[diagnosis]"                   │
│     ↓                                                       │
│  3. 弹出快捷短语选择框（不中断识别）                         │
│     ↓                                                       │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  📌 相关快捷短语                          [ESC 关闭] │   │
│  │  ┌─────────────────────────────────────────────────┐│   │
│  │  │  1  患者主诉                                    ││   │
│  │  │  2  患者信息                                    ││   │
│  │  │  3  患者诊断                                    ││   │
│  │  └─────────────────────────────────────────────────┘│   │
│  │  [↑↓选择] [Enter 插入] [ESC 取消]                    │   │
│  └─────────────────────────────────────────────────────┘   │
│     ↓                                                       │
│  4. 用户操作                                                 │
│     ├── 按数字键 1/2/3 → 直接插入对应短语                   │
│     ├── 按 ↑↓选择 + Enter → 插入选中短语                    │
│     └── 按 ESC 或超时 → 关闭弹窗，继续识别                  │
│     ↓                                                       │
│  5. 插入快捷短语到光标位置                                   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

#### 快捷键操作

| 按键 | 功能 | 说明 |
|------|------|------|
| **数字键 1-3** | 直接插入 | 按数字键直接插入对应短语 |
| **↑ ↓** | 上下选择 | 在候选短语之间切换 |
| **Enter** | 确认插入 | 插入当前选中的短语 |
| **ESC** | 取消关闭 | 关闭弹窗，继续识别 |
| **自动超时** | 自动关闭 | 5 秒无操作自动关闭 |

#### 技术实现要点

| 要点 | 说明 |
|------|------|
| **非阻塞式设计** | 弹窗 `ShowActivated=false`，不抢占输入焦点 |
| **全局键盘钩子** | 即使焦点不在弹窗也能响应快捷键 |
| **独立线程** | 弹窗显示和键盘监听独立于语音识别主流程 |

#### 数据结构扩展

```json
{
  "shortcuts": [
    {
      "id": "sc-001",
      "trigger": "患者主诉",
      "content": "患者主诉：[症状]，持续 [时间]",
      "category": "医疗",
      "linkedHotwords": ["患者", "主诉", "症状"],  // 新增：关联热词
      "priority": 10  // 新增：优先级，用于排序
    }
  ]
}
```

#### 设置选项

```
┌─────────────────────────────────────────────────────┐
│  快捷短语建议设置                                   │
├─────────────────────────────────────────────────────┤
│                                                     │
│  ☑ 启用热词联动快捷短语建议                         │
│                                                     │
│  防打扰设置：                                       │
│  ☑ 同一热词 [30] 秒内不重复弹出                     │
│  ☑ 连续 [3] 次取消则本次语音输入不再弹出            │
│                                                     │
│  自动关闭：                                         │
│  ○ [5] 秒后自动关闭                                 │
│  ○ 不自动关闭                                       │
│                                                     │
│  最多显示：[5] 个建议                                │
│                                                     │
│         [恢复默认]        [保存]                    │
└─────────────────────────────────────────────────────┘
```

#### UI 设计

```
┌─────────────────────────────────────────────────────────────┐
│  📌 相关快捷短语                                  [×]        │
├─────────────────────────────────────────────────────────────┤
│  匹配热词：患者                                             │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  [1] 患者主诉                                       │   │
│  │      → 患者主诉：[症状]，持续 [时间]                │   │
│  ├─────────────────────────────────────────────────────┤   │
│  │  [2] 患者信息                                       │   │
│  │      → 患者姓名：[name]，性别：[sex]，年龄：[age]   │   │
│  ├─────────────────────────────────────────────────────┤   │
│  │  [3] 患者诊断                                       │   │
│  │      → 初步诊断：[diagnosis]                        │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  [↑↓选择] [Enter 插入] [1-3 快捷插入] [ESC 取消]            │
└─────────────────────────────────────────────────────────────┘
```

#### 实现代码示例

```csharp
// 快捷短语建议弹窗
public class ShortcutSuggestionPopup : Window
{
    private int _selectedIndex = 0;
    private List<Shortcut> _shortcuts;
    private DispatcherTimer _autoCloseTimer;
    
    public ShortcutSuggestionPopup(List<Shortcut> shortcuts)
    {
        InitializeComponent();
        _shortcuts = shortcuts;
        
        // 关键设置：弹窗不抢占输入焦点
        ShowActivated = false;  // 不激活窗口
        Topmost = true;         // 保持置顶
        Focusable = false;      // 不可聚焦
        
        // 自动关闭定时器
        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _autoCloseTimer.Tick += (s, e) => Close();
        _autoCloseTimer.Start();
    }
    
    // 插入快捷短语
    private void InsertShortcut(int index)
    {
        if (index >= 0 && index < _shortcuts.Count)
        {
            var shortcut = _shortcuts[index];
            _keyboardSimulator.SimulateTextInsertion(shortcut.Content);
            Close();
        }
    }
}

// 全局键盘钩子
public class GlobalKeyboardHook
{
    // 监听全局按键，即使焦点不在弹窗也能响应
    public static void Install()
    {
        _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, ...);
    }
}
```

---

## 4. 个人术语学习

### 4.1 功能说明

**作用**：从用户历史记录和修正中学习，自动发现用户常用的专业词汇和易错词。

### 4.2 学习流程

```
┌─────────────────────────────────────────────────────────┐
│                    术语学习流程                          │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  用户输入 → 记录到历史库                                 │
│       ↓                                                 │
│  词频分析 → 发现高频词                                   │
│       ↓                                                 │
│  候选词筛选 → 过滤常用词，保留专业词                     │
│       ↓                                                 │
│  用户确认 → 加入热词表                                   │
│       ↓                                                 │
│  持续学习 → 根据使用情况调整权重                         │
└─────────────────────────────────────────────────────────┘
```

### 4.3 代码示例

```csharp
public class HotwordLearner
{
    private Dictionary<string, int> _wordFreq = new();
    private HashSet<string> _commonWords; // 常用词过滤表
    
    public HotwordLearner()
    {
        // 加载常用词表
        _commonWords = LoadCommonWords();
    }
    
    /// <summary>
    /// 记录用户输入
    /// </summary>
    public void RecordInput(string text)
    {
        var words = Segment(text);
        foreach (var word in words)
        {
            // 过滤常用词
            if (_commonWords.Contains(word)) continue;
            
            // 过滤太短或太长的词
            if (word.Length < 2 || word.Length > 10) continue;
            
            if (_wordFreq.ContainsKey(word))
                _wordFreq[word]++;
            else
                _wordFreq[word] = 1;
        }
    }
    
    /// <summary>
    /// 生成候选热词（频率>=10 的词）
    /// </summary>
    public List<CandidateHotword> GenerateCandidates()
    {
        return _wordFreq
            .Where(kvp => kvp.Value >= 10)
            .Select(kvp => new CandidateHotword
            {
                Word = kvp.Key,
                Frequency = kvp.Value,
                SuggestedWeight = Math.Min(kvp.Value / 5, 10)
            })
            .OrderByDescending(x => x.Frequency)
            .ToList();
    }
    
    /// <summary>
    /// 分词（使用 Jieba 或类似库）
    /// </summary>
    private List<string> Segment(string text)
    {
        // 使用 Python Jieba 或.NET 分词库
        return JiebaSegment.Cut(text).ToList();
    }
}

public class CandidateHotword
{
    public string Word { get; set; }
    public int Frequency { get; set; }
    public int SuggestedWeight { get; set; }
}
```

### 4.4 从修正中学习

```csharp
public class CorrectionLearner
{
    /// <summary>
    /// 从用户修正中学习
    /// </summary>
    public void LearnFromCorrection(string original, string corrected)
    {
        // 检测差异
        var diff = FindDifference(original, corrected);
        
        if (diff != null)
        {
            // 记录纠错对
            _correctionLog.Add(new CorrectionEntry
            {
                WrongWord = diff.Original,
                CorrectWord = diff.Corrected,
                Count = 1
            });
        }
    }
    
    /// <summary>
    /// 获取纠错建议
    /// </summary>
    public string GetCorrectionSuggestion(string text)
    {
        foreach (var entry in _correctionLog)
        {
            if (text.Contains(entry.WrongWord))
            {
                return text.Replace(entry.WrongWord, entry.CorrectWord);
            }
        }
        return text;
    }
}
```

---

## 5. 数据结构设计

### 5.1 数据库 Schema

```sql
-- 热词表
CREATE TABLE hotwords (
    id TEXT PRIMARY KEY,
    word TEXT NOT NULL,
    weight INTEGER DEFAULT 5,
    category TEXT,
    pinyin TEXT,
    usage_count INTEGER DEFAULT 0,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- 快捷短语表
CREATE TABLE shortcuts (
    id TEXT PRIMARY KEY,
    trigger_word TEXT NOT NULL,
    content TEXT NOT NULL,
    aliases TEXT,  -- JSON 数组
    category TEXT,
    usage_count INTEGER DEFAULT 0,
    is_protected INTEGER DEFAULT 0,
    skip_ai INTEGER DEFAULT 0,
    show_prompt INTEGER DEFAULT 0,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- 纠错记录表
CREATE TABLE correction_log (
    id TEXT PRIMARY KEY,
    wrong_word TEXT NOT NULL,
    correct_word TEXT NOT NULL,
    count INTEGER DEFAULT 1,
    last_used_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- 输入历史表
CREATE TABLE input_history (
    id TEXT PRIMARY KEY,
    text TEXT NOT NULL,
    source TEXT,  -- 'voice' | 'keyboard'
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- 词典包表
CREATE TABLE vocabulary_packs (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    category TEXT,
    is_installed INTEGER DEFAULT 0,
    install_path TEXT,
    purchased_at DATETIME,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

### 5.2 词典包格式

```
medical-standard.zip
├── manifest.json        # 词典元信息
├── hotwords.json        # 热词列表
├── shortcuts.json       # 快捷短语列表
├── terms.json           # 专业术语表
├── preview.txt          # 预览内容（10-20%）
└── thumbnail.png        # 封面图片（400x300）
```

**manifest.json 示例**：
```json
{
  "id": "medical-standard",
  "name": "医疗行业标准词典",
  "version": "1.0.0",
  "author": "WordFlow 官方",
  "authorId": "wordflow-official",
  "category": "医疗",
  "description": "包含 5000+ 医学术语、300+ 常用快捷短语",
  "price": 29.00,
  "currency": "CNY",
  "isPremium": true,
  "preview": "preview.txt",
  "thumbnail": "thumbnail.png",
  "files": {
    "hotwords": "hotwords.json",
    "shortcuts": "shortcuts.json",
    "terms": "terms.json"
  },
  "stats": {
    "hotwordCount": 5000,
    "shortcutCount": 300,
    "termCount": 2000
  },
  "createdAt": "2026-03-01T00:00:00Z",
  "updatedAt": "2026-03-07T00:00:00Z"
}
```

---

## 6. API 接口设计

### 6.1 本地 API（C#）

```csharp
public interface IVocabularyService
{
    // 热词管理
    Task<List<Hotword>> GetHotwordsAsync(string category = null);
    Task<Hotword> AddHotwordAsync(Hotword hotword);
    Task UpdateHotwordAsync(Hotword hotword);
    Task DeleteHotwordAsync(string id);
    Task ImportHotwordsAsync(IEnumerable<Hotword> hotwords);
    Task ExportHotwordsAsync(string filePath);
    
    // 快捷短语管理
    Task<List<Shortcut>> GetShortcutsAsync(string category = null);
    Task<Shortcut> AddShortcutAsync(Shortcut shortcut);
    Task UpdateShortcutAsync(Shortcut shortcut);
    Task DeleteShortcutAsync(string id);
    Task<string> ExpandShortcutAsync(string trigger);
    
    // 词典包管理
    Task<List<VocabularyPack>> GetInstalledPacksAsync();
    Task InstallPackAsync(string packPath);
    Task UninstallPackAsync(string packId);
    
    // 学习功能
    Task RecordInputAsync(string text);
    Task<List<CandidateHotword>> GetLearningCandidatesAsync();
    Task AcceptCandidateAsync(CandidateHotword candidate);
}
```

### 6.2 ASR 服务 API（Python）

```python
# ASR 服务端热词配置
@app.route('/api/v1/hotwords', methods=['POST'])
def set_hotwords():
    """设置热词列表"""
    data = request.json
    hotwords = data.get('hotwords', {})  # {"词": 权重}
    
    asr_service.set_hotwords(hotwords)
    
    return jsonify({'success': True})

@app.route('/api/v1/hotwords', methods=['GET'])
def get_hotwords():
    """获取当前热词"""
    return jsonify({
        'hotwords': asr_service.hotwords
    })
```

---

## 7. UI 设计

### 7.1 词库管理窗口

```
┌─────────────────────────────────────────────────────────────┐
│  个人词库管理                                    [×]        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  [词库] [热词] [快捷短语] [学习]                            │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  搜索词库...                                [+ 添加] │   │
│  ├─────────────────────────────────────────────────────┤   │
│  │                                                     │   │
│  │  已安装词典包                                        │   │
│  │  ┌─────────────────────────────────────────────┐   │   │
│  │  │ 📦 医疗行业标准词典                    [卸载] │   │   │
│  │  │    5000 热词 | 300 快捷短语                  │   │   │
│  │  └─────────────────────────────────────────────┘   │   │
│  │                                                     │   │
│  │  本地热词                                            │   │
│  │  ┌─────────────────────────────────────────────┐   │   │
│  │  │ 室上性心动过速    权重:10    医疗      [编辑] │   │   │
│  │  │ 冠状动脉          权重:8     医疗      [编辑] │   │   │
│  │  └─────────────────────────────────────────────┘   │   │
│  │                                                     │   │
│  │  快捷短语                                            │   │
│  │  ┌─────────────────────────────────────────────┐   │   │
│  │  │ 格式要求 → 请将文档格式修改如下...    [编辑] │   │   │
│  │  │ 主诉 → 患者主诉：[症状描述]...       [编辑] │   │   │
│  │  └─────────────────────────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  [导入词典包] [导出词典包]             [关闭]               │
└─────────────────────────────────────────────────────────────┘
```

### 7.2 添加热词对话框

```
┌─────────────────────────────────────────┐
│  添加热词                          [×]  │
├─────────────────────────────────────────┤
│                                         │
│  词汇 *    [___________________]        │
│                                         │
│  权重      [▼ 10 - 强加权  ]            │
│            (1-轻微，5-中等，10-强)       │
│                                         │
│  分类      [▼ 医疗         ]            │
│                                         │
│  拼音      [shì shàng xìng...]          │
│                                         │
│         [取消]        [确定]            │
└─────────────────────────────────────────┘
```

### 7.3 添加快捷短语对话框

```
┌─────────────────────────────────────────┐
│  添加快捷短语                      [×]  │
├─────────────────────────────────────────┤
│                                         │
│  触发词 *   [___________________]       │
│                                         │
│  别名       [___________________]       │
│             (多个用逗号分隔)             │
│                                         │
│  分类       [▼ 工作         ]           │
│                                         │
│  展开内容 * [___________________]       │
│             [___________________]       │
│             [___________________]       │
│                                         │
│  □ 展开前显示确认框                      │
│  □ 跳过 AI 增强处理                      │
│                                         │
│         [取消]        [确定]            │
└─────────────────────────────────────────┘
```

### 7.4 学习候选词对话框

```
┌─────────────────────────────────────────┐
│  发现常用词汇                      [×]  │
├─────────────────────────────────────────┤
│                                         │
│  检测到以下词汇您经常使用，              │
│  是否加入热词表？                        │
│                                         │
│  ┌───────────────────────────────────┐ │
│  │ ☑ 室上性心动过速 (42 次)  建议权重:8│ │
│  │ ☑ 冠状动脉 (28 次)        建议权重:6│ │
│  │ ☐ 心电图 (15 次)          建议权重:3│ │
│  └───────────────────────────────────┘ │
│                                         │
│         [全部忽略]   [添加选中]         │
└─────────────────────────────────────────┘
```

---

## 8. 实施计划

### 8.1 第一阶段：基础功能

- [ ] 热词 CRUD 管理
- [ ] 快捷短语 CRUD 管理
- [ ] 本地数据库设计
- [ ] UI 界面实现

### 8.2 第二阶段：学习功能

- [ ] 输入历史记录
- [ ] 词频分析
- [ ] 候选词推荐
- [ ] 纠错学习

### 8.3 第三阶段：词典包

- [ ] 词典包导入/导出
- [ ] 词典包格式规范
- [ ] 安装/卸载功能

### 8.4 第四阶段：市场集成

- [ ] 云端市场 API
- [ ] 购买验证
- [ ] 在线安装

---

*本文档为 WordFlow 个人词典功能设计，将根据开发进度持续更新。*

*最后更新：2026-03-07*
