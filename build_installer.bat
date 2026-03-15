@echo off
setlocal EnableDelayedExpansion
chcp 65001 >nul
title WordFlow 一键发布打包工具
echo ==========================================
echo WordFlow 一键发布打包工具
echo ==========================================
echo.

REM 切换到脚本所在目录，确保所有相对路径正确
cd /d "%~dp0"

REM ==================== 配置 ====================
set "OUTPUT_DIR=Output\Installer"
set "SOURCE_DIR=publish_installer"
set "INNO_SETUP_PATH=H:\Inno Setup 6"
set "ISS_FILE=Installer\WordFlowSetup.iss"

REM ==================== 步骤 0: 清理旧文件 ====================
echo [0/6] 清理旧文件...
if exist %SOURCE_DIR% (
    echo - 清理 %SOURCE_DIR% 目录
    rmdir /s /q %SOURCE_DIR%
)
if exist %OUTPUT_DIR%\WordFlow (
    echo - 清理 %OUTPUT_DIR%\WordFlow 目录
    rmdir /s /q %OUTPUT_DIR%\WordFlow
)
if exist %OUTPUT_DIR%\WordFlow_Setup.exe (
    echo - 清理旧安装包
    del /q %OUTPUT_DIR%\WordFlow_Setup.exe
)
echo 清理完成！
echo.

REM ==================== 步骤 1: 执行 dotnet publish ====================
echo [1/6] 发布项目（执行 dotnet publish）...
echo 命令：dotnet publish WordFlow.csproj -c Release -r win-x64 --self-contained true -o %SOURCE_DIR%
echo.

dotnet publish WordFlow.csproj -c Release -r win-x64 --self-contained true -o %SOURCE_DIR%
if errorlevel 1 (
    echo.
    echo ==========================================
    echo 错误：dotnet publish 失败！
    echo ==========================================
    echo 请检查：
    echo 1. .NET SDK 是否已安装
    echo 2. WordFlow.csproj 文件是否存在
    echo 3. 项目是否有编译错误
    echo.
    pause
    exit /b 1
)
echo 发布完成！
echo.

REM ==================== 步骤 2: 验证发布结果 ====================
echo [2/6] 验证发布结果...

REM 检查主程序文件
if not exist %SOURCE_DIR%\WordFlow.exe (
    echo 错误：找不到 WordFlow.exe
    echo 请检查发布是否成功
    pause
    exit /b 1
)

REM 检查关键配置文件
if not exist %SOURCE_DIR%\Data\models.json (
    echo 错误：找不到 %SOURCE_DIR%\Data\models.json
    echo 请检查 Data 目录是否正确复制
    pause
    exit /b 1
)

REM 检查 PythonASR 目录
if not exist %SOURCE_DIR%\PythonASR\asr_server.py (
    echo 错误：找不到 %SOURCE_DIR%\PythonASR\asr_server.py
    echo 请检查 PythonASR 目录是否正确复制
    pause
    exit /b 1
)

REM 检查 Python 嵌入环境
if not exist %SOURCE_DIR%\PythonASR\python\python.exe (
    echo 错误：找不到 %SOURCE_DIR%\PythonASR\python\python.exe
    echo 请检查 Python 环境是否正确复制
    pause
    exit /b 1
)

echo - WordFlow.exe 存在 ✓
echo - Data\models.json 存在 ✓
echo - PythonASR\asr_server.py 存在 ✓
echo - PythonASR\python\python.exe 存在 ✓
echo 验证通过！
echo.

REM ==================== 步骤 3: 读取版本信息 ====================
echo [3/6] 读取版本信息...

REM 读取 models.json 版本
set MODEL_VERSION=未知
for /f "tokens=2 delims=:" %%a in ('findstr "version" %SOURCE_DIR%\Data\models.json') do (
    set MODEL_VERSION=%%a
    goto :versionFound
)
:versionFound
echo - models.json 版本：%MODEL_VERSION%

REM 获取程序版本（从文件版本）
set APP_VERSION=
for %%i in (%SOURCE_DIR%\WordFlow.exe) do set APP_VERSION=%%~zi
echo - WordFlow.exe 大小：%APP_VERSION% 字节
echo.

REM ==================== 步骤 4: 复制到 Output 目录 ====================
echo [4/6] 复制程序文件到打包目录...

mkdir %OUTPUT_DIR%\WordFlow
mkdir %OUTPUT_DIR%\WordFlow\PythonASR
mkdir %OUTPUT_DIR%\WordFlow\Data

echo - 复制主程序文件...
xcopy /s /y %SOURCE_DIR%\*.exe %OUTPUT_DIR%\WordFlow\
xcopy /s /y %SOURCE_DIR%\*.dll %OUTPUT_DIR%\WordFlow\
xcopy /s /y %SOURCE_DIR%\*.pdb %OUTPUT_DIR%\WordFlow\
xcopy /s /y %SOURCE_DIR%\*.runtimeconfig.json %OUTPUT_DIR%\WordFlow\
xcopy /s /y %SOURCE_DIR%\*.deps.json %OUTPUT_DIR%\WordFlow\

echo - 复制语言资源（只保留英文，中文已包含在主程序）...
if exist %SOURCE_DIR%\en (
    xcopy /s /y %SOURCE_DIR%\en %OUTPUT_DIR%\WordFlow\en /E
    echo   + 英文资源已复制
)
REM 注意：已排除其他语言包以节省空间 (约 15MB)
REM 排除的语言：cs de es fr it ja ko pl pt-BR ru zh-Hant

