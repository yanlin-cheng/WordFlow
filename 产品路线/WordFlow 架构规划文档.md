# WordFlow 架构规划文档

**版本：** v2.0  
**更新日期：** 2026 年 3 月 2 日  
**状态：** 进行中

---

## 一、项目愿景

### 1.1 产品定位

WordFlow 是一款面向专业用户的**离线语音输入工具**，提供高精度、低延迟的语音转文字功能。

### 1.2 目标用户

- **专业人士**：医生、律师、记者等需要大量文字输入的职业
- **内容创作者**：作家、博主、UP 主等
- **技术人员**：程序员、工程师等
- **普通用户**：需要高效输入的所有人

### 1.3 核心价值主张

| 价值 | 说明 |
|------|------|
| **离线优先** | 核心功能完全离线，保护用户隐私，无需担心网络问题 |
| **高精度识别** | 支持多模型切换，适应不同场景和语种 |
| **专业词库** | 支持医疗、法律、编程等专业领域词汇 |
| **持续学习** | 从用户修正中学习，不断优化识别效果 |
| **跨设备同步** | 个人词库、使用数据云端备份（可选） |

---

## 二、参考项目深度分析

### 2.1 CapsWriter-Offline 分析

**项目地址：** https://github.com/HaujetZhao/CapsWriter-Offline

#### 2.1.1 项目概况

CapsWriter-Offline 是一个开源的离线语音输入工具，主要特点：
- 使用 Python 开发
- 基于 sherpa-onnx 语音识别引擎
- 支持按 CapsLock 键录音，松开识别
- 完全离线运行

#### 2.1.2 架构分析

```
┌─────────────────────────────────────────────┐
│           单一 Python 进程                    │
│  ┌─────────────────────────────────────┐   │
│  │  main.py                            │   │
│  │  - GUI (Tkinter)                    │   │
│  │  - 热键监听 (keyboard 库)            │   │
│  │  - 音频录制 (pyaudio)               │   │
│  │  - 语音识别 (sherpa-onnx)           │   │
│  │  - 模型管理                         │   │
│  │  - 文本输出 (剪贴板/直接输入)        │   │
│  └─────────────────────────────────────┘   │
└─────────────────────────────────────────────┘
```

#### 2.1.3 文件结构

```
CapsWriter-Offline/
├── main.py                 # 主程序（约 1000 行）
├── requirements.txt        # Python 依赖
├── models/                 # 模型文件
│   └── sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17/
├── assets/                 # 资源文件
│   └── icon.png
└── README.md
```

#### 2.1.4 优点分析

| 优点 | 说明 | 可借鉴之处 |
|------|------|-----------|
| **架构简单** | 所有逻辑在一个 Python 文件中，易于理解和修改 | 保持代码简洁，避免过度设计 |
| **部署方便** | 只需 Python 环境和依赖包 | 使用嵌入 Python，减少用户配置 |
| **交互直观** | CapsLock 键录音，符合直觉 | 保持简洁的交互方式 |
| **轻量级** | 核心代码约 1000 行 | 避免臃肿，专注核心功能 |
| **开源生态** | 使用成熟的 sherpa-onnx | 利用开源，不重复造轮子 |

#### 2.1.5 缺点分析

| 缺点 | 说明 | 改进方向 |
|------|------|---------|
| **扩展性差** | 所有功能耦合在一起 | 采用模块化架构 |
| **UI 简陋** | Tkinter 界面不够美观 | 使用 WPF 打造专业界面 |
| **无商业功能** | 无付费验证、用户系统 | 设计 LicenseService |
| **无云同步** | 数据仅本地存储 | 增加 CloudSyncService |
| **无词库管理** | 不支持专业词库 | 实现 VocabularyService |

#### 2.1.6 技术选型

| 组件 | 技术 | 评价 |
|------|------|------|
| UI 框架 | Tkinter | ⭐⭐ 简单但简陋 |
| 热键监听 | keyboard 库 | ⭐⭐⭐⭐ 跨平台，但需要管理员权限 |
| 音频录制 | pyaudio | ⭐⭐⭐ 成熟但有依赖 |
| 语音识别 | sherpa-onnx | ⭐⭐⭐⭐⭐ 轻量级，无需 PyTorch |

