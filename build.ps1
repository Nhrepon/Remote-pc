$ErrorActionPreference = "Stop"

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "       Building RemotePC - Remote Desktop App" -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path $csc)) {
    Write-Host "[ERROR] C# compiler (csc.exe) not found!" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path "bin\Release")) {
    New-Item -ItemType Directory -Path "bin\Release" -Force | Out-Null
}

Write-Host "Compiling C# source code..." -ForegroundColor Yellow

$sources = @(
    "src\Common\*.cs",
    "src\Server\*.cs",
    "src\Client\*.cs",
    "src\UI\*.cs",
    "src\Program.cs"
)

$args = @(
    "/target:winexe",
    "/platform:anycpu",
    "/optimize+",
    "/win32manifest:app.manifest",
    "/win32icon:assets\app.ico",
    "/out:bin\Release\RemotePC.exe",
    "/resource:assets\app.ico,RemotePC.AppIcon.ico",
    "/resource:assets\app-logo.png,RemotePC.AppLogo.png",
    "/r:System.dll,System.Drawing.dll,System.Windows.Forms.dll"
) + $sources

& $csc $args

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "====================================================" -ForegroundColor Green
    Write-Host " BUILD SUCCESSFUL!" -ForegroundColor Green
    Write-Host " Executable created at: $PSScriptRoot\bin\Release\RemotePC.exe" -ForegroundColor Green
    Write-Host "====================================================" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Compilation failed with exit code $LASTEXITCODE" -ForegroundColor Red
}
