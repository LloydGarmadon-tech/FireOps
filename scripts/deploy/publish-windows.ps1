$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = (Resolve-Path (Join-Path $scriptDir "..\..")).Path
$project = Join-Path $root "FireOps.csproj"
$out = Join-Path $root "deploy\FireOps"
$dataDir = Join-Path $out "data"
$backupDir = Join-Path $root "deploy\_data-backup"

Write-Host "=== FireOps Windows Publish ===" -ForegroundColor Cyan
Write-Host "Projekt: $project"
Write-Host "Ausgabe: $out"

if (Test-Path $dataDir) {
    Write-Host "Vorhandene FireOps-Daten sichern..." -ForegroundColor Yellow
    if (Test-Path $backupDir) { Remove-Item $backupDir -Recurse -Force }
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    Copy-Item (Join-Path $dataDir "*") $backupDir -Recurse -Force -ErrorAction SilentlyContinue
}

if (Test-Path $out) {
    Write-Host "Alte Programmdateien entfernen..."
    Remove-Item $out -Recurse -Force
}
New-Item -ItemType Directory -Path $out -Force | Out-Null

Write-Host "FireOps fuer Windows x64 veroeffentlichen..." -ForegroundColor Yellow

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $out `
    /p:PublishSingleFile=false `
    /p:DebugType=None `
    /p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) { throw "dotnet publish ist fehlgeschlagen." }

New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
if (Test-Path $backupDir) {
    Write-Host "Gesicherte FireOps-Daten wiederherstellen..." -ForegroundColor Yellow
    Copy-Item (Join-Path $backupDir "*") $dataDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $backupDir -Recurse -Force
}

# Einfacher Start fuer den Wachen-PC: Doppelklick, Server startet, Browser oeffnet sich automatisch.
$stationLauncher = @'
@echo off
setlocal
cd /d "%~dp0"
title FireOps

set "ASPNETCORE_URLS=http://127.0.0.1:5000"
set "ASPNETCORE_ENVIRONMENT=Production"

echo ========================================
echo              FireOps
echo ========================================
echo.
echo FireOps wird gestartet.
echo Dieses Fenster waehrend der Nutzung offen lassen.
echo Zum Beenden dieses Fenster schliessen.
echo.

start "" powershell.exe -NoProfile -WindowStyle Hidden -Command "Start-Sleep -Seconds 2; Start-Process 'http://localhost:5000'"
"%~dp0FireOps.exe"

if errorlevel 1 (
    echo.
    echo FireOps wurde mit einem Fehler beendet.
    pause
)
endlocal
'@
Set-Content -Path (Join-Path $out "FireOps starten.cmd") -Value $stationLauncher -Encoding ASCII

# Technischer manueller Start bleibt fuer Entwicklung/Fehlersuche erhalten.
$startScript = @'
$ErrorActionPreference = "Stop"
$baseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $baseDir
$env:ASPNETCORE_URLS = "http://127.0.0.1:5000"
$env:ASPNETCORE_ENVIRONMENT = "Production"
Write-Host "FireOps wird lokal gestartet..." -ForegroundColor Cyan
Write-Host "Adresse: http://localhost:5000"
Write-Host "Daten:   $baseDir\data\fireops.db"
Write-Host "Beenden mit Strg+C"
& "$baseDir\FireOps.exe"
'@
Set-Content -Path (Join-Path $out "Start-FireOps.ps1") -Value $startScript -Encoding UTF8

$firewallScript = @'
$ErrorActionPreference = "Stop"
$ruleName = "FireOps TCP 5000"
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw "Dieses Skript muss als Administrator ausgefuehrt werden." }
$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if ($existing) { Write-Host "Firewall-Regel ist bereits vorhanden: $ruleName" -ForegroundColor Green; exit 0 }
New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5000 -Profile Private -Description "Erlaubt FireOps-Zugriffe aus privaten lokalen Netzwerken."
Write-Host "Firewall-Regel fuer TCP 5000 wurde eingerichtet." -ForegroundColor Green
'@
Set-Content -Path (Join-Path $out "Setup-Firewall.ps1") -Value $firewallScript -Encoding UTF8

$autostartScript = @'
$ErrorActionPreference = "Stop"
$taskName = "FireOps"
$baseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $baseDir "FireOps.exe"
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw "Dieses Skript muss als Administrator ausgefuehrt werden." }
$action = New-ScheduledTaskAction -Execute $exe -WorkingDirectory $baseDir
$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description "Startet FireOps automatisch beim Windows-Start." -Force | Out-Null
Write-Host "Autostart-Aufgabe '$taskName' wurde eingerichtet." -ForegroundColor Green
'@
Set-Content -Path (Join-Path $out "Setup-Autostart.ps1") -Value $autostartScript -Encoding UTF8

$removeAutostartScript = @'
$ErrorActionPreference = "Stop"
$taskName = "FireOps"
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw "Dieses Skript muss als Administrator ausgefuehrt werden." }
if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) { Unregister-ScheduledTask -TaskName $taskName -Confirm:$false; Write-Host "Autostart-Aufgabe '$taskName' wurde entfernt." -ForegroundColor Green } else { Write-Host "Keine Autostart-Aufgabe '$taskName' vorhanden." }
'@
Set-Content -Path (Join-Path $out "Remove-Autostart.ps1") -Value $removeAutostartScript -Encoding UTF8

$readme = @'
FireOps - Version fuer den Wachen-PC
====================================

STARTEN
-------
Doppelklick auf:

    FireOps starten.cmd

FireOps startet und der Standardbrowser oeffnet automatisch die Anwendung.
Das schwarze FireOps-Fenster waehrend der Nutzung geoeffnet lassen.

BEENDEN
-------
Das schwarze FireOps-Fenster schliessen.

DATEN
-----
Alle Einsatzdaten liegen unter:

    data\fireops.db

Den Ordner data bei Updates nicht loeschen. Vor Updates sollte fireops.db
gesichert werden.

INSTALLATION
------------
Der komplette Ordner FireOps kann auf den Windows-PC in der Wache kopiert
werden. Es sind weder Visual Studio noch das .NET SDK erforderlich.

NETZWERK / AUTOSTART
--------------------
Die Skripte Setup-Firewall.ps1 und Setup-Autostart.ps1 sind optionale
Administrationsfunktionen. Fuer den einfachen lokalen Betrieb auf einem
Wachen-PC werden sie nicht benoetigt.
'@
Set-Content -Path (Join-Path $out "README-START.txt") -Value $readme -Encoding UTF8

Write-Host ""
Write-Host "Publish erfolgreich." -ForegroundColor Green
Write-Host "Deployment: $out"
Write-Host "Wachen-Start: Doppelklick auf 'FireOps starten.cmd'"
Write-Host "Datenverzeichnis: $dataDir"
