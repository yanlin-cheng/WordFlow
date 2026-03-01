# WordFlow Python Embedded Environment Preparation Script
# This script prepares a minimal Python environment for distribution

param(
    [string]$PythonVersion = "3.11.8",
    [string]$OutputDir = "embedded_python",
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "WordFlow Python Embedded Environment Prep" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Clean if requested
if ($Clean -and (Test-Path $OutputDir)) {
    Write-Host "Cleaning existing directory..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $OutputDir
}

# Create output directory
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$OutputPath = Resolve-Path $OutputDir

Write-Host "Output directory: $OutputPath" -ForegroundColor Gray
Write-Host ""

# Download embedded Python
$PythonUrl = "https://www.python.org/ftp/python/$PythonVersion/python-$PythonVersion-embed-amd64.zip"
$PythonZip = "$OutputPath\python_embed.zip"

Write-Host "[1/4] Downloading Python $PythonVersion..." -ForegroundColor Green
Write-Host "URL: $PythonUrl" -ForegroundColor Gray

try {
    Invoke-WebRequest -Uri $PythonUrl -OutFile $PythonZip -UseBasicParsing
    Write-Host "Downloaded: $([math]::Round((Get-Item $PythonZip).Length / 1MB, 2)) MB" -ForegroundColor Gray
} catch {
    Write-Error "Failed to download Python: $_"
    exit 1
}

# Extract Python
Write-Host ""
Write-Host "[2/4] Extracting Python..." -ForegroundColor Green
Expand-Archive -Path $PythonZip -DestinationPath $OutputPath -Force
Remove-Item $PythonZip

# Enable site-packages by modifying python311._pth (or similar)
Write-Host ""
Write-Host "[3/4] Configuring Python environment..." -ForegroundColor Green

$pthFile = Get-ChildItem $OutputPath -Filter "python*._pth" | Select-Object -First 1
if ($pthFile) {
    $pthContent = Get-Content $pthFile.FullName
    # Uncomment import site line
    $pthContent = $pthContent -replace "^#import site", "import site"
    Set-Content $pthFile.FullName $pthContent
    Write-Host "Updated: $($pthFile.Name)" -ForegroundColor Gray
}

# Create get-pip.py
$GetPipUrl = "https://bootstrap.pypa.io/get-pip.py"
$GetPipPath = "$OutputPath\get-pip.py"

Write-Host "Downloading get-pip.py..." -ForegroundColor Gray
try {
    Invoke-WebRequest -Uri $GetPipUrl -OutFile $GetPipPath -UseBasicParsing
} catch {
    Write-Error "Failed to download get-pip.py: $_"
    exit 1
}

# Install pip
Write-Host ""
Write-Host "[4/4] Installing pip and dependencies..." -ForegroundColor Green
$PythonExe = "$OutputPath\python.exe"

& $PythonExe $GetPipPath --no-warn-script-location
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to install pip"
    exit 1
}

# Install required packages
$Packages = @(
    "sherpa-onnx>=1.10.0",
    "numpy>=1.21.0",
    "requests>=2.28.0"
)

Write-Host "Installing packages: $($Packages -join ', ')" -ForegroundColor Gray
& $PythonExe -m pip install $Packages --no-warn-script-location
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to install packages"
    exit 1
}

# Clean up
Remove-Item $GetPipPath -ErrorAction SilentlyContinue

# Create a marker file
"WordFlow Embedded Python Environment" | Set-Content "$OutputPath\WORDFLOW_EMBEDDED.marker"

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Location: $OutputPath" -ForegroundColor Gray
Write-Host "Size: $([math]::Round((Get-ChildItem $OutputPath -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB, 2)) MB" -ForegroundColor Gray
Write-Host ""
Write-Host "To use in WordFlow:" -ForegroundColor Yellow
Write-Host "  1. Copy this folder to your WordFlow installation" -ForegroundColor Gray
Write-Host "  2. Rename it to 'python' or 'embedded_python'" -ForegroundColor Gray
Write-Host "  3. The start_server.bat will automatically detect it" -ForegroundColor Gray
