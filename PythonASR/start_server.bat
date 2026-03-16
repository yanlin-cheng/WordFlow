@echo off
title WordFlow ASR Service
cls

REM Get the script directory
set "SCRIPT_DIR=%~dp0"

REM Change to script directory (handles spaces correctly)
cd /d "%SCRIPT_DIR%"

REM Set console code page to UTF-8
chcp 65001 >nul 2>&1

echo ==========================================
echo WordFlow ASR Service (Sherpa-ONNX)
echo ==========================================
echo.

REM Set Python executable path using relative path from current directory
REM This approach handles paths with spaces correctly
set "PYTHON_EXE=%CD%\python\python.exe"

if not exist "%PYTHON_EXE%" (
    echo.
    echo Error: Cannot find embedded Python!
    echo Path: %PYTHON_EXE%
    echo Current directory: %CD%
    echo.
    echo Please reinstall WordFlow or contact support.
    pause
    exit /b 1
)

echo [1/2] Using Python: "%PYTHON_EXE%"
echo Script directory: %CD%
echo.

echo [2/2] Starting ASR service...
echo.
echo Note: The service will automatically detect installed models.
echo       If no models are installed, please download in WordFlow.
echo.

REM Start service (foreground mode)
"%PYTHON_EXE%" asr_server.py --port 5000

if errorlevel 1 (
    echo.
    echo ==========================================
    echo Error: Server crashed
    echo ==========================================
    echo.
    echo Please check log files or contact support.
    pause
    exit /b 1
)

echo.
echo Service stopped normally
pause
