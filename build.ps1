# CodeIndexer Build Script
# 此脚本用于构建CodeIndexer解决方案

[CmdletBinding()]
param(
    [Parameter()]
    [string]$Configuration = "Release",
    
    [Parameter()]
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$workspaceRoot = $PSScriptRoot
$solutionPath = Join-Path $workspaceRoot "CodeIndexer.sln"

# 显示标题
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "      CodeIndexer Build Script          " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 检查.NET SDK是否安装
try {
    $dotnetVersion = dotnet --version
    Write-Host "Detected .NET SDK version: $dotnetVersion" -ForegroundColor Green
}
catch {
    Write-Host "Error: .NET SDK not detected. Please install .NET 6.0 SDK or higher." -ForegroundColor Red
    Write-Host "Download URL: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# 清理解决方案（如果需要）
if ($Clean) {
    Write-Host "Cleaning solution..." -ForegroundColor Yellow
    try {
        Push-Location $workspaceRoot
        dotnet clean $solutionPath -c $Configuration
        if ($LASTEXITCODE -ne 0) { throw "Clean failed with exit code: $LASTEXITCODE" }
        Write-Host "Clean successful!" -ForegroundColor Green
        Pop-Location
    }
    catch {
        Write-Host "Clean failed: $_" -ForegroundColor Red
        if (Test-Path variable:/oldLocation) { Pop-Location }
        exit 1
    }
}

# 恢复NuGet包
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
try {
    Push-Location $workspaceRoot
    dotnet restore $solutionPath
    if ($LASTEXITCODE -ne 0) { throw "Package restore failed with exit code: $LASTEXITCODE" }
    Write-Host "Package restore successful!" -ForegroundColor Green
    Pop-Location
}
catch {
    Write-Host "Package restore failed: $_" -ForegroundColor Red
    if (Test-Path variable:/oldLocation) { Pop-Location }
    exit 1
}

# 构建解决方案
Write-Host "Building solution..." -ForegroundColor Yellow
try {
    Push-Location $workspaceRoot
    dotnet build $solutionPath -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code: $LASTEXITCODE" }
    Write-Host "Build successful!" -ForegroundColor Green
    Pop-Location
}
catch {
    Write-Host "Build failed: $_" -ForegroundColor Red
    if (Test-Path variable:/oldLocation) { Pop-Location }
    exit 1
}

Write-Host ""
Write-Host "Build process completed successfully!" -ForegroundColor Green
Write-Host "Use .\run.ps1 to start the application components." -ForegroundColor Yellow