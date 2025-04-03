# CodeIndexer启动脚本
# 此脚本用于启动CodeIndexer的服务端和客户端

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$ServiceOnly,
    
    [Parameter()]
    [switch]$ClientOnly,
    
    [Parameter()]
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$workspaceRoot = $PSScriptRoot
$servicePath = Join-Path $workspaceRoot "CodeIndexer.Service"
$clientPath = Join-Path $workspaceRoot "CodeIndexer.Client"
$solutionPath = Join-Path $workspaceRoot "CodeIndexer.sln"

# 显示标题
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "      C#代码索引系统启动脚本          " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""


# 构建解决方案
if (-not $NoBuild) {
    Write-Host "正在构建解决方案..." -ForegroundColor Yellow
    try {
        Push-Location $workspaceRoot
        dotnet build $solutionPath -c Release
        if ($LASTEXITCODE -ne 0) { throw "构建失败，退出代码: $LASTEXITCODE" }
        Write-Host "构建成功!" -ForegroundColor Green
        Pop-Location
    }
    catch {
        Write-Host "构建失败: $_" -ForegroundColor Red
        if (Test-Path variable:/oldLocation) { Pop-Location }
        exit 1
    }
}

# 启动服务和客户端的函数
function Start-CodeIndexerService {
    Write-Host "`n正在启动CodeIndexer服务..." -ForegroundColor Yellow
    try {
        Push-Location $servicePath
        Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host '启动CodeIndexer服务...' -ForegroundColor Cyan; dotnet run"
        Write-Host "服务启动成功!" -ForegroundColor Green
        Write-Host "API地址: http://localhost:5000" -ForegroundColor Cyan
        Write-Host "Swagger文档: http://localhost:5000/swagger" -ForegroundColor Cyan
        Pop-Location
        # 等待服务启动
        Write-Host "等待服务启动..." -ForegroundColor Yellow
        Start-Sleep -Seconds 5
    }
    catch {
        Write-Host "启动服务失败: $_" -ForegroundColor Red
        if (Test-Path variable:/oldLocation) { Pop-Location }
        return $false
    }
    return $true
}

function Start-CodeIndexerClient {
    Write-Host "`n正在启动CodeIndexer客户端..." -ForegroundColor Yellow
    try {
        Push-Location $clientPath
        Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host '启动CodeIndexer客户端...' -ForegroundColor Cyan; dotnet run"
        Write-Host "客户端启动成功!" -ForegroundColor Green
        Pop-Location
    }
    catch {
        Write-Host "启动客户端失败: $_" -ForegroundColor Red
        if (Test-Path variable:/oldLocation) { Pop-Location }
        return $false
    }
    return $true
}

# 根据参数启动服务和客户端
if ($ServiceOnly) {
    # 只启动服务
    $serviceStarted = Start-CodeIndexerService
    if (-not $serviceStarted) { exit 1 }
}
elseif ($ClientOnly) {
    # 只启动客户端
    $clientStarted = Start-CodeIndexerClient
    if (-not $clientStarted) { exit 1 }
}
else {
    # 启动服务和客户端
    $serviceStarted = Start-CodeIndexerService
    if (-not $serviceStarted) { exit 1 }
    
    $clientStarted = Start-CodeIndexerClient
    if (-not $clientStarted) { exit 1 }
}

Write-Host "`n所有组件已启动!" -ForegroundColor Green
Write-Host "提示: 每个组件都在单独的PowerShell窗口中运行,关闭窗口即可停止相应组件" -ForegroundColor Yellow