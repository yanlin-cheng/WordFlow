# WordFlow v2.0

**WordFlow** is a smart voice input tool that supports local ASR speech recognition, converting speech to text in real-time and inputting it into any application.

**v2.0 Complete Rebuild** - Rebuilt with .NET 8 WPF for a more stable and intelligent voice input experience.

---

## 📥 Download

### Option 1: Direct Download (Recommended)

| Download | Size | Description |
|----------|------|-------------|
| [⬇️ WordFlow_Setup.exe](https://gitee.com/yanlin-cheng/WordFlow/releases/download/v1.0.0/WordFlow_Setup.exe) | ~60 MB | **Installer** (Auto-detects and installs .NET 8 runtime) |

### Option 2: Manual Model Download (Optional)

If automatic download fails, manually download model files to `PythonASR/models/` directory:

| Download | Size | Description |
|----------|------|-------------|
| [🧠 Model File 1/3](https://gitee.com/yanlin-cheng/WordFlow/releases/download/v1.0.0/paraformer-zh.tar.bz2.part1) | 90 MB | Voice model part 1 |
| [🧠 Model File 2/3](https://gitee.com/yanlin-cheng/WordFlow/releases/download/v1.0.0/paraformer-zh.tar.bz2.part2) | 90 MB | Voice model part 2 |
| [🧠 Model File 3/3](https://gitee.com/yanlin-cheng/WordFlow/releases/download/v1.0.0/paraformer-zh.tar.bz2.part3) | 26 MB | Voice model part 3 |

> **💡 Tip**: We recommend using the installer, which automatically downloads models on first launch.

---

## 🚀 Quick Start

### Installation Steps

1. **Download the installer**: Download `WordFlow_Setup.exe`
2. **Run the installer**: Double-click and follow the prompts
   - Automatically detects and installs .NET 8 runtime
   - Option to download and install speech recognition models (recommended)
3. **Launch the app**: Start using after installation

### Model Installation

**Recommended**: During installation, check the model download option - the installer will automatically download and install models.

**Alternative**: If models weren't installed during setup:
1. Launch WordFlow
2. Click the "Model Manager" button
3. Click the "Download" button for the desired model
4. The model will automatically attempt to load after download

### How to Use

1. **Launch WordFlow** - The app minimizes to the system tray
2. **Press and Hold to Speak** - Hold the hotkey (default: CapsLock) to start recording
3. **Release to Input** - Release the hotkey to automatically recognize and input text

---

## ✨ Features

### Core Features

| Feature | Description |
|---------|-------------|
| 🎤 **Smart Voice Input** | Press and hold to speak, release to recognize and input |
| 🧠 **Local ASR Recognition** | Uses Alibaba DAMO Academy Paraformer model, works offline |
| 📚 **Personal Vocabulary** | Support custom vocabulary, AI smart correction |
| 📝 **History** | Automatically saves input history for reference |
| 🔔 **Tray Integration** | Minimizes to system tray, always accessible |
| ⚡ **Auto Start** | Supports automatic startup on boot |
| ⌨️ **Global Hotkeys** | Customizable hotkeys |
| 🔄 **Auto Model Load** | Automatically attempts to load model after download |

### Hotkey Settings

WordFlow supports multiple hotkey configurations:

| Hotkey Type | Default | Description |
|-------------|---------|-------------|
| Voice Input Hotkey | CapsLock | Press and hold to speak, release to recognize |
| Custom Hotkey | Right Alt | Configurable in settings |

> **💡 Tip**: We recommend using CapsLock as the voice input hotkey because:
> - Centered position, easy for both hands
> - Large key surface, less prone to accidental touches
> - Clear physical feedback when pressed

### Model Management

v2.0 adds model management features:
- **One-click Download**: Click "Download" button directly, no need to select repeatedly
- **Auto Load**: Automatically attempts to load model after download
- **Service Detection**: Prompts user if ASR service is not running
- **Status Display**: Real-time model status (Installed / Not Downloaded / Loaded)

---

## 💻 System Requirements

| Requirement | Specification |
|-------------|---------------|
| Operating System | Windows 10/11 64-bit |
| Runtime Environment | .NET 8.0 Runtime (automatically installed by installer) |
| Disk Space | At least 300MB free space (including model files) |
| Network | Internet required for initial model download |
| Microphone | Available audio input device |
| Memory | 4GB or more recommended |

---

## 🏗️ Technical Architecture

### Tech Stack

| Component | Technology | Description |
|-----------|------------|-------------|
| UI Framework | .NET 8 WPF | Mature and stable, native Windows support |
| Database | SQLite | Lightweight, no additional services needed |
| ORM | Dapper | Lightweight, good performance |
| Speech Recognition | Paraformer (ONNX Runtime) | Alibaba DAMO Academy open-source model |
| Audio Processing | NAudio | Mature .NET audio library |
| Word Segmentation | Jieba.NET | Chinese word segmentation |
| Pinyin Conversion | TinyPinyin | Chinese to Pinyin |
| Logging | Serilog | Structured logging |

### Directory Structure

```
WordFlow/
├── WordFlow.exe              # .NET main executable
├── appsettings.json          # Application config
├── Data/
│   └── models.json           # Model configuration
├── PythonASR/
│   ├── asr_server.py         # ASR server
│   ├── download_model.py     # Model download script
│   ├── start_server.bat      # Startup script
│   └── models/
│       └── paraformer-zh/    # Voice model (downloaded on first launch)
│           ├── model.int8.onnx
│           └── tokens.txt
├── Resources/
│   └── icon.ico              # Application icon
└── Logs/                     # Log files
```

### Service Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    WordFlow Client                      │
│  ┌─────────────────────────────────────────────────┐   │
│  │              .NET WPF UI Layer                   │   │
│  │  - Main Window (MainWindow)                      │   │
│  │  - Settings Window (SettingsWindow)              │   │
│  │  - Vocabulary Manager (VocabularyManagerWindow)  │   │
│  │  - Model Manager (ModelManagerWindow)            │   │
│  │  - First Run Wizard (FirstRunWizard)             │   │
│  └─────────────────────────────────────────────────┘   │
│                          ↓                               │
│  ┌─────────────────────────────────────────────────┐   │
│  │              .NET Service Layer                  │   │
│  │  - SettingsService                               │   │
│  │  - HistoryService (History/SQLite)               │   │
│  │  - VocabularyService                             │   │
│  │  - VocabularyLearningEngine                      │   │
│  │  - GlobalHotkeyService                           │   │
│  │  - TrayService                                   │   │
│  │  - ModelDownloadService                          │   │
│  │  - SpeechRecognitionService                      │   │
│  └─────────────────────────────────────────────────┘   │
│                          ↓ HTTP                        │
│  ┌─────────────────────────────────────────────────┐   │
│  │          Python ASR Service (Separate Process)   │   │
│  │  - Sherpa-ONNX Speech Recognition               │   │
│  │  - Model Loading/Switching                      │   │
│  │  - Audio Preprocessing                          │   │
│  └─────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

---

## ❓ FAQ

### Q1: What downloads on first launch?

On first launch, it automatically downloads the speech model file (~206MB). Keep your network connected. After download, you can use it normally without downloading again.

### Q2: What if model download fails?

If automatic download fails:
1. Check if your network connection is working
2. Try restarting the application
3. Manually download model files (see "Option 2" above)
4. Click the "Refresh" button in the model manager page

### Q3: Why can't I use it after downloading the model?

v2.0 has optimized this issue:
- Automatically attempts to load model after download
- Prompts user if ASR service is not running
- If still not working, restart the app or manually click the "Connect Service" button

### Q4: What if recognition is inaccurate?

1. Check if your microphone is working properly
2. Use in a quiet environment
3. Speak clearly at a moderate pace
4. Add frequently used vocabulary to your personal dictionary

### Q5: Why can't I input text in some applications?

Some applications may restrict external input. Try:
1. Run WordFlow as administrator
2. Click the text box in the target application first to get focus
3. Check if any security software is blocking

### Q6: How do I change the hotkey?

1. Right-click the system tray icon
2. Select "Settings"
3. Change in "Hotkey Settings"

### Q7: Why doesn't it auto-start on boot?

1. Check if "Auto-start on boot" is checked
2. Check if security software is blocking auto-start
3. Confirm in Task Manager's "Startup" tab

---

## 📋 Changelog

### v2.0.0 (2026-02-28)

**Architecture Rebuild**
- 🏗️ Complete rebuild with .NET 8 WPF
- 🏗️ Modular service architecture
- 🏗️ Optimized memory usage and startup speed

**New Features**
- ✨ Model manager window for visual model management
- ✨ One-click model download, no need to select repeatedly
- ✨ Auto-load model after download
- ✨ Prompt user if ASR service is not running
- ✨ Real-time model status display (Installed/Not Downloaded/Loaded)
- ✨ Download progress bar with percentage

**Optimizations**
- 🔧 Optimized model download process
- 🔧 Optimized model loading logic
- 🔧 Improved UI response speed
- 🔧 Better error detection and logging

**Deployment Optimizations**
- 🚀 Optimized installer structure
- 🚀 Support for resumable downloads
- 🚀 Separate model configuration files

**Known Issues**
- Some applications may not be fully compatible with voice input screen function

### v1.0.0 (2025-02-27)

**New Features**
- ✨ Initial release
- ✨ Basic voice input functionality
- ✨ Personal vocabulary management
- ✨ History functionality
- ✨ System tray integration
- ✨ Auto-start on boot
- ✨ Global hotkeys

**Deployment Optimizations**
- 🚀 Self-contained release, no need to install .NET Runtime
- 🚀 Single file release, only 74MB, double-click to use
- 🚅 Model download moved to first launch, supports resumable downloads
- 🚀 Simplified installation process, improved installation success rate

---

## 🔗 Project Links

| Platform | Link |
|----------|------|
| 📂 **GitHub Source Repository** | https://github.com/yanlin-cheng/WordFlow |
| 📦 **Gitee Release Versions** | https://gitee.com/yanlin-cheng/WordFlow/releases |
| 🐛 **Issue Tracker (GitHub)** | https://github.com/yanlin-cheng/WordFlow/issues |
| 🐛 **Issue Tracker (Gitee)** | https://gitee.com/yanlin-cheng/WordFlow/issues |

---

## 📄 License

This project uses a **MIT + Proprietary** dual-mode license:

- **Basic Features** (MIT License): Core voice input, offline models, local vocabulary management, etc.
- **Premium Features** (Proprietary License): User login, cloud sync, vocabulary marketplace, AI enhancement services, etc.

See [LICENSE.txt](LICENSE.txt) for details.

---

## 🗺️ Roadmap

WordFlow's future development plan includes:

- **Phase 1** (March 2026): Basic improvements, open-source release
- **Phase 2** (April-May 2026): User system, payment integration
- **Phase 3** (June-July 2026): Vocabulary marketplace launch
- **Phase 4** (August-September 2026): AI enhancement services
- **Phase 5** (October 2026+): Continuous optimization, multi-platform support

See [Roadmap Document](产品路线/WordFlow 产品路线与商业化规划.md) for details.

---

**WordFlow v2.0** - Complete rebuild, making voice input smarter and easier
