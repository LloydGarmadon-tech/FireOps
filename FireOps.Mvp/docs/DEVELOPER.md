# FireOps – Entwicklerdokumentation

## 1. Ziel des Projekts

FireOps ist ein lokaler MVP zur digitalen Unterstützung der Atemschutzüberwachung. Die Anwendung visualisiert und dokumentiert Trupps, Druckmeldungen, Einsatzzeiten, Rückzug, Atemnotfälle und die Übergabe an eine zentrale Atemschutzüberwachung.

**Wichtig:** Die Anwendung ist eine unterstützende Dokumentations- und Visualisierungslösung. Funk/DMO, die vorgeschriebenen organisatorischen Verfahren und die Sicherheitseinrichtungen der Atemschutzgeräte bleiben maßgeblich.

## 2. Technischer Stack

- .NET 10 / ASP.NET Core
- Blazor Web App
- Entity Framework Core 10
- SQLite
- Razor Components
- CSS ohne zusätzliches UI-Framework

Die SQLite-Datei `fireops.db` wird lokal erzeugt und gehört nicht in Git.

## 3. Projektstruktur

```text
FireOps.Mvp/
├── Components/
│   ├── Layout/          # Gemeinsames Seitenlayout
│   ├── Pages/           # Arbeits- und Lageansichten
│   ├── App.razor
│   ├── Routes.razor
│   └── TouchPressureInput.razor
├── Data/
│   ├── DbInitializer.cs # Initialisierung und Demodaten
│   └── FireOpsDbContext.cs
├── Domain/
│   ├── Entities.cs      # Persistierte Fachobjekte
│   └── Enums.cs         # Fachliche Status/Typen
├── Services/
│   ├── ChangeNotifier.cs
│   └── FireOpsService.cs # Anwendungs- und Fachlogik
├── ViewModels/
│   └── Models.cs        # Modelle für die UI
├── docs/
│   └── DEVELOPER.md
├── wwwroot/
│   └── app.css
├── Program.cs
├── appsettings.json
└── FireOps.Mvp.csproj
```

Die Struktur ist bewusst klein gehalten. Neue Fachlogik sollte nicht direkt in Razor-Komponenten wachsen. Bei zunehmendem Umfang sollten Berechnungen wie Luftverbrauch und Warnstufen in eigene Domain-Services ausgelagert und mit Unit-Tests abgesichert werden.

## 4. Wichtige Seiten

- `Home.razor`: Einstieg und Einsatzübersicht
- `IncidentDetail.razor`: zentrale Arbeitsansicht eines Einsatzes
- `TeamDetail.razor`: Detailansicht eines Atemschutztrupps, Druckmeldungen und taktische Aktionen
- `Commander.razor`: Lagebild für Gruppenführer/Führung
- `CentralMonitoring.razor`: zentrale Atemschutzüberwachung

## 5. Datenmodell und Dokumentation von Ereignissen

Der aktuelle Zustand eines Atemschutztrupps wird relational gespeichert. Zusätzlich werden fachlich wichtige Vorgänge als `IncidentEvent` protokolliert. Druckmeldungen werden als eigene Datensätze gespeichert.

Das Ereignisprotokoll ist wichtig, weil nicht nur der aktuelle Zustand, sondern auch der zeitliche Ablauf nachvollziehbar bleiben soll. Dazu gehören beispielsweise:

- Start des Atemschutzeinsatzes
- Erreichen des Einsatzziels
- Beginn des Rückzugs
- Atemnotfall
- Rückkehr des Trupps
- Änderungen taktischer Angaben
- Druckkontrollen der Reserveflasche des Sicherheitstrupps

Bei neuen Funktionen sollte geprüft werden, ob nur ein aktueller Zustand benötigt wird oder ob zusätzlich ein historisches Ereignis geschrieben werden muss.

## 6. Druck- und Rückzugslogik

Die Berechnung erfolgt **für jeden Geräteträger einzeln**. Der schwächste Geräteträger bestimmt die Entscheidung für den gesamten Trupp.

### 6.1 Umkehrdruck am Einsatzziel

Beim Erreichen des Einsatzziels wird der aktuelle Druck jedes Geräteträgers erfasst.

```text
Hinwegverbrauch = Startdruck - Druck am Einsatzziel
Umkehrdruck     = (Hinwegverbrauch × 2) + Reserve
```

Die Reserve beträgt im MVP standardmäßig 50 bar.

Beispiel:

```text
Startdruck:             300 bar
Druck am Einsatzziel:   220 bar
Hinwegverbrauch:         80 bar
Rückwegberechnung:      160 bar
Reserve:                 50 bar
Umkehrdruck:            210 bar
```

Bei 220 bar darf der Geräteträger noch am Einsatzziel arbeiten. Sobald eine tatsächliche Druckmeldung 210 bar oder weniger ergibt, ist der Umkehrdruck erreicht. Der gesamte Trupp muss den Rückzug antreten.

### 6.2 Vor dem Einsatzziel

Solange das Einsatzziel noch nicht bestätigt wurde, wird aus dem bisherigen Verbrauch ein vorläufiger Umkehrdruck berechnet. Dadurch kann ein hoher Luftverbrauch bereits während des Vorgehens erkannt werden, obwohl die Anwendung die tatsächlich zurückgelegte Wegstrecke nicht kennt.

