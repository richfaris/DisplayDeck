# shutdown.ps1 — Stop DisplayDeck if it's running.
#
#   Right-click > Run with PowerShell, or:  powershell -ExecutionPolicy Bypass -File shutdown.ps1

$ErrorActionPreference = 'Stop'
$AppName = 'DisplayDeck'

$procs = Get-Process -Name $AppName -ErrorAction SilentlyContinue
if (-not $procs) {
    Write-Host "$AppName isn't running." -ForegroundColor Yellow
    return
}

$procs | Stop-Process -Force
Write-Host "Stopped $AppName (was PID $($procs.Id -join ', '))." -ForegroundColor Green
