# pingyi - Offline Screenshot Translator & OCR Tool

[![Download from GitHub](https://img.shields.io/badge/Download%20pingyi-%23FF6B35?style=for-the-badge&logo=github&logoColor=white)](https://github.com/atak4neren/pingyi/releases)

## What is pingyi?

pingyi is a free, open-source application that lets you translate text from screenshots and extract text from images - all without needing an internet connection. You can take a screenshot of any area on your screen, and pingyi will recognize the text inside it, then translate it to another language. It runs entirely on your computer, keeping your data private.

## Key Features

- **📸 Screenshot Translation** - Capture any area of your screen and instantly translate the text
- **🔍 OCR Text Extraction** - Extract text from images, screenshots, or photos
- **🤖 Multiple OCR Engines** - Supports PaddleOCR for high-accuracy text recognition
- **🌐 Local Translation** - Translate text using your own computer with llama.cpp or Ollama
- **💻 Cross-Platform** - Works on Windows and Ubuntu Linux (X11)
- **🔒 Offline-First** - No internet required for basic functionality
- **⚡ Fast & Lightweight** - Built with Avalonia and C# for smooth performance

## 🚀 Getting Started

### System Requirements

**Windows:**
- Windows 10 or later (64-bit)
- 4GB RAM minimum (8GB recommended)
- 500MB free disk space
- .NET 8.0 Runtime (installed automatically if needed)

**Ubuntu Linux (X11):**
- Ubuntu 20.04 or later
- X11 display server
- 4GB RAM minimum (8GB recommended)
- 500MB free disk space
- .NET 8.0 Runtime

### 📥 Download & Install

Visit this link to download the application.

[![Download pingyi](https://img.shields.io/badge/⬇️%20Download%20pingyi-%23007ACC?style=for-the-badge&logo=github&logoColor=white)](https://github.com/atak4neren/pingyi/releases)

**Windows Installation Steps:**
1. Click the download button above to go to the releases page
2. Find the latest version (look for the newest release)
3. Download the file named `pingyi-windows-x64.zip` or similar
4. Extract the ZIP file to a folder on your computer
5. Run `pingyi.exe` from the extracted folder

**Ubuntu Linux Installation Steps:**
1. Visit the releases page
2. Download the file named `pingyi-linux-x64.tar.gz` or similar
3. Extract the archive to a folder
4. Make the executable runnable: `chmod +x pingyi`
5. Run `./pingyi` from the terminal

## 🎯 How to Use

1. **Launch pingyi** - Open the application after installation
2. **Select Capture Mode** - Choose between screenshot translation or OCR extraction
3. **Capture Area** - Click and drag to select the area on your screen containing text
4. **View Results** - The recognized text appears in the application window
5. **Translate** - If translation is enabled, the translated text shows below the original

### Configuration Options

- **OCR Engine** - Choose between PaddleOCR (default) or other supported engines
- **Translation Backend** - Select llama.cpp, Ollama, or other local LLM options
- **Language Settings** - Set source and target languages for translation
- **Hotkeys** - Customize keyboard shortcuts for quick captures

## 🔧 Setup Guides

### Setting Up OCR

pingyi comes with PaddleOCR pre-configured. For best results:
1. Ensure your system has the necessary language packs installed
2. The first run may download OCR model files automatically
3. Test with a simple screenshot to verify text recognition works

### Setting Up Local Translation

**Using llama.cpp:**
1. Download a compatible GGUF model file
2. In pingyi settings, point to the model file location
3. Adjust memory usage settings based on your system

**Using Ollama:**
1. Install Ollama on your system
2. Pull a translation model (e.g., `ollama pull llama2`)
3. In pingyi settings, select Ollama as the translation backend

## ❓ Frequently Asked Questions

**Q: Does pingyi need an internet connection?**
A: No, pingyi works completely offline. All processing happens on your computer.

**Q: Can I use pingyi with any language?**
A: pingyi supports many languages through PaddleOCR and various translation models. Check the documentation for specific language support.

**Q: Is my data safe?**
A: Yes, all processing is local. No data leaves your computer.

**Q: Why is the first launch slower?**
A: The first launch may download necessary OCR and translation model files. Subsequent launches are faster.

## 🛠️ Troubleshooting

**OCR not working:**
- Ensure you have enough screen area selected
- Check that the text is clearly visible and not too small
- Try restarting the application

**Translation not responding:**
- Verify your translation backend is properly configured
- Check that the model file is valid and compatible
- Reduce the model size if memory is limited

**Application won't start on Linux:**
- Ensure you're using X11, not Wayland
- Install required dependencies: `sudo apt install libx11-dev libxrandr-dev`

## 🤝 Contributing

pingyi is open-source and welcomes contributions. If you'd like to help:
- Report bugs or suggest features on GitHub Issues
- Submit pull requests for improvements
- Help translate the application into more languages

## 📄 License

This project is open source. Check the repository for license details.

## Keywords

avalonia, csharp, linux, llama-cpp, local-llm, ocr, offline-first, offline-translation, ollama, paddleocr, screen-translator, screenshot, screenshot-translation, text-recognition, translation, windows