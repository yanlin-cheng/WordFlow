# WordFlow v1.0

WordFlow is an intelligent voice input tool that supports local ASR speech recognition, converting speech to text in real-time and inputting it into any application.

**v1.0 Release** - Built with .NET 8 WPF, providing a stable and intelligent voice input experience.

---

## 📥 Download Now

### Option 1: Installer (Recommended)

| Download | Size | Description |
|----------|------|-------------|
| [⬇️ WordFlow_Setup.exe](https://github.com/yanlin-cheng/WordFlow/releases/download/v1.0.0/WordFlow_Setup.exe) | ~350 MB | **Complete installer** (includes .NET 8 Runtime and Python environment, no additional installation required) |

### Option 2: Manual Model Download (Optional)

If automatic download fails, manually download model files to `PythonASR/models/` directory:

| Download | Size | Description |
|----------|------|-------------|
| [🧠 SenseVoice Model (1/2)](https://github.com/yanlin-cheng/WordFlow/releases/download/v1.0.0/sensevoice-small-onnx.zip.001) | 90 MB | Voice model part 1 |
| [🧠 SenseVoice Model (2/2)](https://github.com/yanlin-cheng/WordFlow/releases/download/v1.0.0/sensevoice-small-onnx.zip.002) | 57 MB | Voice model part 2 |

> **💡 Tip**: We recommend using the installer. Models will be downloaded automatically on first launch.

---

## 🚀 Quick Start

### Installation Steps

1. **Download the installer**: Download `WordFlow_Setup.exe`
2. **Run the installer**: Double-click and follow the prompts
   - The installer will automatically detect and install .NET 8 Runtime if needed
   - You can choose to launch WordFlow immediately after installation
3. **Launch the application**: Start using WordFlow after installation

### Model Installation

**Recommended**: The application will prompt you to download models on first launch

**Alternative**: If automatic download fails:
1. Launch WordFlow
2. Click the "Model Manager" button
3. Click the "Download" button for the desired model
4. The model will be loaded automatically after download completes

### Usage

1. **Launch WordFlow** - The application minimizes to the system tray
2. **Hold to Speak** - Hold the voice input hotkey (default: `CapsLock`) to start recording
3. **Release to Input** - Release the hotkey to recognize and input text to the target application

---

## ✨ Features

### Core Features

| Feature | Description |
|---------|-------------|
| 🎤 **Intelligent Voice Input** | Hold to speak, release to recognize and input |
| 🧠 **Local ASR Recognition** | Uses Sherpa-ONNX framework, works offline |
| 📚 **Personal Vocabulary** | Support custom vocabulary with AI-powered correction |
| 📝 **History Log** | Automatically saves input history for easy reference |
| 🔔 **Tray Integration** | Minimizes to system tray, always accessible |
| ⚡ **Auto-start** | Supports automatic startup on boot |
| ⌨️ **Global Hotkeys** | Customizable hotkeys |
| 🔄 **Auto Model Load** | Automatically loads model after download |

### Hotkey Settings

WordFlow supports various hotkey configurations to meet different usage habits:

| Hotkey Type | Default | Description |
|-------------|---------|-------------|
| Voice Input Hotkey | CapsLock | Hold to speak, release to recognize |

> **💡 Tip**: We recommend using CapsLock as the voice input hotkey because:
> - Centrally located, easy to operate with either hand
> - Large key surface, less prone to accidental touches
> - Clear physical feedback when pressed

### Model Manager

WordFlow provides model management features:
- **One-click Download**: Click "Download" to download models directly
- **Auto Load**: Automatically attempts to load model after download
- **Service Detection**: Prompts to start ASR service if not running
- **Status Display**: Real-time model status (Installed/Not Downloaded/Loaded)

---

## 💻 System Requirements

| Requirement | Specification |
|-------------|---------------|
| Operating System | Windows 10/11 64-bit |
| Runtime Environment | .NET 8.0 Runtime (included in installer) |
| Disk Space | At least 500MB free space (including model files) |
| Network | Internet connection required for initial model download |
| Microphone | Available audio input device required |
| Memory | 4GB RAM recommended |

---

## 🏗️ Technical Architecture

### Technology Stack

| Component | Technology | Description |
|-----------|------------|-------------|
| UI Framework | .NET 8 WPF | Mature and stable, native Windows support |
| Database | SQLite | Lightweight, no additional services required |
| ORM | Dapper | Lightweight ORM with good performance |
| Speech Recognition | Sherpa-ONNX | Next-Gen K2 speech recognition toolkit |
| Audio Processing | NAudio | Mature .NET audio library |
| Segmentation Engine | Jieba.NET | Chinese word segmentation |
| Pinyin Conversion | TinyPinyin | Chinese character to Pinyin conversion |
| Logging | Built-in Logger | Structured logging |

### Directory Structure

```
WordFlow/
├── WordFlow.exe              # Main application
├── Data/
│   └── models.json           # Model configuration
├── PythonASR/
│   ├── asr_server.py         # ASR server
│   ├── start_server.bat      # Startup script
│   └── models/
│       └── sensevoice-small-onnx/  # Voice model (downloaded on first launch)
│           ├── model.int8.onnx
│           └── tokens.txt
├── Resources/
│   └── icon.ico              # Application icon
└── Logs/                     # Log files
```

### Service Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    WordFlow Client                       │
│  ┌─────────────────────────────────────────────────┐   │
│  │              .NET WPF UI Layer                   │   │
│  │  - Main Window (MainWindow)                     │   │
│  │  - Settings Window (SettingsWindow)             │   │
│  │  - Vocabulary Manager (VocabularyManagerWindow) │   │
│  │  - Model Manager (ModelManagerWindow)           │   │
│  │  - First Run Wizard (FirstRunWizard)            │   │
│  └─────────────────────────────────────────────────┘   │
│                          ↓                               │
│  ┌─────────────────────────────────────────────────┐   │
│  │              .NET Service Layer                  │   │
│  │  - SettingsService (Settings management)        │   │
│  │  - HistoryService (History log/SQLite)          │   │
│  │  - VocabularyService (Vocabulary management)    │   │
│  │  - VocabularyLearningEngine (Vocabulary learning)│  │
│  │  - GlobalHotkeyService (Global hotkeys)         │   │
│  │  - TrayService (System tray)                    │   │
│  │  - ModelDownloadService (Model download)        │   │
│  │  - SpeechRecognitionService (ASR client)        │   │
│  └─────────────────────────────────────────────────┘   │
│                          ↓ HTTP                        │
│  ┌─────────────────────────────────────────────────┐   │
│  │          Python ASR Service (Separate Process)   │   │
│  │  - Sherpa-ONNX Speech Recognition               │   │
│  │  - Model loading/switching                      │   │
│  │  - Audio preprocessing                          │   │
│  └─────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

---

## ❓ FAQ

### Q1: What do I need to download on first launch?

On first launch, the application will automatically download voice model files (~156MB). Please ensure you have a stable internet connection. Once downloaded, you can use the application normally without downloading again.

### Q2: What should I do if model download fails?

If automatic download fails:
1. Check if your network connection is working
2. Try restarting the application
3. Manually download model files (see "Option 2" above)
4. Click the "Refresh" button in the Model Manager page

### Q3: Why can't I use it after downloading the model?

WordFlow has optimized this issue:
- Automatically attempts to load the model after download
- Prompts to start ASR service if not running
- If still not working, restart the application or manually click the "Connect Service" button

### Q4: What should I do if recognition is inaccurate?

1. Check if the microphone is working properly
2. Use in a quiet environment
3. Speak clearly at a moderate pace
4. Add frequently used vocabulary to your personal dictionary

### Q5: Why can't I input text in some applications?

Some applications may restrict external input. Try:
1. Run WordFlow as administrator
2. Click the text box in the target application to gain focus first
3. Check if any security software is blocking

### Q6: How do I change the hotkey?

1. Right-click the system tray icon
2. Select "Settings"
3. Modify in the "Hotkey Settings" section

### Q7: Why doesn't it auto-start on boot?

1. Check if "Auto-start on boot" option is enabled
2. Check if security software is blocking auto-start
3. Confirm in the "Startup" tab of Task Manager

---

## 📋 Changelog

### v1.0.0 (2026-03-15)

**Initial Release**
- 🎉 Implemented basic voice input functionality
- 🎉 Implemented personal vocabulary management
- 🎉 Implemented history log feature
- 🎉 Implemented system tray integration
- 🎉 Implemented auto-start on boot
- 🎉 Implemented global hotkeys

**Technical Features**
- 🏗️ Built with .NET 8 WPF
- 🏗️ Modular service architecture
- 🏗️ Built-in Python environment and ASR service
- 🏗️ Support for multiple model download and management

**Deployment Optimization**
- 🚀 Self-contained deployment, no .NET Runtime installation required
- 🚀 Installer size ~350MB, includes all dependencies
- 🚀 Model download moved to first launch, supports resume on break
- 🚀 Simplified installation process, improved success rate

**Known Issues**
- Some applications may not fully support voice input screen feature

---

## 🔗 Project Links

| Platform | Link |
|----------|------|
| 📂 **GitHub Repository** | https://github.com/yanlin-cheng/WordFlow |
| 🐛 **Issue Tracker** | https://github.com/yanlin-cheng/WordFlow/issues |

### Multi-language Versions

- [🇨🇳 中文](README.md)
- [🇬🇧 English](README.en.md)
- [🇯🇵 日本語](README.ja.md)
- [🇰🇷 한국어](README.ko.md)

---

## 📄 License

This project uses a **MIT + Proprietary** dual-mode license:

- **Basic Features** (MIT License): Core voice input, offline models, local vocabulary management, etc.
- **Premium Features** (Proprietary License): User login, cloud sync, vocabulary marketplace, AI enhancement services, etc.

See [LICENSE.txt](LICENSE.txt) for details.

---

## 🗺️ Roadmap

WordFlow's future development plan includes:

- **Phase 1** (March 2026): Foundation improvement, open source release
- **Phase 2** (April-May 2026): User system, payment integration
- **Phase 3** (June-July 2026): Vocabulary marketplace launch
- **Phase 4** (August-September 2026): AI enhancement services
- **Phase 5** (October 2026+): Continuous optimization, multi-platform support

See [Roadmap Document](产品路线/WordFlow 产品路线与商业化规划.md) for details.

---

**WordFlow v1.0** - Making voice input smarter and easier