---

### 2.2 Typeless 分析

**项目地址：** https://typeless.ai

#### 2.2.1 项目概况

Typeless 是一个 AI 语音输入工具，从安装目录分析：
- 使用 Electron 框架
- 跨平台支持（Windows/macOS/Linux）
- 商业化产品

#### 2.2.2 安装目录结构分析

根据用户提供的截图：

```
C:\Users\成彦霖\AppData\Local\Programs\Typeless\
├── locales/                    # 多语言资源
├── resources/                  # 应用资源
│   ├── app.asar               # 主应用打包 (289MB)
│   ├── app.asar.unpacked/     # 解包的原生模块
│   │   ├── build/
│   │   ├── drizzle/           # 数据库 ORM
│   │   ├── lib/
│   │   └── locales/
│   ├── build/
│   ├── drizzle/               # 数据库 ORM（重复？）
│   ├── lib/
│   └── locales/
├── chrome_100_percent.pak     # Chromium 资源包
├── chrome_200_percent.pak
├── d3dcompiler_47.dll         # DirectX 编译器
├── ffmpeg.dll                 # 音视频处理库
├── icudtl.dat                 # ICU 数据
├── libEGL.dll                 # OpenGL ES
├── libGLESv2.dll
├── LICENSE.electron.txt
├── LICENSES.chromium.html
├── resources.pak
├── snapshot_blob.bin          # V8 引擎快照
├── Typeless.exe               # 主程序 (184MB)
├── Uninstall Typeless.exe     # 卸载程序
├── v8_context_snapshot.bin    # V8 上下文快照
├── vcruntime140.dll           # VC++ 运行时
├── vcruntime140_1.dll
├── vk_swiftshader.dll         # Vulkan 软件渲染
├── vk_swiftshader_icd.json
└── vulkan-1.dll               # Vulkan 运行时
```

#### 2.2.3 架构分析

