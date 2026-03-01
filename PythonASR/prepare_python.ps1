# 准备嵌入式 Python 环境
param(
    [string]$PythonVersion = "3.11.9",
    [string]$TargetDir = "$PSScriptRoot\python"
)

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "准备 WordFlow 嵌入式 Python 环境" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null

$pythonUrl = "https://www.python.org/ftp/python/$PythonVersion/python-$PythonVersion-embed-amd64.zip"
$zipPath = "$env:TEMP\python-embed.zip"

Write-Host "[1/4] 下载 Python $PythonVersion 嵌入式版本..." -ForegroundColor Yellow
Write-Host "      来源: $pythonUrl"

try {
    Invoke-WebRequest -Uri $pythonUrl -OutFile $zipPath -UseBasicParsing
    Write-Host "      下载完成" -ForegroundColor Green
} catch {
    Write-Host "      下载失败: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[2/4] 解压到 $TargetDir..." -ForegroundColor Yellow
Expand-Archive -Path $zipPath -DestinationPath $TargetDir -Force
Remove-Item $zipPath -Force
Write-Host "      解压完成" -ForegroundColor Green

Write-Host ""
Write-Host "[3/4] 配置 Python 环境..." -ForegroundColor Yellow
$pthFile = Get-ChildItem -Path $TargetDir -Filter "python*._pth" | Select-Object -First 1
if ($pthFile) {
    $content = Get-Content $pthFile.FullName
    $content = $content -replace "^#import site", "import site"
    Set-Content $pthFile.FullName $content
    Write-Host "      已启用 site 导入" -ForegroundColor Green
}

$pipUrl = "https://bootstrap.pypa.io/get-pip.py"
$pipPath = "$TargetDir\get-pip.py"
Write-Host "      下载 pip 安装脚本..." -ForegroundColor Yellow
Invoke-WebRequest -Uri $pipUrl -OutFile $pipPath -UseBasicParsing

Write-Host ""
Write-Host "[4/4] 安装 pip 和依赖..." -ForegroundColor Yellow
& "$TargetDir\python.exe" $pipPath --no-warn-script-location

$requirementsFile = "$PSScriptRoot\requirements.txt"
if (Test-Path $requirementsFile) {
    Write-Host "      安装依赖包..." -ForegroundColor Yellow
    & "$TargetDir\python.exe" -m pip install -r $requirementsFile --no-warn-script-location
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "嵌入式 Python 环境准备完成！" -ForegroundColor Green
Write-Host "位置: $TargetDir" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Read-Host "Press Enter to continue"
