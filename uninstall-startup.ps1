# uninstall-startup.ps1 - Stop DisplayDeck from launching at Windows startup.
#
#   powershell -ExecutionPolicy Bypass -File uninstall-startup.ps1

$ErrorActionPreference = 'Stop'
$AppName = 'DisplayDeck'

$lnk = Join-Path ([Environment]::GetFolderPath('Startup')) "$AppName.lnk"

if (Test-Path $lnk) {
    Remove-Item $lnk -Force
    Write-Host "$AppName will no longer start at sign-in (removed $lnk)." -ForegroundColor Green
}
else {
    Write-Host "$AppName wasn't set to start at sign-in. Nothing to remove." -ForegroundColor Yellow
}
