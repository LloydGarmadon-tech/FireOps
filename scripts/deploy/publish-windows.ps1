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

# Produktive Daten vor dem Neuaufbau des Deploy-Ordners sichern.
if (Test-Path $dataDir) {
    Write-Host "Vorhandene FireOps-Daten sichern..." -ForegroundColor Yellow
    if (Test-Path $backupDir) {
        Remove-Item $backupDir -Recurse -Force
    }
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

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish ist fehlgeschlagen."
}

New-Item -ItemType Directory -Path $dataDir -Force | Out-Null

if (Test-Path $backupDir) {
    Write-Host "Gesicherte FireOps-Daten wiederherstellen..." -ForegroundColor Yellow
    Copy-Item (Join-Path $backupDir "*") $dataDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $backupDir -Recurse -Force
}

$startScript = @'
$ErrorActionPreference = "Stop"
$baseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $baseDir

$env:ASPNETCORE_URLS = "http://0.0.0.0:5000"
$env:ASPNETCORE_ENVIRONMENT = "Production"

Write-Host "FireOps wird gestartet..." -ForegroundColor Cyan
Write-Host "Lokal:    http://localhost:5000"
Write-Host "Netzwerk: http://<IP-DIESES-PC>:5000"
Write-Host "Daten:    $baseDir\data\fireops.db"
Write-Host "Beenden mit Strg+C"
Write-Host ""

& "$baseDir\FireOps.exe"
'@
Set-Content -Path (Join-Path $out "Start-FireOps.ps1") -Value $startScript -Encoding UTF8

$firewallScript = @'
$ErrorActionPreference = "Stop"
$ruleName = "FireOps TCP 5000"

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Dieses Skript muss als Administrator ausgefuehrt werden."
}

$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Firewall-Regel ist bereits vorhanden: $ruleName" -ForegroundColor Green
    exit 0
}

New-NetFirewallRule `
    -DisplayName $ruleName `
    -Direction Inbound `
    -Action Allow `
    -Protocol TCP `
    -LocalPort 5000 `
    -Profile Private `
    -Description "Erlaubt FireOps-Zugriffe aus privaten lokalen Netzwerken."

Write-Host "Firewall-Regel fuer TCP 5000 wurde eingerichtet." -ForegroundColor Green
'@
Set-Content -Path (Join-Path $out "Setup-Firewall.ps1") -Value $firewallScript -Encoding UTF8

$autostartScript = @'
$ErrorActionPreference = "Stop"
$taskName = "FireOps"
$baseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $baseDir "FireOps.exe"

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Dieses Skript muss als Administrator ausgefuehrt werden."
}

$action = New-ScheduledTaskAction -Execute $exe -WorkingDirectory $baseDir
$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)

Register-ScheduledTask `
    -TaskName $taskName `
    -Action $action `
    -Trigger $trigger `
    -Principal $principal `
    -Settings $settings `
    -Description "Startet FireOps automatisch beim Windows-Start." `
    -Force | Out-Null

Write-Host "Autostart-Aufgabe '$taskName' wurde eingerichtet." -ForegroundColor Green
Write-Host "FireOps startet ab dem naechsten Windows-Neustart automatisch."
'@
Set-Content -Path (Join-Path $out "Setup-Autostart.ps1") -Value $autostartScript -Encoding UTF8

$removeAutostartScript = @'
$ErrorActionPreference = "Stop"
$taskName = "FireOps"

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Dieses Skript muss als Administrator ausgefuehrt werden."
}

if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    Write-Host "Autostart-Aufgabe '$taskName' wurde entfernt." -ForegroundColor Green
} else {
    Write-Host "Keine Autostart-Aufgabe '$taskName' vorhanden."
}
'@
Set-Content -Path (Join-Path $out "Remove-Autostart.ps1") -Value $removeAutostartScript -Encoding UTF8

$readme = @'
FireOps - Windows Deployment
============================

ERSTER TEST
-----------
Start-FireOps.ps1 ausfuehren.
Lokal: http://localhost:5000
Im Netzwerk: http://IP-DES-FIREOPS-PC:5000

DATEN
-----
Die Einsatzdaten liegen getrennt von den Programmdateien unter:

data\fireops.db

Bei Updates darf der Ordner data nicht geloescht werden.
Das Publish-Skript auf dem Entwicklungsrechner erhaelt vorhandene Daten im
lokalen deploy\FireOps\data-Verzeichnis automatisch.

FIREWALL
--------
Einmalig Setup-Firewall.ps1 in einer als Administrator gestarteten
PowerShell ausfuehren. Freigegeben wird nur TCP-Port 5000 im Windows-Profil
"Privat".

AUTOSTART
---------
Einmalig Setup-Autostart.ps1 als Administrator ausfuehren.
Danach startet FireOps beim Windows-Start als geplante Aufgabe unter SYSTEM.
Zum Entfernen Remove-Autostart.ps1 als Administrator ausfuehren.

IP-ADRESSE
----------
ipconfig

Die IPv4-Adresse des verwendeten Netzwerkadapters verwenden.
Beispiel: http://192.168.178.40:5000

UPDATE
------
Vor einem Update data\fireops.db zusaetzlich sichern.
Neue Programmdateien einspielen, aber den vorhandenen data-Ordner behalten.

VORAUSSETZUNGEN
---------------
Self-contained Windows x64. Auf dem Zielrechner sind weder .NET SDK noch
Visual Studio erforderlich.
'@
Set-Content -Path (Join-Path $out "README-START.txt") -Value $readme -Encoding UTF8

Write-Host ""
Write-Host "Publish erfolgreich." -ForegroundColor Green
Write-Host "Deployment: $out"
Write-Host "Datenverzeichnis: $dataDir"
Write-Host "Start: Start-FireOps.ps1"
Write-Host "Einmalige Einrichtung auf dem Ziel-PC: Setup-Firewall.ps1 und Setup-Autostart.ps1 (als Administrator)"
