## 🌍 Multi-language Versions

| Language | Version |
|----------|---------|
| 🇨 [简体中文](README.zh-CN.md) | 简体中文 |
| 🇬 [English](README.md) | English |
| 🇯🇵 [日本語](README.ja.md) | 日本語 |
| 🇰🇷 [한국어](README.ko.md) | 한국어 |

# WordFlow v1.0

WordFlow is an intelligent voice input tool that supports local ASR speech recognition, converting speech to text in real-time and inputting it into any application.

**v1.0 Release** - Built with .NET 8 WPF, providing a stable and intelligent voice input experience.

---

## 🌏 For Users in China

> **💡 Download Tip**: If you experience slow download speeds from GitHub, you can visit our [Gitee mirror site](https://gitee.com/yanlin-cheng/wordflow) for faster downloads. Software and model files are synchronously released on Gitee.

---

## 📥 Download Now

### Installer

| Download | Size | Description |
|----------|------|-------------|
| [⬇️ WordFlow_Setup.exe](https://github.com/yanlin-cheng/WordFlow/releases/download/v1.0.0/WordFlow_Setup.exe) | ~95 MB | **Complete installer** (includes .NET 8 Runtime and Python environment, no additional installation required) |

> **💡 Tip**: After installation, the application will guide you to download the voice recognition model (~150 MB) on first launch. You can also download it anytime from the "Model Manager" page.
>
> The download may be flagged as coming from an unknown source. Please click the download button and select "Keep." I will be implementing official digital certification for the software in the future.

---

## 🚀 Quick Start

### Installation Steps

1. **Download the installer**: Download `WordFlow_Setup.exe`
2. **Run the installer**: Double-click and follow the prompts
3. **Launch the application**: Start WordFlow after installation
4. **Download Model**: The application will guide you to download the model on first launch, or click "Model Manager" → "Download"

### Usage

1. **Launch WordFlow** - The application minimizes to the system tray
2. **Hold to Speak** - Hold the voice input hotkey to start recording
3. **Release to Input** - Release the hotkey to recognize and input text to the target application

---

## ✨ Features

### Core Features

| Feature | Description |
|---------|-------------|
| 🎤 **Intelligent Voice Input** | Hold to speak, release to recognize and input |
| 🧠 **Local ASR Recognition** | Uses Sherpa-ONNX framework, works offline |
| 🌐 **Multi-language Recognition** | Supports Chinese, English, Japanese, Korean, Cantonese |
| 📚 **Personal Vocabulary** | Support custom vocabulary with AI-powered correction |
| 📝 **History Log** | Automatically saves input history for easy reference |
| 🔔 **Tray Integration** | Minimizes to system tray, always accessible |
| ⚡ **Auto-start** | Supports automatic startup on boot |
| ⌨️ **Global Hotkeys** | Customizable hotkeys |

### Supported Languages

WordFlow's speech recognition model supports the following languages:

| Language | Description |
|----------|-------------|
| Chinese (Mandarin) | Simplified Chinese speech recognition |
| English | English speech recognition |
| Japanese | Japanese speech recognition |
| Korean | Korean speech recognition |
| Cantonese | Cantonese speech recognition |

> **Note**: The software UI currently supports Chinese and English only.

### Hotkey Settings

WordFlow supports customizable hotkeys to meet different usage habits:

| Hotkey Type | Description |
|-------------|-------------|
| Voice Input Hotkey | Customizable in settings, default is ` key (grave accent) |

> **💡 Tip**: We recommend using a key that's easy to operate with one hand as the voice input hotkey.

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
| Network | Internet connection required for model download |
| Microphone | Available audio input device required |
| Memory | 4GB RAM recommended |

---

## 🏗️ Technical Architecture

### Technology Stack

| Component | Technology |
|-----------|------------|
| UI Framework | .NET 8 WPF |
| Database | SQLite |
| Speech Recognition | Sherpa-ONNX |
| Audio Processing | NAudio |
| Chinese Segmentation | Jieba.NET |

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
│       └── sensevoice-small-onnx/  # Voice model
└── Resources/
    └── icon.ico              # Application icon
```

---

## ❓ FAQ

### Q1: What do I need to do on first launch?

On first launch, the application will guide you to download the voice recognition model file (~150 MB). Just follow the prompts.

### Q2: What should I do if model download fails?

If download within the application fails:
1. Check if your network connection is working
2. Users in China can try downloading from [Gitee mirror](https://gitee.com/yanlin-cheng/wordflow)

### Q3: Why can't I use it after downloading the model?

- The application automatically attempts to load the model after download
- If ASR service is not running, you'll be prompted to start it
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
- 🎉 Implemented model management

**Technical Features**
- 🏗️ Built with .NET 8 WPF
- 🏗️ Modular service architecture
- 🏗️ Built-in Python environment and ASR service

**Known Issues**
- Some applications may not fully support voice input screen feature

---

## 🔗 Project Links

| Platform | Link |
|----------|------|
| 📂 **GitHub Repository** | https://github.com/yanlin-cheng/WordFlow |
| 🐛 **Issue Tracker** | https://github.com/yanlin-cheng/WordFlow/issues |
| ⬇️ **Downloads** | https://github.com/yanlin-cheng/WordFlow/releases |
| 🇨🇳 **Gitee Mirror** | https://gitee.com/yanlin-cheng/wordflow |

---

## 📄 License

This project uses a **MIT + Proprietary** dual-mode license:

- **Basic Features** (MIT License): Core voice input, offline models, local vocabulary management, etc.
- **Premium Features** (Proprietary License): Cloud sync, AI enhancement services, etc.

See [LICENSE.txt](LICENSE.txt) for details.

---

**WordFlow v1.0** - Making voice input smarter and easier
