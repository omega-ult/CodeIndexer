# CodeIndexer Run Script
# 此脚本用于启动CodeIndexer的服务端和客户端组件

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$ServiceOnly,
    
    [Parameter()]
    [switch]$ClientOnly,
    
    [Parameter()]
    [switch]$NoBuild,
    
    [Parameter()]
    [switch]$TestMode,
    
    [Parameter()]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$workspaceRoot = $PSScriptRoot
$solutionPath = Join-Path $workspaceRoot "CodeIndexer.sln"
$servicePath = Join-Path $workspaceRoot "CodeIndexer.Service"
$clientPath = Join-Path $workspaceRoot "CodeIndexer.Client"
$testPath = Join-Path $workspaceRoot "CodeIndexer.Tests"

# 显示标题
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "      CodeIndexer Run Script            " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 如果需要构建，调用build.ps1脚本
if (-not $NoBuild) {
    Write-Host "Building solution using build.ps1..." -ForegroundColor Yellow
    try {
        & "$workspaceRoot\build.ps1" -Configuration $Configuration
        if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code: $LASTEXITCODE" }
    }
    catch {
        Write-Host "Build failed: $_" -ForegroundColor Red
        exit 1
    }
}

# 启动服务的函数
function Start-CodeIndexerService {
    Write-Host "Starting CodeIndexer service..." -ForegroundColor Yellow
    try {
        Push-Location $servicePath
        Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host 'Starting CodeIndexer service...' -ForegroundColor Cyan; dotnet run --no-build -c $Configuration"
        Write-Host "Service started successfully!" -ForegroundColor Green
        Write-Host "API URL: http://localhost:5000" -ForegroundColor Cyan
        Write-Host "Swagger docs: http://localhost:5000/swagger" -ForegroundColor Cyan
        Pop-Location
        # 等待服务启动
        Write-Host "Waiting for service to start..." -ForegroundColor Yellow
        Start-Sleep -Seconds 5
    }
    catch {
        Write-Host "Failed to start service: $_" -ForegroundColor Red
        if (Test-Path variable:/oldLocation) { Pop-Location }
        return $false
    }
    return $true
}

# 启动客户端的函数
function Start-CodeIndexerClient {
    Write-Host "Starting CodeIndexer client..." -ForegroundColor Yellow
    try {
        Push-Location $clientPath
        Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host 'Starting CodeIndexer client...' -ForegroundColor Cyan; dotnet run --no-build -c $Configuration"
        Write-Host "Client started successfully!" -ForegroundColor Green
        Pop-Location
    }
    catch {
        Write-Host "Failed to start client: $_" -ForegroundColor Red
        if (Test-Path variable:/oldLocation) { Pop-Location }
        return $false
    }
    return $true
}

# 运行测试的函数
function Start-CodeIndexerTest {
    Write-Host "Running CodeIndexer tests..." -ForegroundColor Yellow
    try {
        Push-Location $testPath
        dotnet test --no-build -c $Configuration
        if ($LASTEXITCODE -ne 0) { 
            Write-Host "Tests failed with exit code: $LASTEXITCODE" -ForegroundColor Red
            Pop-Location
            return $false
        }
        Write-Host "Tests completed successfully!" -ForegroundColor Green
        Pop-Location
    }
    catch {
        Write-Host "Failed to run tests: $_" -ForegroundColor Red
        if (Test-Path variable:/oldLocation) { Pop-Location }
        return $false
    }
    return $true
}

# 根据参数启动组件
if ($TestMode) {
    # 运行测试
    $testSuccess = Start-CodeIndexerTest
    if (-not $testSuccess) { exit 1 }
    
    # 如果只是测试模式，则退出
    if (-not $ServiceOnly -and -not $ClientOnly) {
        Write-Host "Test mode completed. Exiting..." -ForegroundColor Green
        exit 0
    }
}

# 启动服务和客户端
if ($ServiceOnly -or (-not $ClientOnly)) {
    # 启动服务
    $serviceStarted = Start-CodeIndexerService
    if (-not $serviceStarted) { exit 1 }
}

if ($ClientOnly -or (-not $ServiceOnly)) {
    # 启动客户端
    $clientStarted = Start-CodeIndexerClient
    if (-not $clientStarted) { exit 1 }
}

Write-Host ""
Write-Host "All components started!" -ForegroundColor Green
Write-Host "Note: Each component runs in its own PowerShell window. Close the window to stop the component." -ForegroundColor Yellow