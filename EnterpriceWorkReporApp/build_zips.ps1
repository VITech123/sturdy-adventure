# ============================================================
# Enterprise Work Report - ZIP Bundle Generator
# Creates two distribution packages:
#   1. EWR_Public.zip  - GitHub-safe (credentials stripped)
#   2. EWR_Private.zip - Full private version with credentials
# ============================================================

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$OutputDir = Join-Path $ProjectRoot "dist"

# Clean output directory
if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
New-Item -ItemType Directory -Path $OutputDir | Out-Null

# Directories and files to EXCLUDE from both zips
$ExcludePatterns = @(
    "bin", "obj", "publish", ".vs", ".git", ".gitignore",
    "dist", "app_data", "crash.log",
    "EnterpriseWorkReport_Release.zip",
    "EnterpriseWorkReport_SourceCode.zip",
    "build_zips.ps1"
)

function Test-Exclude($relativePath) {
    foreach ($pattern in $ExcludePatterns) {
        if ($relativePath -like "*\$pattern*" -or $relativePath -like "*/$pattern*" -or $relativePath -eq $pattern) {
            return $true
        }
    }
    return $false
}

function Copy-FilteredTree($source, $destination) {
    Get-ChildItem -Path $source -Recurse -Force | ForEach-Object {
        $relativePath = $_.FullName.Substring($source.Length + 1)
        
        # Check exclusions
        $excluded = $false
        foreach ($pattern in $ExcludePatterns) {
            if ($relativePath -like "$pattern\*" -or $relativePath -like "$pattern" -or $relativePath -like "*\$pattern\*") {
                $excluded = $true
                break
            }
        }
        
        if (-not $excluded) {
            $targetPath = Join-Path $destination $relativePath
            if ($_.PSIsContainer) {
                New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
            } else {
                $parentDir = Split-Path $targetPath -Parent
                if (-not (Test-Path $parentDir)) {
                    New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
                }
                Copy-Item $_.FullName $targetPath -Force
            }
        }
    }
}

# ============================================================
# 1. PRIVATE VERSION (Full credentials intact)
# ============================================================
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Building PRIVATE Version (EWR_Private.zip)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$PrivateStaging = Join-Path $OutputDir "_private_staging"
New-Item -ItemType Directory -Path $PrivateStaging | Out-Null

Copy-FilteredTree $ProjectRoot $PrivateStaging

$PrivateZip = Join-Path $OutputDir "EWR_Private.zip"
Compress-Archive -Path "$PrivateStaging\*" -DestinationPath $PrivateZip -Force

Write-Host "[OK] Private ZIP created: $PrivateZip" -ForegroundColor Green

# ============================================================
# 2. PUBLIC VERSION (GitHub-safe, credentials stripped)
# ============================================================
Write-Host ""
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "  Building PUBLIC Version (EWR_Public.zip)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow

$PublicStaging = Join-Path $OutputDir "_public_staging"
New-Item -ItemType Directory -Path $PublicStaging | Out-Null

Copy-FilteredTree $ProjectRoot $PublicStaging

# Strip PostgreSQL credentials from DatabaseService.cs
$dbServicePath = Join-Path $PublicStaging "Services\DatabaseService.cs"
if (Test-Path $dbServicePath) {
    $content = Get-Content $dbServicePath -Raw
    
    # Replace the connection string with a placeholder
    $content = $content -replace 'Host=localhost;Port=5432;Database=vendhan;Username=postgres;Password=vendhan@123;Pooling=true;Maximum Pool Size=100;', 
                                  'Host=localhost;Port=5432;Database=YOUR_DB;Username=YOUR_USER;Password=YOUR_PASSWORD;Pooling=true;Maximum Pool Size=100;'
    
    Set-Content $dbServicePath $content -NoNewline
    Write-Host "  [OK] Stripped credentials from DatabaseService.cs" -ForegroundColor Green
}

# Strip any credentials from WebApp config if present
$webAppConfigs = Get-ChildItem -Path $PublicStaging -Recurse -Include "appsettings*.json","web.config" -ErrorAction SilentlyContinue
foreach ($config in $webAppConfigs) {
    $content = Get-Content $config.FullName -Raw
    $content = $content -replace 'vendhan@123', 'YOUR_PASSWORD'
    $content = $content -replace 'vendhan', 'YOUR_DB'
    Set-Content $config.FullName $content -NoNewline
    Write-Host "  [OK] Stripped credentials from $($config.Name)" -ForegroundColor Green
}

$PublicZip = Join-Path $OutputDir "EWR_Public.zip"
Compress-Archive -Path "$PublicStaging\*" -DestinationPath $PublicZip -Force

Write-Host "[OK] Public ZIP created: $PublicZip" -ForegroundColor Green

# ============================================================
# Cleanup staging directories
# ============================================================
Remove-Item (Join-Path $OutputDir "_private_staging") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $OutputDir "_public_staging") -Recurse -Force -ErrorAction SilentlyContinue

# ============================================================
# Summary
# ============================================================
Write-Host ""
Write-Host "========================================" -ForegroundColor Magenta
Write-Host "  BUILD COMPLETE" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""

$privateSize = [math]::Round((Get-Item $PrivateZip).Length / 1MB, 2)
$publicSize = [math]::Round((Get-Item $PublicZip).Length / 1MB, 2)

Write-Host "  EWR_Private.zip : $privateSize MB  (Full credentials)" -ForegroundColor White
Write-Host "  EWR_Public.zip  : $publicSize MB  (GitHub-safe)" -ForegroundColor White
Write-Host ""
Write-Host "  Output folder: $OutputDir" -ForegroundColor Gray
Write-Host ""
