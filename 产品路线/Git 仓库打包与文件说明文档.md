# WordFlow 项目 Git 仓库打包与文件说明文档

**文档版本：** v1.1  
**创建日期：** 2026 年 3 月 1 日  
**更新日期：** 2026 年 3 月 2 日  
**适用仓库：** https://github.com/wanddream/WordFlowV2.git

---

## 一、必须打包的文件目录

### 1.1 核心源代码目录

| 目录/文件 | 说明 | 必须性 |
|-----------|------|--------|
| `WordFlow.slnx` | Visual Studio 解决方案文件 | ⭐⭐⭐ 必须 |
| `WordFlow.csproj` | 项目配置文件 | ⭐⭐⭐ 必须 |
| `App.xaml` / `App.xaml.cs` | 应用程序入口 | ⭐⭐⭐ 必须 |
| `MainWindow.xaml` / `MainWindow.xaml.cs` | 主窗口 | ⭐⭐⭐ 必须 |
| `Services/` | 所有服务层代码 | ⭐⭐⭐ 必须 |
| `Views/` | 所有 UI 窗口代码 | ⭐⭐⭐ 必须 |
| `Models/` | 数据模型 | ⭐⭐⭐ 必须 |
| `Infrastructure/` | 基础设施代码（EventBus 等） | ⭐⭐⭐ 必须 |
| `Utils/` | 工具类 | ⭐⭐⭐ 必须 |
| `Resources/` | 应用资源（图标等） | ⭐⭐⭐ 必须 |
| `Data/models.json` | 模型配置信息 | ⭐⭐⭐ 必须 |

### 1.2 Python ASR 服务目录

| 目录/文件 | 说明 | 必须性 |
|-----------|------|--------|
| `PythonASR/asr_server.py` | ASR 服务端核心代码 | ⭐⭐⭐ 必须 |
| `PythonASR/requirements.txt` | Python 依赖列表 | ⭐⭐⭐ 必须 |
| `PythonASR/download_model.py` | 模型下载脚本 | ⭐⭐ 重要 |
| `PythonASR/使用说明.md` | Python 服务端使用说明 | ⭐⭐ 重要 |
| `PythonASR/start_server.bat` | 服务启动脚本 | ⭐⭐ 重要 |

### 1.3 安装与部署文件

| 目录/文件 | 说明 | 必须性 |
|-----------|------|--------|
| `Installer/WordFlowSetup.iss` | Inno Setup 安装包脚本 | ⭐⭐⭐ 必须 |
| `build_installer.bat` | 安装包构建脚本 | ⭐⭐ 重要 |
| `README.md` | 项目说明文档 | ⭐⭐⭐ 必须 |
| `LICENSE.txt` | 开源许可证 | ⭐⭐ 重要 |

### 1.4 配置文件

| 目录/文件 | 说明 | 必须性 |
|-----------|------|--------|
| `.gitignore` | Git 忽略规则 | ⭐⭐⭐ 必须 |
| `appsettings.json` | 应用配置（如有） | ⭐⭐ 重要 |
| `TrimmerRootDescriptor.xml` | .NET 裁剪配置 | ⭐⭐ 重要 |

---

## 二、重点文件及其作用

### 2.1 应用程序入口

| 文件 | 作用 |
|------|------|
| `App.xaml` | WPF 应用程序定义，包含全局资源和启动/退出事件 |
| `App.xaml.cs` | 应用程序生命周期管理，服务初始化，托盘图标管理 |

### 2.2 主界面

| 文件 | 作用 |
|------|------|
| `MainWindow.xaml` | 主界面 UI 定义 |
| `MainWindow.xaml.cs` | 主界面逻辑，录音控制，事件订阅，窗口管理 |

### 2.3 核心服务层

