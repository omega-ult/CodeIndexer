# CodeIndexer 构建和运行脚本
# 此脚本用于构建整个解决方案并启动服务

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$NoBuild,
    
    [Parameter()]
    [switch]$ServiceOnly,
    
    [Parameter()]
    [switch]$ClientOnly,
    
    [Parameter()]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$workspaceRoot = $PSScriptRoot
$solutionPath = Join-Path $workspaceRoot "CodeIndexer.sln"
$servicePath = Join-Path $workspaceRoot "CodeIndexer.Service"
$clientPath = Join-Path $workspaceRoot "CodeIndexer.Client"

# 显示标题
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "      C#代码索引系统构建和运行脚本      " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 检查.NET SDK是否安装
try {
    $dotnetVersion = dotnet --version
    Write-Host "检测到.NET SDK版本: $dotnetVersion" -ForegroundColor Green
}
catch {
    Write-Host "错误: 未检测到.NET SDK。请安装.NET 6.0 SDK或更高版本。" -ForegroundColor Red
    Write-Host "下载地址: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# 恢复NuGet包
Write-Host "正在恢复NuGet包..." -ForegroundColor Yellow
try {
    Push-Location $workspaceRoot
    dotnet restore $solutionPath
    if ($LASTEXITCODE -ne 0) { throw "包恢复失败，退出代码: $LASTEXITCODE" }
    Write-Host "包恢复成功!" -ForegroundColor Green
    Pop-Location
}
catch {
    Write-Host "包恢复失败: $_" -ForegroundColor Red
    if (Test-Path variable:/oldLocation) { Pop-Location }
    exit 1
}

# 构建解决方案
if (-not $NoBuild) {
    Write-Host "正在构建解决方案..." -ForegroundColor Yellow
    try {
        Push-Location $workspaceRoot
        dotnet build $solutionPath -c $Configuration
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

# 启动服务的函数
function Start-CodeIndexerService {
    Write-Host "`n正在启动CodeIndexer服务..." -ForegroundColor Yellow
    try {
        Push-Location $servicePath
        $process = Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host '启动CodeIndexer服务...' -ForegroundColor Cyan; dotnet run --no-build -c $Configuration" -PassThru
        Write-Host "服务启动成功! 进程ID: $($process.Id)" -ForegroundColor Green
        Write-Host "API地址: http://localhost:5000" -ForegroundColor Cyan
        Write-Host "Swagger文档: http://localhost:5000/swagger" -ForegroundColor Cyan
        Pop-Location
        # 等待服务启动
        Write-Host "等待服务启动..." -ForegroundColor Yellow
        Start-Sleep -Seconds 5
        return $process
    }
    catch {
        Write-Host "启动服务失败: $_" -ForegroundColor Red
        if (Test-Path variable:/oldLocation) { Pop-Location }
        return $null
    }
}

# 启动客户端的函数
function Start-CodeIndexerClient {
    Write-Host "`n正在启动CodeIndexer客户端..." -ForegroundColor Yellow
    try {
        Push-Location $clientPath
        $process = Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host '启动CodeIndexer客户端...' -ForegroundColor Cyan; dotnet run --no-build -c $Configuration" -PassThru
        Write-Host "客户端启动成功! 进程ID: $($process.Id)" -ForegroundColor Green
        Pop-Location
        return $process
    }
    catch {
        Write-Host "启动客户端失败: $_" -ForegroundColor Red
        if (Test-Path variable:/oldLocation) { Pop-Location }
        return $null
    }
}

# 根据参数启动服务和客户端
$serviceProcess = $null
$clientProcess = $null

if ($ServiceOnly) {
    # 只启动服务
    $serviceProcess = Start-CodeIndexerService
    if ($null -eq $serviceProcess) { exit 1 }
}
elseif ($ClientOnly) {
    # 只启动客户端
    $clientProcess = Start-CodeIndexerClient
    if ($null -eq $clientProcess) { exit 1 }
}
else {
    # 启动服务和客户端
    $serviceProcess = Start-CodeIndexerService
    if ($null -eq $serviceProcess) { exit 1 }
    
    $clientProcess = Start-CodeIndexerClient
    if ($null -eq $clientProcess) { exit 1 }
}

Write-Host "`n所有组件已启动!" -ForegroundColor Green
Write-Host "提示: 每个组件都在单独的PowerShell窗口中运行，关闭窗口即可停止相应组件" -ForegroundColor Yellow
Write-Host "按Ctrl+C可以停止所有组件" -ForegroundColor Yellow

# 等待用户按Ctrl+C
try {
    Write-Host "`n按Ctrl+C停止所有组件..." -ForegroundColor Yellow
    while ($true) {
        Start-Sleep -Seconds 1
    }
}
finally {
    # 停止所有进程
    if ($null -ne $serviceProcess -and -not $serviceProcess.HasExited) {
        Write-Host "正在停止服务..." -ForegroundColor Yellow
        Stop-Process -Id $serviceProcess.Id -Force -ErrorAction SilentlyContinue
    }
    
    if ($null -ne $clientProcess -and -not $clientProcess.HasExited) {
        Write-Host "正在停止客户端..." -ForegroundColor Yellow
        Stop-Process -Id $clientProcess.Id -Force -ErrorAction SilentlyContinue
    }
    
    Write-Host "所有组件已停止!" -ForegroundColor Green
}