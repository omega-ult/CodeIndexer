# CodeIndexer Simple Startup Script

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$ServiceOnly,
    
    [Parameter()]
    [switch]$ClientOnly
)

$ErrorActionPreference = "Stop"
$workspaceRoot = $PSScriptRoot
$solutionPath = Join-Path $workspaceRoot "CodeIndexer.sln"
$servicePath = Join-Path $workspaceRoot "CodeIndexer.Service"
$clientPath = Join-Path $workspaceRoot "CodeIndexer.Client"

# Display title
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "      CodeIndexer Startup Script        " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Build solution
Write-Host "Building solution..." -ForegroundColor Yellow
try {
    Push-Location $workspaceRoot
    dotnet restore $solutionPath
    dotnet build $solutionPath -c Release
    if ($LASTEXITCODE -ne 0) { 
        throw "Build failed with exit code: $LASTEXITCODE" 
    }
    Write-Host "Build successful!" -ForegroundColor Green
    Pop-Location
}
catch {
    Write-Host "Build failed: $_" -ForegroundColor Red
    if (Test-Path variable:/oldLocation) { Pop-Location }
    exit 1
}

# Start service
function Start-CodeIndexerService {
    Write-Host "Starting CodeIndexer service..." -ForegroundColor Yellow
    try {
        Push-Location $servicePath
        Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run --no-build -c Release"
        Write-Host "Service started successfully!" -ForegroundColor Green
        Write-Host "API URL: http://localhost:5000" -ForegroundColor Cyan
        Write-Host "Swagger docs: http://localhost:5000/swagger" -ForegroundColor Cyan
        Pop-Location
        # Wait for service to start
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

# Start client
function Start-CodeIndexerClient {
    Write-Host "Starting CodeIndexer client..." -ForegroundColor Yellow
    try {
        Push-Location $clientPath
        Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run --no-build -c Release"
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

# Start components based on parameters
if ($ServiceOnly) {
    # Start service only
    $serviceStarted = Start-CodeIndexerService
    if (-not $serviceStarted) { exit 1 }
}
elseif ($ClientOnly) {
    # Start client only
    $clientStarted = Start-CodeIndexerClient
    if (-not $clientStarted) { exit 1 }
}
else {
    # Start both service and client
    $serviceStarted = Start-CodeIndexerService
    if (-not $serviceStarted) { exit 1 }
    
    $clientStarted = Start-CodeIndexerClient
    if (-not $clientStarted) { exit 1 }
}

Write-Host ""
Write-Host "All components started!" -ForegroundColor Green
Write-Host "Note: Each component runs in its own PowerShell window. Close the window to stop the component." -ForegroundColor Yellow