```
┌─────────────────────────────────────────────────────┐
│                  Electron 前端                       │
│  ┌─────────────────────────────────────────────┐   │
│  │  React/Vue + TypeScript                     │   │
│  │  - UI 渲染 (Chromium)                        │   │
│  │  - 用户交互                                 │   │
│  │  - 设置管理                                 │   │
│  └─────────────────────────────────────────────┘   │
│                      ↓ IPC                          │
│  ┌─────────────────────────────────────────────┐   │
│  │  Node.js 后端                               │   │
│  │  - 文件系统访问                             │   │
│  │  - 原生模块调用                             │   │
│  │  - 自动更新 (electron-updater)              │   │
│  │  - 语音识别 (可能调用外部 API 或本地模型)     │   │
│  └─────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

#### 2.2.4 优点分析

| 优点 | 说明 | 可借鉴之处 |
|------|------|-----------|
| **跨平台** | Electron 一次开发多端运行 | 考虑使用 .NET MAUI 或 Avalonia |
| **ASAR 打包** | 代码打包成单一归档，保护源码 | 使用 ILRepack 或 ConfuserEx |
| **自动更新** | electron-updater 成熟方案 | 实现类似机制 |
| **现代 UI** | 使用 Web 技术，界面美观 | WPF 也可以打造现代界面 |
| **商业化** | 有完整的付费系统 | 设计 LicenseService |

#### 2.2.5 缺点分析

| 缺点 | 说明 | WordFlow 的改进方向 |
|------|------|-------------------|
| **体积大** | 主程序 184MB + 资源 289MB = 473MB | .NET 原生应用可控制在 100MB 内 |
| **内存占用高** | Electron 应用普遍内存占用高 | .NET WPF 内存占用更低 |
| **启动慢** | Chromium 启动较慢 | .NET 启动更快 |
| **依赖 Chromium** | 需要 bundled Chromium | 无此依赖 |

#### 2.2.6 技术选型

| 组件 | 技术 | 评价 |
|------|------|------|
| UI 框架 | Electron (React/Vue) | ⭐⭐⭐⭐ 美观但臃肿 |
| 打包 | ASAR | ⭐⭐⭐⭐ 保护源码 |
| 数据库 | Drizzle ORM | ⭐⭐⭐⭐ 轻量级 ORM |
| 自动更新 | electron-updater | ⭐⭐⭐⭐⭐ 成熟方案 |

---

### 2.3 两项目对比总结

| 维度 | CapsWriter-Offline | Typeless |
|------|-------------------|----------|
| **架构** | 单一 Python 进程 | Electron + Node.js |
| **安装包大小** | ~50MB（不含模型） | ~473MB |
| **内存占用** | 低（~200MB） | 高（~500MB+） |
| **UI 美观度** | ⭐⭐ | ⭐⭐⭐⭐ |
| **扩展性** | ⭐⭐ | ⭐⭐⭐⭐ |
| **商业化程度** | ⭐（开源免费） | ⭐⭐⭐⭐⭐（商业产品） |
| **离线支持** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐（可能依赖 API） |

---

## 三、WordFlow 目标架构

### 3.1 架构设计原则

1. **离线优先**：核心功能完全离线，网络仅用于同步和更新
2. **模块化**：清晰的服务分层，便于扩展和维护
3. **轻量级**：安装包控制在 100MB 内（不含模型）
4. **高性能**：启动快、内存占用低
5. **商业化**：内置付费验证、用户系统

### 3.2 整体架构图

```
┌─────────────────────────────────────────────────────────┐
│                    WordFlow 客户端                       │
│  ┌─────────────────────────────────────────────────┐   │
│  │              .NET WPF 界面层                     │   │
│  │  - 主窗口 (MainWindow)                          │   │
│  │  - 设置窗口 (SettingsWindow)                    │   │
│  │  - 词库管理 (VocabularyManagerWindow)           │   │
│  │  - 模型管理 (ModelManagerWindow)                │   │
│  │  - 首次运行向导 (FirstRunWizard)                │   │
│  │  - 录音指示器 (RecordingIndicatorWindow)        │   │
│  └─────────────────────────────────────────────────┘   │
│                          ↓                               │
│  ┌─────────────────────────────────────────────────┐   │
│  │              .NET 服务层                         │   │
│  │  - SettingsService (设置管理)                   │   │
│  │  - HistoryService (历史记录/SQLite)             │   │
│  │  - VocabularyService (词库管理)                 │   │
│  │  - VocabularyLearningEngine (词库学习)          │   │
│  │  - GlobalHotkeyService (全局热键)               │   │
│  │  - TrayService (系统托盘)                       │   │
│  │  - ModelDownloadService (模型下载)              │   │
│  │  - SpeechRecognitionService (语音识别客户端)     │   │
│  │  - LicenseService (正版认证) ★新增              │   │
│  │  - UserService (用户账户) ★新增                 │   │
│  │  - FeatureFlagService (功能开关) ★新增          │   │
│  │  - CloudSyncService (云同步) ★新增              │   │
│  └─────────────────────────────────────────────────┘   │
│                          ↓ HTTP                        │
│  ┌─────────────────────────────────────────────────┐   │
│  │          Python ASR 服务 (独立进程)              │   │
│  │  - Sherpa-ONNX 语音识别                         │   │
│  │  - 模型加载/切换                                 │   │
│  │  - 音频预处理                                   │   │
│  │  - VAD 语音活动检测 ★新增                        │   │
│  └─────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
                          ↓ HTTPS