| 文件 | 作用 |
|------|------|
| `Services/SpeechRecognitionService.cs` | 语音识别核心服务，管理录音和识别流程 |
| `Services/GlobalHotkeyServiceV2.cs` | 全局热键监听，支持 ` 键和右 Alt 键 |
| `Services/SettingsService.cs` | 用户设置管理（热键、自启动等） |
| `Services/HistoryService.cs` | 历史记录管理，SQLite 数据库操作 |
| `Services/VocabularyLearningEngine.cs` | 词库学习引擎，从用户修正中学习 |
| `Services/AIVocabularyService.cs` | AI 词库服务，专业词汇管理 |
| `Services/ModelDownloadService.cs` | 模型下载服务 |
| `Services/TrayServiceV2.cs` | 系统托盘服务 |
| `Services/FirstRunService.cs` | 首次运行向导服务 |
| `Services/AutoStartService.cs` | 开机自启动服务 |

### 2.4 UI 窗口

| 文件 | 作用 |
|------|------|
| `Views/SettingsWindow.xaml(.cs)` | 设置窗口 |
| `Views/ModelManagerWindow.xaml(.cs)` | 模型管理窗口 |
| `Views/VocabularyManagerWindow.xaml(.cs)` | 词库管理窗口 |
| `Views/FirstRunWizard.xaml(.cs)` | 首次运行向导 |
| `Views/RecordingIndicatorWindow.xaml(.cs)` | 录音指示器悬浮窗 |
| `Views/TranscriptPopupWindow.xaml(.cs)` | 识别结果弹窗 |
| `Views/VocabularyPage.xaml(.cs)` | 词库管理页面 |

### 2.5 数据模型

| 文件 | 作用 |
|------|------|
| `Models/InputHistory.cs` | 输入历史记录模型 |
| `Models/PersonalVocabulary.cs` | 个人词库模型 |
| `Models/CorrectionLog.cs` | 修正日志模型 |

### 2.6 Python 服务端

| 文件 | 作用 |
|------|------|
| `PythonASR/asr_server.py` | HTTP 服务器，处理语音识别请求 |
| `PythonASR/download_model.py` | 从 HuggingFace 下载语音模型 |
| `PythonASR/requirements.txt` | Python 依赖：sherpa-onnx, numpy, requests, websockets |

### 2.7 安装部署

| 文件 | 作用 |
|------|------|
| `Installer/WordFlowSetup.iss` | Inno Setup 脚本，定义安装包行为 |
| `build_installer.bat` | 一键构建安装包脚本 |

---

## 三、无需上传的文件（.gitignore）

### 3.1 编译输出目录

| 目录 | 说明 |
|------|------|
| `bin/` | 编译输出目录 |
| `obj/` | 编译中间文件 |
| `publish/` | 发布输出目录 |
| `publish_*/` | 各种测试发布目录 |
| `Output/` | 安装包输出目录 |

### 3.2 用户数据与运行时文件

| 目录/文件 | 说明 |
|-----------|------|
| `Data/WordFlow.db` | SQLite 数据库（用户数据） |
| `Data/*.db` | 所有数据库文件 |
| `Logs/` | 日志文件 |
| `*.log` | 日志文件 |
| `debug_log.txt` | 调试日志 |

### 3.3 大型资源文件

| 目录/文件 | 说明 |
|-----------|------|
| `PythonASR/models/` | 语音模型文件（每个约 200MB+） |
| `PythonASR/python/` | 嵌入的 Python 环境（约 50MB） |
| `*.onnx` | ONNX 模型文件 |
| `*.bin` | 二进制模型文件 |

### 3.4 IDE 与编辑器文件

| 目录/文件 | 说明 |
|-----------|------|
| `.vs/` | Visual Studio 工作区文件 |
| `.vscode/` | VS Code 配置（个人配置） |
| `*.user` | 用户特定设置 |
| `*.suo` | 解决方案用户选项 |

### 3.5 临时文件

| 文件类型 | 说明 |
|----------|------|
| `*.tmp` | 临时文件 |
| `Thumbs.db` | Windows 缩略图缓存 |
| `desktop.ini` | Windows 文件夹配置 |

---

## 四、项目记录与总结

### 4.1 项目概况

**WordFlow** 是一款面向专业用户的离线语音输入工具，提供高精度、低延迟的语音转文字功能。

**技术栈：**
- 前端：.NET 8 WPF
- 后端：Python + sherpa-onnx
- 数据库：SQLite
- 安装包：Inno Setup

### 4.2 已完成功能

| 功能模块 | 状态 | 说明 |
|----------|------|------|
| 语音识别 | ✅ 完成 | 支持按住 ` 键或右 Alt 键录音 |
| 热键管理 | ✅ 完成 | 支持自定义热键 |
| 历史记录 | ✅ 完成 | SQLite 存储，支持搜索筛选 |
| 个人词库 | ✅ 完成 | 支持导入导出 |
| 模型管理 | ✅ 完成 | 支持下载/切换/删除模型 |
| 系统托盘 | ✅ 完成 | 最小化到托盘，双击恢复 |
| 开机自启 | ✅ 完成 | 可选开机自启动 |
| 首次运行向导 | ✅ 完成 | 引导用户完成初始设置 |

### 4.3 架构设计

```
┌─────────────────────────────────────────────────────────┐
│                    WordFlow 客户端                       │
│  ┌─────────────────────────────────────────────────┐   │
│  │              .NET WPF 界面层                     │   │
│  └─────────────────────────────────────────────────┘   │
│                          ↓                               │
│  ┌─────────────────────────────────────────────────┐   │
│  │              .NET 服务层                         │   │
│  │  - SettingsService / HistoryService             │   │
│  │  - VocabularyService / SpeechRecognitionService │   │
│  │  - GlobalHotkeyService / TrayService            │   │
│  └─────────────────────────────────────────────────┘   │
│                          ↓ HTTP                        │
│  ┌─────────────────────────────────────────────────┐   │
│  │          Python ASR 服务 (独立进程)              │   │
│  │  - Sherpa-ONNX 语音识别                         │   │
│  └─────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

### 4.4 已知问题与待优化项

| 问题 | 优先级 | 说明 |
|------|--------|------|
| numpy 版本冲突 | 高 | Python 嵌入环境的 numpy 需要预安装 |
| 模型文件过大 | 中 | 单个模型约 200MB，需在线下载 |
| 首次启动配置 | 中 | 需要引导用户下载模型 |

### 4.5 重构文档目录

| 文件 | 说明 |
|------|------|
| `产品路线/WordFlow 架构规划文档.md` | v2.0 架构规划 |
| `产品路线/Git 仓库打包与文件说明文档.md` | 本文档 |

### 4.6 打包发布流程

```bash
# 1. 清理编译输出
dotnet clean

# 2. 发布应用
dotnet publish -c Release -r win-x64 --self-contained false

# 3. 准备 Python 环境
cd PythonASR
.\prepare_embedded_python.ps1

# 4. 构建安装包
.\build_installer.bat
```

### 4.7 Git 初始化命令

```bash
# 初始化仓库
git init

# 添加所有文件
git add .

# 首次提交
git commit -m "Initial commit: WordFlow V2.0"

# 添加远程仓库
git remote add origin https://github.com/wanddream/WordFlowV2.git

# 推送到 GitHub
git branch -M main
git push -u origin main
```

---

## 五、快速检查清单

### 上传前检查

- [ ] 确认 `bin/` 和 `obj/` 目录已被 .gitignore 忽略
- [ ] 确认 `PythonASR/models/` 目录已被忽略
- [ ] 确认 `PythonASR/python/` 目录已被忽略
- [ ] 确认数据库文件未被包含
- [ ] 确认日志文件未被包含

### 必传文件检查

- [ ] `WordFlow.slnx`
- [ ] `WordFlow.csproj`
- [ ] `App.xaml` / `App.xaml.cs`
- [ ] `MainWindow.xaml` / `MainWindow.xaml.cs`
- [ ] `Services/` 目录
- [ ] `Views/` 目录
- [ ] `Models/` 目录
- [ ] `PythonASR/asr_server.py`
- [ ] `PythonASR/requirements.txt`
- [ ] `Installer/WordFlowSetup.iss`
- [ ] `README.md`
- [ ] `.gitignore`

---

*本文档将持续更新，以反映项目的最新状态。*
