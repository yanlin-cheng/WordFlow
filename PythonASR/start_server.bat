@echo off
chcp 65001 >nul
title WordFlow ASR Service
cls

REM 确保在当前目录
pushd "%~dp0"

REM 设置模型目录环境变量（可选）
set ASR_MODELS_DIR=models

echo ==========================================
echo WordFlow ASR Service (Sherpa-ONNX)
echo ==========================================
echo.

REM 使用嵌入的 Python（安装包自带环境）
REM 使用 %~dp0 获取批处理文件所在目录的完整路径
set "SCRIPT_DIR=%~dp0"
set "PYTHON_EXE=%SCRIPT_DIR%python\python.exe"

if not exist "%PYTHON_EXE%" (
    echo 错误：找不到嵌入的 Python！
    echo 路径：%PYTHON_EXE%
    echo 当前目录：%CD%
    echo 脚本目录：%SCRIPT_DIR%
    echo.
    echo 请重新安装 WordFlow 或联系技术支持。
    popd
    pause
    exit /b 1
)

echo [1/2] 使用嵌入的 Python: "%PYTHON_EXE%"
echo 脚本目录：%SCRIPT_DIR%
echo.

echo [2/2] 启动 ASR 服务...
echo.
echo 运行："%PYTHON_EXE%" asr_server.py --port 5000
echo.
echo 提示：服务启动后会自动检测已安装的模型
echo      如果没有模型，请在 WordFlow 中下载
echo.

REM 启动服务（前台运行，便于查看日志）
REM --port 5000: 使用默认端口 5000
REM 模型会在 WordFlow 中点击"使用"按钮时自动加载
cd /d "%SCRIPT_DIR%"
"%PYTHON_EXE%" asr_server.py --port 5000

if errorlevel 1 (
    echo.
    echo ==========================================
    echo 错误：服务器崩溃
    echo ==========================================
    echo.
    echo 请检查日志信息或联系技术支持。
    popd
    pause
    exit /b 1
)

echo.
echo 服务已正常停止
popd
pause
