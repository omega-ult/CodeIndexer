# CodeIndexer Test Script
# 此脚本用于运行CodeIndexer的测试项目

[CmdletBinding()]
param(
    [Parameter()]
    [string]$TestProject = "",
    
    [Parameter()]
    [string]$Filter = "",
    
    [Parameter()]
    [switch]$NoBuild,
    
    [Parameter()]
    [switch]$Detailed,
    
    [Parameter()]
    [string]$Configuration = "Debug",
    
    [Parameter()]
    [string]$Framework = "",
    
    [Parameter()]
    [int]$Parallel = 0,
    
    [Parameter()]
    [switch]$CollectCoverage
)

$ErrorActionPreference = "Stop"
$workspaceRoot = $PSScriptRoot
$solutionPath = Join-Path $workspaceRoot "CodeIndexer.sln"
$testProjectsRoot = Join-Path $workspaceRoot "CodeIndexer.Tests"

# 显示标题
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "      CodeIndexer Test Script           " -ForegroundColor Cyan
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

# 构建测试命令
function Build-TestCommand {
    $testCommand = "dotnet test"
    
    # 如果指定了特定的测试项目
    if ($TestProject) {
        $projectPath = ""
        if (Test-Path $TestProject) {
            # 如果提供的是完整路径
            $projectPath = $TestProject
        } elseif (Test-Path (Join-Path $testProjectsRoot $TestProject)) {
            # 如果提供的是相对于测试根目录的路径
            $projectPath = Join-Path $testProjectsRoot $TestProject
        } elseif (Test-Path (Join-Path $testProjectsRoot "$TestProject.csproj")) {
            # 如果提供的是项目名称（不带.csproj扩展名）
            $projectPath = Join-Path $testProjectsRoot "$TestProject.csproj"
        } else {
            Write-Host "Error: Could not find test project '$TestProject'" -ForegroundColor Red
            exit 1
        }
        $testCommand += " `"$projectPath`""
    } else {
        # 否则运行解决方案中的所有测试
        $testCommand += " `"$solutionPath`""
    }
    
    # 添加配置
    $testCommand += " -c $Configuration"
    
    # 如果不需要构建
    if ($NoBuild) {
        $testCommand += " --no-build"
    }
    
    # 如果指定了测试过滤器
    if ($Filter) {
        $testCommand += " --filter `"$Filter`""
    }
    
    # 如果指定了框架
    if ($Framework) {
        $testCommand += " -f $Framework"
    }
    
    # 如果指定了并行度
    if ($Parallel -gt 0) {
        $testCommand += " -p:ParallelizeTestCollections=true -p:MaxParallelThreads=$Parallel"
    }
    
    # 如果需要详细输出
    if ($Detailed) {
        $testCommand += " -v detailed"
    }
    
    # 如果需要收集代码覆盖率
    if ($CollectCoverage) {
        $testCommand += " /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=./TestResults/coverage/"
    }
    
    return $testCommand
}

# 运行测试
Write-Host "Running tests..." -ForegroundColor Yellow
try {
    Push-Location $workspaceRoot
    
    $testCommand = Build-TestCommand
    Write-Host "Executing: $testCommand" -ForegroundColor DarkGray
    
    Invoke-Expression $testCommand
    
    if ($LASTEXITCODE -ne 0) { 
        Write-Host "Tests failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    
    Write-Host "Tests completed successfully!" -ForegroundColor Green
    Pop-Location
}
catch {
    Write-Host "Failed to run tests: $_" -ForegroundColor Red
    if (Test-Path variable:/oldLocation) { Pop-Location }
    exit 1
}

Write-Host ""
Write-Host "Test process completed!" -ForegroundColor Green