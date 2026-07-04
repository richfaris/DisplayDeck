# install-startup.ps1 - Run DisplayDeck automatically when Windows starts.
#
# Creates a shortcut in your per-user Startup folder that launches DisplayDeck
# straight into the system tray (--tray) on every sign-in. No admin required.
#
#   powershell -ExecutionPolicy Bypass -File install-startup.ps1
#
# To undo: run uninstall-startup.ps1.

$ErrorActionPreference = 'Stop'
$AppName = 'DisplayDeck'
$root = $PSScriptRoot

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

$startup = [Environment]::GetFolderPath('Startup')
$lnk = Join-Path $startup "$AppName.lnk"

$shell = New-Object -ComObject WScript.Shell
$sc = $shell.CreateShortcut($lnk)
$sc.TargetPath = $exe
$sc.Arguments = '--tray'
$sc.WorkingDirectory = Split-Path $exe
$sc.IconLocation = $exe
$sc.Description = "$AppName - starts in the system tray at sign-in"
$sc.Save()

Write-Host "$AppName will now start automatically at sign-in." -ForegroundColor Green
Write-Host "  Shortcut : $lnk" -ForegroundColor DarkGray
Write-Host "  Target   : $exe --tray" -ForegroundColor DarkGray
Write-Host "Undo any time with uninstall-startup.ps1." -ForegroundColor DarkGray
