# FireOps – Atemschutzüberwachung MVP

FireOps ist ein lokaler MVP zur digitalen Unterstützung der Atemschutzüberwachung. Die Anwendung visualisiert und dokumentiert Atemschutztrupps, Druckmeldungen, Einsatzzeiten, Rückzug, Atemnotfälle und die Übergabe an eine zentrale Atemschutzüberwachung.

> **Hinweis:** FireOps ist eine unterstützende Visualisierungs- und Dokumentationslösung. Funk/DMO, geltende Einsatzgrundsätze und die Sicherheitseinrichtungen der Atemschutzgeräte bleiben maßgeblich.

## Funktionen

- ASP.NET Core / Blazor Web App auf .NET 10
- EF Core mit lokaler SQLite-Datenbank
- Atemschutztrupps mit Truppführer und Truppmann
- Rollen einschließlich Sicherheitstrupp
- touch-optimierte Druckerfassung
- individuelle Verbrauchs- und Umkehrdruckberechnung je Geräteträger
- Frühwarnung anhand Zeit und bisherigem Luftverbrauch
- Rückzugswarnung nach dem schwächsten Truppmitglied
- kritische Warnstufe bei 50 bar
- Dokumentation von Einsatzziel, Rückzug, Rückkehr und Abschluss
- Atemnotfall mit betroffener Person und Zeitstempel
- Notfalltasche/Reserveflasche beim Sicherheitstrupp
- Ereignis- und Druckhistorie
- Gruppenführer-Lagebild
- zentrale Atemschutzüberwachung
- Übergabe an zentrale ASÜ

## Projekt starten

Voraussetzung: **.NET 10 SDK**.

```powershell
dotnet restore
dotnet run
```

Danach die unter `Now listening on:` ausgegebene Adresse im Browser öffnen.

Zum Zurücksetzen der Demodaten die Anwendung beenden und die lokale Datei `fireops.db` löschen. Beim nächsten Start wird sie neu erzeugt.

## Projektstruktur

```text
Components/   Blazor-Komponenten und Seiten
Data/         EF-Core-Kontext und Initialisierung
Domain/       Entitäten und Enums
Services/     Anwendungs- und Fachlogik
ViewModels/   UI-Modelle
docs/         Entwicklerdokumentation
wwwroot/      CSS und statische Inhalte
```

Die ausführliche technische und fachliche Beschreibung befindet sich in `docs/DEVELOPER.md`.

## Noch nicht Bestandteil des MVP

- echter Offline-Sync zwischen mehreren Geräten
- PWA-/IndexedDB-Outbox
- Benutzer-/PIN-Anmeldung
- automatische Atemschutzgeräte-Telemetrie
- Alamos-Anbindung
- Internet-/Cloud-Server

## NuGet

Das Projekt enthält eine lokale `NuGet.Config`, die für diesen MVP `nuget.org` verwendet. Falls lokale Restore-Daten Probleme verursachen:

```powershell
dotnet nuget locals all --clear
dotnet restore
```