┌─────────────────────────────────────────────────────────┐
│                   WordFlow 云平台 ★新增                  │
│  - 用户认证 (JWT)                                       │
│  - 付费验证 (License 验证)                              │
│  - 词库同步 / 分享                                      │
│  - 模型更新推送                                          │
│  - 使用统计分析                                          │
└─────────────────────────────────────────────────────────┘
```

### 3.3 目录结构规划

```
WordFlow/
├── WordFlow.exe              # .NET 主程序
├── WordFlow.dll              # 主程序集
├── appsettings.json          # 应用配置
├── Data/
│   └── models.json           # 模型配置
├── PythonASR/
│   ├── python/               # 嵌入的 Python 环境 (~50MB)
│   │   ├── python.exe
│   │   ├── python311.dll
│   │   ├── Lib/
│   │   └── site-packages/    # 预装依赖
│   ├── asr_server.py         # ASR 服务端
│   ├── requirements.txt      # Python 依赖
│   └── models/               # 模型文件（可选，可下载）
│       ├── paraformer-zh/
│       └── sensevoice-small/
├── Resources/
│   └── icon.ico              # 应用图标
└── Logs/                     # 日志文件
```

### 3.4 安装包结构

```
WordFlow_Setup.exe (安装程序，约 60MB)
    ↓ 安装到
C:\Program Files (x86)\WordFlow\
├── WordFlow.exe              # .NET 主程序 (~10MB)
├── WordFlow.dll              # 主程序集
├── appsettings.json
├── Data/
│   └── models.json
├── PythonASR/
│   ├── python/               # Python 环境 (~50MB)
│   └── asr_server.py
└── models/                   # 可选，预装模型 (~200MB)
```

**安装包大小估算：**
- 基础安装包：~60MB（含 Python 环境，不含模型）
- 完整安装包：~300MB（含一个预装模型）
- 可选：精简安装包 ~10MB（Python 和模型都需在线下载）

---

## 四、核心模块详细设计

### 4.1 语音识别模块

**设计原则：**
- Python ASR 服务保持独立进程
- 通过 HTTP API 通信（简单、跨语言）
- 支持模型热切换
- 支持流式识别（未来）

**HTTP API 设计：**
```python
# 服务端 API
GET  /health          # 健康检查
GET  /models          # 获取可用模型
POST /recognize       # 语音识别
POST /load_model      # 切换模型

# 未来扩展
WS   /stream         # 流式识别 (WebSocket)
POST /vad_config      # VAD 配置
```

**客户端调用示例：**
```csharp
public class SpeechRecognitionService
{
    private readonly string _baseUrl = "http://127.0.0.1:5000";
    private readonly HttpClient _httpClient;
    