### 6.3 Verbrauchsprognose

Aus Startzeit, Startdruck und letzter Druckmeldung wird je Geräteträger ein durchschnittlicher Verbrauch in `bar/min` berechnet. Daraus wird abgeschätzt, wann der maßgebliche Umkehrdruck bei gleichbleibendem Verbrauch erreicht wird.

Die Prognose ist eine **Frühwarnung**, kein Ersatz für eine aktuelle Druckmeldung.

### 6.4 Warnstufen

1. **Frühwarnung:** prognostizierter Umkehrdruck in höchstens 5 Minuten – aktuelle Druckmeldung anfordern.
2. **Rückzug:** mindestens ein Geräteträger hat seinen individuellen Umkehrdruck tatsächlich erreicht oder unterschritten – gesamter Trupp zurück.
3. **Kritisch:** 50 bar oder weniger – höchste Druckwarnstufe.

Die Entscheidung wird nie aus einem gemittelten Truppdruck abgeleitet.

## 7. Atemnotfall

Ein Atemnotfall ist kein bloßer Dokumentationseintrag, sondern ein aktiver Zustand des Trupps.

Beim Auslösen werden gespeichert:

- betroffener Geräteträger
- Trupp
- exakter Zeitpunkt
- optionale Bemerkung

Der Trupp erhält den Status `Emergency` und muss in den Lageansichten deutlich priorisiert werden. Ein eingeleiteter Rückzug beendet den Notfallstatus nicht. Erst die Rückmeldung des Trupps beendet den aktiven Notfall; das ursprüngliche Ereignis bleibt im Ereignisprotokoll erhalten.

## 8. Sicherheitstrupp und Notfalltasche

Für einen Sicherheitstrupp wird zusätzlich der Druck der Reserveflasche in der Notfalltasche dokumentiert. Beim Anlegen eines Sicherheitstrupps ist ein Startdruck erforderlich.

Spätere Kontrollen werden als `SafetyBagPressureRecorded` mit Zeitpunkt, Druck und optionaler Bemerkung protokolliert. Der Druck der Reserveflasche ist bewusst unabhängig von den Flaschendrücken der Geräteträger.

## 9. UI-Grundsätze

Die Oberfläche ist für Touch-Bedienung im Einsatz gedacht.

- große Schaltflächen und Druck-Schnellwerte
- keine kleinen Browser-Spinner für Druckwerte
- möglichst keine verschachtelten oder modalen Scrollbalken
- Dialoge nutzen verfügbare Bildschirmbreite
- sicherheitsrelevante Zustände müssen ohne Detailnavigation erkennbar sein
- Atemnotfälle werden in Lageansichten priorisiert
- beendete Trupps bleiben für Nachdokumentation erreichbar

## 10. Lokaler Start

Voraussetzung ist das .NET 10 SDK.

```powershell
dotnet restore
dotnet run
```

Anschließend die von `Now listening on:` ausgegebene Adresse öffnen. Die konkrete Portnummer kann durch das lokale Launch-Profil abweichen.

### Demo-Daten zurücksetzen

Anwendung beenden und `fireops.db` löschen. Beim nächsten Start wird die lokale Datenbank neu angelegt.

## 11. Vor einem Commit

Mindestens folgende Prüfungen durchführen:

```powershell
dotnet restore
dotnet build
```

Danach die wichtigsten Abläufe manuell testen:

1. Trupp anlegen und Atemschutzeinsatz starten.
2. Druckmeldungen für beide Personen erfassen.
3. Einsatzziel bestätigen und individuellen Umkehrdruck prüfen.
4. Prüfen, dass der zuerst erreichte Grenzwert den Rückzug des gesamten Trupps auslöst.
5. 50-bar-Warnung prüfen.
6. Atemnotfall für eine konkrete Person auslösen und Lageansichten prüfen.
7. Sicherheitstrupp mit Reserveflasche anlegen und weiteren Flaschendruck dokumentieren.
8. Rückzug, Rückkehr und Abschluss prüfen.

## 12. Git-Konventionen

Nicht versionieren:

- `bin/`, `obj/`, `artifacts/`
- `.vs/` und benutzerspezifische IDE-Dateien
- lokale SQLite-Datenbanken
- Logs und temporäre Dateien

Commit-Nachrichten sollten beschreiben, **warum bzw. welche fachliche Änderung** enthalten ist, z. B.:

```text
Add individual turnaround pressure warnings
Add emergency firefighter tracking
Document safety bag reserve pressure
```

## 13. Sinnvolle nächste technische Schritte

- Unit-Tests für Umkehrdruck, Verbrauchsprognose und Warnstufen
- Fachberechnungen aus `FireOpsService` in eigene Services/Domain-Klassen extrahieren
- EF-Core-Migrationsstrategie statt ausschließlich Initialisierung
- Authentifizierung/PIN und Rollenmodell
- Offline-/Mehrgeräte-Synchronisation
- PWA/Outbox-Konzept
- strukturierte Protokollierung und Fehlerbehandlung
- Alamos-/Einsatzschnittstelle erst nach Stabilisierung des fachlichen Modells
