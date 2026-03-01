@echo off
chcp 65001 >nul
title WordFlow ASR Service
cls

REM 确保在当前目录
pushd "%~dp0"

echo ==========================================
echo WordFlow ASR Service (Sherpa-ONNX)
echo ==========================================
echo.

REM 使用嵌入的 Python（安装包自带环境）
set "PYTHON_EXE=python\python.exe"

if not exist "%PYTHON_EXE%" (
    echo 错误：找不到嵌入的 Python！
    echo 路径：%CD%\%PYTHON_EXE%
    echo.
    echo 请重新安装 WordFlow 或联系技术支持。
    popd
    pause
    exit /b 1
)

echo [1/2] 使用嵌入的 Python: %PYTHON_EXE%
echo.

echo [2/2] 启动 ASR 服务...
echo.
echo 运行：%PYTHON_EXE% asr_server.py
echo.

REM 启动服务（前台运行，便于查看日志）
"%PYTHON_EXE%" asr_server.py

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
