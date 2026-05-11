# ============================================================
# Enterprise Work Report - Binary Release Packager
# Builds the project and packages binaries for distribution
# ============================================================

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$OutputDir = Join-Path $ProjectRoot "dist"
$ReleaseDir = Join-Path $ProjectRoot "release"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Building and Packaging Release" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. Build the project in Release mode
Write-Host "Step 1: Building project..." -ForegroundColor Gray
dotnet build EnterpriseWorkReport.csproj -c Release | Out-Null

$BinSource = Join-Path $ProjectRoot "bin\Release\net48"

if (-not (Test-Path $BinSource)) {
    Write-Error "Build output not found at $BinSource"
}

# 2. Prepare Release folder
Write-Host "Step 2: Preparing release folder..." -ForegroundColor Gray
if (Test-Path $ReleaseDir) { Remove-Item $ReleaseDir -Recurse -Force }
New-Item -ItemType Directory -Path $ReleaseDir | Out-Null

# 3. Copy binaries and dependencies
Write-Host "Step 3: Copying binaries..." -ForegroundColor Gray
Copy-Item "$BinSource\*" $ReleaseDir -Recurse -Force

# Copy VC++ Redistributable DLLs to avoid requiring separate installation for Tesseract
Write-Host "Copying VC++ runtime libraries for standalone deployment..." -ForegroundColor Gray
$vc64_1 = "C:\Windows\System32\vcruntime140.dll"
$vc64_2 = "C:\Windows\System32\msvcp140.dll"
$vc86_1 = "C:\Windows\SysWOW64\vcruntime140.dll"
$vc86_2 = "C:\Windows\SysWOW64\msvcp140.dll"

$relX64 = Join-Path $ReleaseDir "x64"
$relX86 = Join-Path $ReleaseDir "x86"

if (Test-Path $relX64) {
    if (Test-Path $vc64_1) { Copy-Item $vc64_1 $relX64 -Force }
    if (Test-Path $vc64_2) { Copy-Item $vc64_2 $relX64 -Force }
}

if (Test-Path $relX86) {
    if (Test-Path $vc86_1) { Copy-Item $vc86_1 $relX86 -Force }
    if (Test-Path $vc86_2) { Copy-Item $vc86_2 $relX86 -Force }
}

# 4. Create ZIP for distribution
Write-Host "Step 4: Creating distribution ZIP..." -ForegroundColor Gray
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir | Out-Null }
$ZipFile = Join-Path $OutputDir "EnterpriseWorkReport_Release_v1.0.zip"
if (Test-Path $ZipFile) { Remove-Item $ZipFile -Force }

Compress-Archive -Path "$ReleaseDir\*" -DestinationPath $ZipFile -Force

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  RELEASE READY!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "  You can run the app from:" -ForegroundColor White
Write-Host "  $ReleaseDir\EnterpriseWorkReport.exe" -ForegroundColor Yellow
Write-Host ""
Write-Host "  Distribution ZIP:" -ForegroundColor White
Write-Host "  $ZipFile" -ForegroundColor Yellow
Write-Host ""
