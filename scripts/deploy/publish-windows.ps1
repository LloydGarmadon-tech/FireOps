$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = (Resolve-Path (Join-Path $scriptDir "..\..")).Path
$project = Join-Path $root "FireOps.csproj"
$out = Join-Path $root "deploy\FireOps"

Write-Host "=== FireOps Windows Publish ===" -ForegroundColor Cyan
Write-Host "Projekt: $project"
Write-Host "Ausgabe: $out"

if (Test-Path $out) {
    Write-Host "Alten Deploy-Ordner entfernen..."
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

$startScript = @'
$ErrorActionPreference = "Stop"
$baseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $baseDir

$env:ASPNETCORE_URLS = "http://0.0.0.0:5000"
$env:ASPNETCORE_ENVIRONMENT = "Production"

Write-Host "FireOps wird gestartet..." -ForegroundColor Cyan
Write-Host "Lokal:    http://localhost:5000"
Write-Host "Netzwerk: http://<IP-DIESES-PC>:5000"
Write-Host "Beenden mit Strg+C"
Write-Host ""

& "$baseDir\FireOps.exe"
'@

Set-Content -Path (Join-Path $out "Start-FireOps.ps1") -Value $startScript -Encoding UTF8

$readme = @'
FireOps - Windows Deployment
============================

START
-----
1. Start-FireOps.ps1 mit PowerShell starten.
2. Auf demselben PC im Browser oeffnen:
   http://localhost:5000
3. Von einem anderen Geraet im gleichen Netzwerk:
   http://IP-DES-FIREOPS-PC:5000

IP-ADRESSE ERMITTELN
--------------------
PowerShell oder Eingabeaufforderung oeffnen und ausfuehren:

ipconfig

Die IPv4-Adresse des verwendeten Netzwerkadapters verwenden.
Beispiel: http://192.168.178.40:5000

WINDOWS-FIREWALL
----------------
Wenn andere Geraete FireOps nicht erreichen koennen, muss auf dem
FireOps-PC TCP-Port 5000 fuer das lokale Netzwerk freigegeben werden.

DATEN
-----
Die SQLite-Datenbank fireops.db wird im Arbeitsverzeichnis der Anwendung
angelegt. Diesen Ordner deshalb nicht bei jedem Update ungeprueft loeschen.
Vor einem Update die Datenbank sichern.

VORAUSSETZUNGEN
---------------
Dieses Deployment ist self-contained fuer Windows x64. Auf dem Zielrechner
muss deshalb kein .NET SDK und keine Entwicklungsumgebung installiert sein.
'@

Set-Content -Path (Join-Path $out "README-START.txt") -Value $readme -Encoding UTF8

Write-Host "" 
Write-Host "Publish erfolgreich." -ForegroundColor Green
Write-Host "Deployment: $out"
Write-Host "Start auf dem Ziel-PC: Start-FireOps.ps1"
