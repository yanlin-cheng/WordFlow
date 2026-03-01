@echo off
chcp 65001 >nul
echo ==========================================
echo WordFlow 安装包构建脚本
echo ==========================================
echo.

set OUTPUT_DIR=Output\Installer
set BUILD_DIR=bin\Release\net8.0-windows

REM 创建输出目录
if not exist %OUTPUT_DIR% mkdir %OUTPUT_DIR%

echo [1/4] 清理旧文件...
if exist %OUTPUT_DIR%\WordFlow rmdir /s /q %OUTPUT_DIR%\WordFlow

echo [2/4] 复制程序文件...
mkdir %OUTPUT_DIR%\WordFlow
xcopy /s /y %BUILD_DIR%\WordFlow.exe %OUTPUT_DIR%\WordFlow\
xcopy /s /y %BUILD_DIR%\*.dll %OUTPUT_DIR%\WordFlow\
xcopy /s /y %BUILD_DIR%\*.runtimeconfig.json %OUTPUT_DIR%\WordFlow\
xcopy /s /y %BUILD_DIR%\runtimes %OUTPUT_DIR%\WordFlow\runtimes /E
xcopy /s /y %BUILD_DIR%\win-x64 %OUTPUT_DIR%\WordFlow\win-x64 /E

echo [3/4] 复制 PythonASR 脚本和配置文件（排除 venv、__pycache__ 和 models）...
robocopy PythonASR %OUTPUT_DIR%\WordFlow\PythonASR /E /XD venv __pycache__ models /NJH /NJS /NDL /NC /NS

echo [3.5/4] 复制嵌入的 Python 环境（完整版，包含所有运行时依赖）...
REM 创建 python 目录结构
mkdir %OUTPUT_DIR%\WordFlow\PythonASR\python

REM 复制 Python 可执行文件和核心 DLL
xcopy /y PythonASR\python\python.exe %OUTPUT_DIR%\WordFlow\PythonASR\python\
xcopy /y PythonASR\python\python3.dll %OUTPUT_DIR%\WordFlow\PythonASR\python\
xcopy /y PythonASR\python\python311.dll %OUTPUT_DIR%\WordFlow\PythonASR\python\
xcopy /y PythonASR\python\python311._pth %OUTPUT_DIR%\WordFlow\PythonASR\python\
xcopy /y PythonASR\python\libcrypto-3.dll %OUTPUT_DIR%\WordFlow\PythonASR\python\
xcopy /y PythonASR\python\libffi-8.dll %OUTPUT_DIR%\WordFlow\PythonASR\python\
xcopy /y PythonASR\python\libssl-3.dll %OUTPUT_DIR%\WordFlow\PythonASR\python\
xcopy /y PythonASR\python\sqlite3.dll %OUTPUT_DIR%\WordFlow\PythonASR\python\
xcopy /y PythonASR\python\vcruntime140.dll %OUTPUT_DIR%\WordFlow\PythonASR\python\
xcopy /y PythonASR\python\vcruntime140_1.dll %OUTPUT_DIR%\WordFlow\PythonASR\python\

REM 复制完整的 site-packages（包含 sherpa_onnx 和所有依赖）
echo 复制 site-packages...
robocopy PythonASR\python\Lib\site-packages %OUTPUT_DIR%\WordFlow\PythonASR\python\Lib\site-packages /E /XD __pycache__ tests /NJH /NJS /NDL /NC /NS

REM 复制 Scripts 中的 DLL
xcopy /y PythonASR\python\Scripts\*.dll %OUTPUT_DIR%\WordFlow\PythonASR\python\Scripts\

REM 创建空的 models 目录（模型由用户在程序内下载）
if not exist %OUTPUT_DIR%\WordFlow\PythonASR\models mkdir %OUTPUT_DIR%\WordFlow\PythonASR\models

xcopy /s /y /i Data %OUTPUT_DIR%\WordFlow\Data

echo [4/4] 编译 Inno Setup 安装程序...
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer\WordFlowSetup.iss
    if errorlevel 1 (
        echo 错误：Inno Setup 编译失败！
        pause
        exit /b 1
    )
) else (
    echo 警告：未找到 Inno Setup，跳过安装程序编译。
    echo 请手动使用 Inno Setup 编译 Installer\WordFlowSetup.iss
)

echo.
echo ==========================================
echo 构建完成！
echo 输出目录：%OUTPUT_DIR%\WordFlow
echo 安装程序：%OUTPUT_DIR%\WordFlow_Setup.exe
echo ==========================================
echo.
echo 说明：
echo - 安装包包含完整的 Python 环境（约 60MB）
echo - 不包含模型文件，用户在程序内下载
echo - 首次运行时自动启动 Python 服务
pause