echo - 复制 runtimes 目录...
if exist %SOURCE_DIR%\runtimes (
    xcopy /s /y %SOURCE_DIR%\runtimes %OUTPUT_DIR%\WordFlow\runtimes /E
)

echo - 复制 PythonASR 目录...
xcopy /s /y %SOURCE_DIR%\PythonASR\* %OUTPUT_DIR%\WordFlow\PythonASR\ /E

echo - 复制 Data 目录...
xcopy /s /y %SOURCE_DIR%\Data\* %OUTPUT_DIR%\WordFlow\Data\ /E

echo - 复制其他必要文件...
if exist %SOURCE_DIR%\AppxManifest.xml (
    xcopy /s /y %SOURCE_DIR%\AppxManifest.xml %OUTPUT_DIR%\WordFlow\
)
if exist %SOURCE_DIR%\MsixManifest.xml (
    xcopy /s /y %SOURCE_DIR%\MsixManifest.xml %OUTPUT_DIR%\WordFlow\
)

echo 复制完成！
echo.

REM ==================== 步骤 4.5: 清理不需要的语言包 ====================
echo [4.5/6] 清理不需要的语言包（节省安装包大小）...
REM 删除除英文外的所有语言包（中文已包含在主程序中）
for %%L in (cs de es fr it ja ko pl pt-BR ru zh-Hant) do (
    if exist %OUTPUT_DIR%\WordFlow\%%L (
        rmdir /s /q %OUTPUT_DIR%\WordFlow\%%L
        echo   - 已删除 %%L 语言包
    )
)
echo 语言包清理完成！
echo.

REM ==================== 步骤 5: 编译 Inno Setup ====================
echo [5/6] 编译 Inno Setup 安装程序...

REM 检查 ISS 文件是否存在
if not exist "%~dp0%ISS_FILE%" (
    echo 错误：找不到 %ISS_FILE%
    echo 请确认文件路径正确
    pause
    exit /b 1
)
echo - ISS 文件存在：%ISS_FILE%

REM 查找 Inno Setup
set "ISCC_EXE="
if exist "%INNO_SETUP_PATH%\ISCC.exe" (
    set "ISCC_EXE=%INNO_SETUP_PATH%\ISCC.exe"
    echo - 找到 Inno Setup: !ISCC_EXE!
) else if exist "%PROGRAMFILES%\Inno Setup 6\ISCC.exe" (
    set "ISCC_EXE=%PROGRAMFILES%\Inno Setup 6\ISCC.exe"
    echo - 找到 Inno Setup: !ISCC_EXE!
) else if exist "%PROGRAMFILES(X86)%\Inno Setup 6\ISCC.exe" (
    set "ISCC_EXE=%PROGRAMFILES(X86)%\Inno Setup 6\ISCC.exe"
    echo - 找到 Inno Setup: !ISCC_EXE!
) else (
    echo.
    echo 警告：未找到 Inno Setup (ISCC.exe)
    echo.
    echo 已检测的位置：
    echo - %INNO_SETUP_PATH%\ISCC.exe
    echo - %PROGRAMFILES%\Inno Setup 6\ISCC.exe
    echo - %PROGRAMFILES(X86)%\Inno Setup 6\ISCC.exe
    echo.
    echo 程序文件已复制到：%OUTPUT_DIR%\WordFlow
    echo 请手动使用 Inno Setup 编译 %ISS_FILE%
    echo.
    goto :skipInno
)

REM 使用 ISCC.exe 编译
echo - 开始编译安装程序...
"!ISCC_EXE!" "%~dp0%ISS_FILE%"
if errorlevel 1 (
    echo.
    echo ==========================================
    echo 错误：Inno Setup 编译失败！
    echo ==========================================
    echo 请检查：
    echo 1. ISS 文件路径是否正确
    echo 2. ISS 文件中引用的文件是否存在
    echo 3. Inno Setup 是否正确安装
    echo.
    pause
    exit /b 1
)
echo Inno Setup 编译成功！
echo.

:skipInno
REM ==================== 步骤 6: 显示结果 ====================
echo [6/6] 构建完成！
echo.
echo ==========================================
echo 构建结果
echo ==========================================
echo.
echo 程序目录：%OUTPUT_DIR%\WordFlow
echo 安装包：%OUTPUT_DIR%\WordFlow_Setup.exe
echo.
echo 版本信息：
echo - models.json: %MODEL_VERSION%
echo.
echo 下一步操作：
echo 1. 测试安装包功能是否正常
echo 2. 在目标机器上安装并测试
echo 3. 检查语音识别功能是否正常
echo.
echo ==========================================

REM 检查安装包是否生成
if exist %OUTPUT_DIR%\WordFlow_Setup.exe (
    echo ✓ 安装包已生成：%OUTPUT_DIR%\WordFlow_Setup.exe
    echo.
    echo 是否打开输出目录？(Y/N)
    set /p OPEN_DIR=
    if /i "%OPEN_DIR%"=="Y" (
        explorer "%~dp0%OUTPUT_DIR%"
    )
) else (
    echo ⚠ 安装包未生成，请手动编译 Inno Setup
)

echo.
echo ==========================================
pause