    public async Task<string> RecognizeAsync(byte[] audioData)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/recognize", new
        {
            audio = Convert.ToBase64String(audioData),
            sample_rate = 16000
        });
        return await response.Content.ReadAsStringAsync();
    }
}
```

**优化方向：**
1. **VAD 语音活动检测**：自动判断说话开始/结束，无需手动控制
2. **流式识别**：边说边出结果，降低延迟
3. **多模型支持**：支持中英文、方言、专业领域模型
4. **模型量化**：支持 int8 量化模型，减少内存占用

### 4.2 词库管理模块

**当前架构：**
```
VocabularyService (C#)
    ↓
HistoryService (SQLite)
    ↓
PersonalVocabulary / CorrectionLog / InputHistory
```

**扩展设计：**
```
┌─────────────────────────────────────┐
│         VocabularyService           │
│  - 词库 CRUD                        │
│  - 词库导入/导出                     │
│  - 词库分类管理                     │
│  - 权重调整                         │
│  - 词库搜索                         │
└─────────────────────────────────────┘
            ↓
┌─────────────────────────────────────┐
│      VocabularyLearningEngine       │
│  - 从用户修正学习                    │
│  - 自动发现专业词汇                  │
│  - 词频统计                         │
│  - 相似词推荐                       │
└─────────────────────────────────────┘
            ↓
┌─────────────────────────────────────┐
│         CloudSyncService            │
│  - 词库上传/下载                     │
│  - 冲突解决                         │
│  - 版本历史                         │
│  - 词库分享                         │
└─────────────────────────────────────┘
```

**词库分类：**
| 分类 | 说明 | 示例 |
|------|------|------|
| 通用词库 | 基础词汇 | 日常用语、常用词 |
| 专业词库 | 行业术语 | 医疗、法律、编程 |
| 人名库 | 人名、地名 | 客户名、供应商 |
| 自定义词库 | 用户创建 | 个人专有词汇 |

### 4.3 正版认证模块 ★新增

**设计目标：**
- 防止盗版传播
- 支持离线验证
- 支持在线验证（增强安全性）
- 机器码绑定

**架构设计：**
```
┌─────────────────────────────────────┐
│         LicenseService              │
│                                     │
│  ┌─────────────────────────────┐   │
│  │ 离线验证                     │   │
│  │ - 机器码生成                │   │
│  │ - 许可证文件验证             │   │
│  │ - 加壳保护                   │   │
│  └─────────────────────────────┘   │
│                                     │
│  ┌─────────────────────────────┐   │
│  │ 在线验证                     │   │
│  │ - 用户登录                   │   │
│  │ - 许可证状态检查             │   │
│  │ - 定期心跳                   │   │
│  └─────────────────────────────┘   │
└─────────────────────────────────────┘
```

**机器码生成：**
```csharp
public static class MachineId
{
    public static string Generate()
    {
        // 组合 CPU ID + 主板 ID + 硬盘序列号
        var cpuId = GetCpuId();
        var motherboardId = GetMotherboardId();
        var diskId = GetDiskSerialNumber();
        
        var combined = $"{cpuId}-{motherboardId}-{diskId}";
        return SHA256.Hash(combined);
    }
    
    private static string GetCpuId()
    {
        // 使用 WMI 获取 CPU ID
        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessorId FROM Win32_Processor");
        return searcher.Get().Cast<ManagementBaseObject>()
            .FirstOrDefault()?["ProcessorId"]?.ToString() ?? "";
    }
}
```

**许可证文件格式：**
```json
{
  "license_key": "WORDFLOW-XXXX-XXXX-XXXX-XXXX",
  "machine_id": "abc123...",
  "user_id": "user@email.com",
  "user_name": "张三",
  "issued_at": "2024-01-01T00:00:00Z",
  "expires_at": "2025-01-01T00:00:00Z",
  "license_type": "professional",
  "features": ["basic", "pro", "cloud_sync"],
  "max_devices": 3,
  "signature": "RSA 签名..."
}
```

**加壳保护：**
- 使用 VMProtect 或 ConfuserEx
- 混淆关键代码（LicenseService、UserService）
- 反调试保护
- 代码虚拟化

### 4.4 付费功能设计

**功能分级：**

| 功能 | 免费版 | 专业版 (¥99/年) | 企业版 (¥499/年) |
|------|------|--------|--------|
| 基础语音识别 | ✓ | ✓ | ✓ |
| 模型数量 | 1 个 | 3 个 | 无限 |
| 个人词库 | 本地 100 词 | 云同步 1000 词 | 无限 |
| 专业词库 | 基础 | 全部 | 全部 + 定制 |
| 词库分享 | ✗ | ✓ | ✓ |
| API 调用 | ✗ | ✓ | ✓ |
| 优先支持 | ✗ | ✗ | ✓ |
| 设备数 | 1 | 3 | 10 |

**实现架构：**
```csharp
public class FeatureFlagService
{
    private readonly LicenseService _licenseService;
    
    public bool CanUseFeature(Feature feature)
    {
        var license = _licenseService.GetCurrentLicense();
        if (license == null) return false;
        
        return license.Features.Contains(feature.ToString());
    }
    
    public int GetModelLimit()
    {
        var license = _licenseService.GetCurrentLicense();
        return license?.Type switch
        {
            "free" => 1,
            "professional" => 3,
            "enterprise" => int.MaxValue,
            _ => 1
        };
    }
    
    public void RecordFeatureUsage(Feature feature)
    {
        // 记录功能使用情况，用于统计分析
        _analyticsService.Track("feature_usage", new { feature });
    }
}

public enum Feature
{
    BasicRecognition,
    MultiModel,
    CloudSync,
    VocabularyShare,
    ApiAccess,
    PrioritySupport
}
```

### 4.5 词库平台设计 ★新增

**平台架构：**
```
┌─────────────────────────────────────────────────────┐
│                  WordFlow 客户端                     │
│  ┌─────────────────────────────────────────────┐   │
│  │  VocabularyManagerWindow                    │   │
│  │  - 词库浏览                                 │   │
│  │  - 搜索/筛选                                │   │
│  │  - 导入/导出                                │   │
│  │  - 上传/分享                                │   │
│  │  - 购买付费词库                             │   │
│  └─────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
                        ↓ REST API
┌─────────────────────────────────────────────────────┐
│                  WordFlow 云平台                     │
│  ┌─────────────────────────────────────────────┐   │
│  │  API Gateway                                │   │
│  │  - 请求路由                                 │   │
│  │  - 认证鉴权                                 │   │
│  │  - 限流                                     │   │
│  └─────────────────────────────────────────────┘   │
│         ↓                                          │
│  ┌─────────────────────────────────────────────┐   │
│  │  Vocabulary Service                         │   │
│  │  - 词库 CRUD                                │   │
│  │  - 版本管理                                 │   │
│  │  - 评分/评论                                │   │
│  │  - 下载统计                                 │   │
│  │  - 支付集成                                 │   │
│  └─────────────────────────────────────────────┘   │
│         ↓                                          │
│  ┌─────────────────────────────────────────────┐   │
│  │  Database (PostgreSQL)                      │   │
│  │  - vocabularies                             │   │
│  │  - categories                               │   │
│  │  - downloads                                │   │
│  │  - reviews                                  │   │
│  │  - payments                                 │   │
│  └─────────────────────────────────────────────┘   │
│         ↓                                          │
│  ┌─────────────────────────────────────────────┐   │
│  │  Object Storage (S3/OSS)                    │   │
│  │  - 词库文件存储                             │   │
│  │  - 模型文件存储                             │   │
│  └─────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

**词库数据模型：**
```csharp
public class CloudVocabulary
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public VocabularyCategory Category { get; set; }
    public string Language { get; set; }
    
    // 词库内容
    public List<VocabularyEntry> Entries { get; set; }
    
    // 元数据
    public string AuthorId { get; set; }
    public string AuthorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // 统计
    public int Downloads { get; set; }
    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    
    // 访问控制
    public bool IsPublic { get; set; }
    public bool IsPremium { get; set; }
    public decimal Price { get; set; }
}

public class VocabularyEntry
{
    public string Word { get; set; }
    public string Pinyin { get; set; }
    public string Definition { get; set; }
    public string Context { get; set; }
    public double Weight { get; set; }
}

public enum VocabularyCategory
{
    General,
    Medical,
    Legal,
    Programming,
    Business,
    Name,
    Custom
}
```

---

## 五、技术选型

### 5.1 客户端技术栈

| 组件 | 技术 | 说明 |
|------|------|------|
| UI 框架 | .NET 8 WPF | 成熟稳定，Windows 原生 |
| 数据库 | SQLite | 轻量级，无需额外服务 |
| ORM | Dapper | 轻量级，性能好 |
| 依赖注入 | Microsoft.Extensions.DependencyInjection | 官方支持 |
| 日志 | Serilog | 结构化日志 |
| 配置 | System.Text.Json | .NET 原生 JSON 支持 |
| 机器码 | System.Management (WMI) | 获取硬件信息 |
| 加壳 | ConfuserEx / VMProtect | 代码保护 |

### 5.2 Python 服务端技术栈

| 组件 | 技术 | 说明 |
|------|------|------|
| 语音识别 | sherpa-onnx | 无需 PyTorch，轻量级 |
| Web 框架 | 内置 http.server | 最小化依赖 |
| 音频处理 | numpy | 音频数据处理 |
| 下载 | requests | HTTP 请求 |
| VAD | silero-vad (可选) | 语音活动检测 |

### 5.3 云平台技术栈（建议）

| 组件 | 技术 | 说明 |
|------|------|------|
| 后端框架 | FastAPI | 快速开发，异步支持 |
| 数据库 | PostgreSQL | 关系型数据 |
| ORM | SQLAlchemy | Python ORM |
| 缓存 | Redis | 会话/缓存 |
| 对象存储 | AWS S3 / 阿里云 OSS | 词库文件存储 |
| 认证 | JWT | 无状态认证 |
| 支付 | 支付宝/微信支付 | 国内支付 |

---

## 六、实施路线图

### 阶段一：基础修复（当前优先级）
- [x] 修复 SQLite 初始化问题
- [x] 修复 Python numpy 版本冲突
- [x] 优化安装包结构
- [x] 确保基本功能正常运行

### 阶段二：架构优化
- [ ] 重构 Python ASR 服务为独立进程
- [ ] 实现 HTTP API 通信
- [ ] 添加服务健康检查
- [ ] 实现 VAD 语音活动检测

### 阶段三：正版认证系统
- [ ] 实现机器码生成
- [ ] 实现许可证验证
- [ ] 集成加壳工具 (ConfuserEx)
- [ ] 实现离线许可证文件

### 阶段四：付费功能
- [ ] 实现 FeatureFlagService
- [ ] 添加功能分级控制
- [ ] 实现使用统计
- [ ] 实现许可证管理界面

### 阶段五：词库平台
- [ ] 设计数据库 schema
- [ ] 实现云平台 API
- [ ] 实现客户端同步
- [ ] 实现词库分享功能

### 阶段六：持续优化
- [ ] 性能优化
- [ ] 用户体验优化
- [ ] 新功能迭代
- [ ] 多平台支持（macOS/Linux）

---

## 七、安全考虑

### 7.1 代码保护
- 使用 ConfuserEx 或 VMProtect 加壳
- 混淆关键代码（LicenseService、UserService）
- 反调试保护
- 移除调试符号

### 7.2 通信安全
- HTTPS 加密通信
- API 请求签名
- 防止重放攻击
- 速率限制

### 7.3 数据安全
- 敏感数据加密存储
- 用户密码哈希存储 (BCrypt)
- 定期安全审计
- 数据备份

### 7.4 许可证安全
- RSA 签名验证
- 机器码绑定
- 定期在线验证（专业版以上）
- 防破解机制

---

## 八、性能优化

### 8.1 启动速度
- 延迟加载非关键服务
- 预编译 XAML
- 使用 ReadyToRun 编译
- 减少启动时服务初始化

### 8.2 内存占用
- 及时释放未使用资源
- 使用流式处理大文件
- 避免内存泄漏
- 模型按需加载

### 8.3 识别延迟
- 使用 WebSocket 实时通信
- 流式识别
- 本地模型优先
- 模型量化（int8）

### 8.4 安装包大小
- 精简 Python 依赖
- 使用 int8 量化模型
- 可选组件按需下载

---

## 九、风险评估

| 风险 | 可能性 | 影响 | 缓解措施 |
|------|--------|------|---------|
| Python 依赖冲突 | 中 | 高 | 锁定版本，虚拟环境 |
| 模型文件过大 | 高 | 中 | 量化、压缩、分卷 |
| 破解盗版 | 高 | 高 | 加壳、在线验证 |
| 云服务成本高 | 中 | 中 | 按需付费、CDN |
| 用户隐私问题 | 中 | 高 | 本地优先、加密存储 |

---

## 十、附录

### 10.1 参考资源
- CapsWriter-Offline: https://github.com/HaujetZhao/CapsWriter-Offline
- sherpa-onnx: https://github.com/k2-fsa/sherpa-onnx
- Typeless: https://typeless.ai
- .NET WPF: https://docs.microsoft.com/dotnet/desktop/wpf/
- ConfuserEx: https://github.com/mkaring/ConfuserEx

### 10.2 版本历史
- v1.0 (2024-02): 初始版本
- v1.1 (计划): 架构优化
- v2.0 (计划): 云平台集成

### 10.3 待讨论事项
1. 是否支持 macOS/Linux？
2. 云服务的部署方案？
3. 定价策略？
4. 营销渠道？

---

*本文档为 WordFlow 项目的长期架构规划，将根据项目发展持续更新。*
*下次更新预计：2026 年 3 月*
