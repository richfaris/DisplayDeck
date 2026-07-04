# startup.ps1 — Start DisplayDeck if it isn't already running.
#
#   Right-click > Run with PowerShell, or:  powershell -ExecutionPolicy Bypass -File startup.ps1
#
# It checks whether the app is already running first, then launches the most
# recently built executable it can find (publish, then Release, then Debug).

$ErrorActionPreference = 'Stop'
$AppName = 'DisplayDeck'
$root = $PSScriptRoot

$existing = Get-Process -Name $AppName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "$AppName is already running (PID $($existing.Id -join ', ')). Nothing to do." -ForegroundColor Yellow
    return
}

$candidates = @(
    "publish\$AppName.exe",
    "src\$AppName.App\bin\Release\net10.0-windows\$AppName.exe",
    "src\$AppName.App\bin\Debug\net10.0-windows\$AppName.exe"
) | ForEach-Object { Join-Path $root $_ } | Where-Object { Test-Path $_ }

if (-not $candidates) {
    Write-Host "Couldn't find a built $AppName.exe. Build it first:" -ForegroundColor Red
    Write-Host "  dotnet build src\$AppName.App\$AppName.App.csproj -c Debug"
    exit 1
}

$exe = $candidates | Sort-Object { (Get-Item $_).LastWriteTime } -Descending | Select-Object -First 1
Write-Host "Starting $AppName ..." -ForegroundColor Green
Write-Host "  $exe" -ForegroundColor DarkGray
Start-Process -FilePath $exe
Write-Host "$AppName started. Press Ctrl+Alt+D or click the tray icon to open it." -ForegroundColor Green
