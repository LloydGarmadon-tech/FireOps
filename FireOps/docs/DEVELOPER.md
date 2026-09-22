# FireOps â€“ Entwicklerdokumentation

## 1. Ziel des Projekts

FireOps ist ein lokaler MVP zur digitalen UnterstÃ¼tzung der AtemschutzÃ¼berwachung. Die Anwendung visualisiert und dokumentiert Trupps, Druckmeldungen, Einsatzzeiten, RÃ¼ckzug, AtemnotfÃ¤lle und die Ãœbergabe an eine zentrale AtemschutzÃ¼berwachung.

**Wichtig:** Die Anwendung ist eine unterstÃ¼tzende Dokumentations- und VisualisierungslÃ¶sung. Funk/DMO, die vorgeschriebenen organisatorischen Verfahren und die Sicherheitseinrichtungen der AtemschutzgerÃ¤te bleiben maÃŸgeblich.

## 2. Technischer Stack

- .NET 10 / ASP.NET Core
- Blazor Web App
- Entity Framework Core 10
- SQLite
- Razor Components
- CSS ohne zusÃ¤tzliches UI-Framework

Die SQLite-Datei `fireops.db` wird lokal erzeugt und gehÃ¶rt nicht in Git.

## 3. Projektstruktur

```text
FireOps/
â”œâ”€â”€ Components/
â”‚   â”œâ”€â”€ Layout/          # Gemeinsames Seitenlayout
â”‚   â”œâ”€â”€ Pages/           # Arbeits- und Lageansichten
â”‚   â”œâ”€â”€ App.razor
â”‚   â”œâ”€â”€ Routes.razor
â”‚   â””â”€â”€ TouchPressureInput.razor
â”œâ”€â”€ Data/
â”‚   â”œâ”€â”€ DbInitializer.cs # Initialisierung und Demodaten
â”‚   â””â”€â”€ FireOpsDbContext.cs
â”œâ”€â”€ Domain/
â”‚   â”œâ”€â”€ Entities.cs      # Persistierte Fachobjekte
â”‚   â””â”€â”€ Enums.cs         # Fachliche Status/Typen
â”œâ”€â”€ Services/
â”‚   â”œâ”€â”€ ChangeNotifier.cs
â”‚   â””â”€â”€ FireOpsService.cs # Anwendungs- und Fachlogik
â”œâ”€â”€ ViewModels/
â”‚   â””â”€â”€ Models.cs        # Modelle fÃ¼r die UI
â”œâ”€â”€ docs/
â”‚   â””â”€â”€ DEVELOPER.md
â”œâ”€â”€ wwwroot/
â”‚   â””â”€â”€ app.css
â”œâ”€â”€ Program.cs
â”œâ”€â”€ appsettings.json
â””â”€â”€ FireOps.csproj
```

Die Struktur ist bewusst klein gehalten. Neue Fachlogik sollte nicht direkt in Razor-Komponenten wachsen. Bei zunehmendem Umfang sollten Berechnungen wie Luftverbrauch und Warnstufen in eigene Domain-Services ausgelagert und mit Unit-Tests abgesichert werden.

## 4. Wichtige Seiten

- `Home.razor`: Einstieg und EinsatzÃ¼bersicht
- `IncidentDetail.razor`: zentrale Arbeitsansicht eines Einsatzes
- `TeamDetail.razor`: Detailansicht eines Atemschutztrupps, Druckmeldungen und taktische Aktionen
- `Commander.razor`: Lagebild fÃ¼r GruppenfÃ¼hrer/FÃ¼hrung
- `CentralMonitoring.razor`: zentrale AtemschutzÃ¼berwachung

## 5. Datenmodell und Dokumentation von Ereignissen

Der aktuelle Zustand eines Atemschutztrupps wird relational gespeichert. ZusÃ¤tzlich werden fachlich wichtige VorgÃ¤nge als `IncidentEvent` protokolliert. Druckmeldungen werden als eigene DatensÃ¤tze gespeichert.

Das Ereignisprotokoll ist wichtig, weil nicht nur der aktuelle Zustand, sondern auch der zeitliche Ablauf nachvollziehbar bleiben soll. Dazu gehÃ¶ren beispielsweise:

- Start des Atemschutzeinsatzes
- Erreichen des Einsatzziels
- Beginn des RÃ¼ckzugs
- Atemnotfall
- RÃ¼ckkehr des Trupps
- Ã„nderungen taktischer Angaben
- Druckkontrollen der Reserveflasche des Sicherheitstrupps

Bei neuen Funktionen sollte geprÃ¼ft werden, ob nur ein aktueller Zustand benÃ¶tigt wird oder ob zusÃ¤tzlich ein historisches Ereignis geschrieben werden muss.

## 6. Druck- und RÃ¼ckzugslogik

Die Berechnung erfolgt **fÃ¼r jeden GerÃ¤tetrÃ¤ger einzeln**. Der schwÃ¤chste GerÃ¤tetrÃ¤ger bestimmt die Entscheidung fÃ¼r den gesamten Trupp.

### 6.1 Umkehrdruck am Einsatzziel

Beim Erreichen des Einsatzziels wird der aktuelle Druck jedes GerÃ¤tetrÃ¤gers erfasst.

```text
Hinwegverbrauch = Startdruck - Druck am Einsatzziel
Umkehrdruck     = (Hinwegverbrauch Ã— 2) + Reserve
```

Die Reserve betrÃ¤gt im MVP standardmÃ¤ÃŸig 50 bar.

Beispiel:

```text
Startdruck:             300 bar
Druck am Einsatzziel:   220 bar
Hinwegverbrauch:         80 bar
RÃ¼ckwegberechnung:      160 bar
Reserve:                 50 bar
Umkehrdruck:            210 bar
```

Bei 220 bar darf der GerÃ¤tetrÃ¤ger noch am Einsatzziel arbeiten. Sobald eine tatsÃ¤chliche Druckmeldung 210 bar oder weniger ergibt, ist der Umkehrdruck erreicht. Der gesamte Trupp muss den RÃ¼ckzug antreten.

### 6.2 Vor dem Einsatzziel

Solange das Einsatzziel noch nicht bestÃ¤tigt wurde, wird aus dem bisherigen Verbrauch ein vorlÃ¤ufiger Umkehrdruck berechnet. Dadurch kann ein hoher Luftverbrauch bereits wÃ¤hrend des Vorgehens erkannt werden, obwohl die Anwendung die tatsÃ¤chlich zurÃ¼ckgelegte Wegstrecke nicht kennt.

### 6.3 Verbrauchsprognose

Aus Startzeit, Startdruck und letzter Druckmeldung wird je GerÃ¤tetrÃ¤ger ein durchschnittlicher Verbrauch in `bar/min` berechnet. Daraus wird abgeschÃ¤tzt, wann der maÃŸgebliche Umkehrdruck bei gleichbleibendem Verbrauch erreicht wird.

Die Prognose ist eine **FrÃ¼hwarnung**, kein Ersatz fÃ¼r eine aktuelle Druckmeldung.

### 6.4 Warnstufen

1. **FrÃ¼hwarnung:** prognostizierter Umkehrdruck in hÃ¶chstens 5 Minuten â€“ aktuelle Druckmeldung anfordern.
2. **RÃ¼ckzug:** mindestens ein GerÃ¤tetrÃ¤ger hat seinen individuellen Umkehrdruck tatsÃ¤chlich erreicht oder unterschritten â€“ gesamter Trupp zurÃ¼ck.
3. **Kritisch:** 50 bar oder weniger â€“ hÃ¶chste Druckwarnstufe.

Die Entscheidung wird nie aus einem gemittelten Truppdruck abgeleitet.

## 7. Atemnotfall

Ein Atemnotfall ist kein bloÃŸer Dokumentationseintrag, sondern ein aktiver Zustand des Trupps.

Beim AuslÃ¶sen werden gespeichert:

- betroffener GerÃ¤tetrÃ¤ger
- Trupp
- exakter Zeitpunkt
- optionale Bemerkung

Der Trupp erhÃ¤lt den Status `Emergency` und muss in den Lageansichten deutlich priorisiert werden. Ein eingeleiteter RÃ¼ckzug beendet den Notfallstatus nicht. Erst die RÃ¼ckmeldung des Trupps beendet den aktiven Notfall; das ursprÃ¼ngliche Ereignis bleibt im Ereignisprotokoll erhalten.

## 8. Sicherheitstrupp und Notfalltasche

FÃ¼r einen Sicherheitstrupp wird zusÃ¤tzlich der Druck der Reserveflasche in der Notfalltasche dokumentiert. Beim Anlegen eines Sicherheitstrupps ist ein Startdruck erforderlich.

SpÃ¤tere Kontrollen werden als `SafetyBagPressureRecorded` mit Zeitpunkt, Druck und optionaler Bemerkung protokolliert. Der Druck der Reserveflasche ist bewusst unabhÃ¤ngig von den FlaschendrÃ¼cken der GerÃ¤tetrÃ¤ger.

## 9. UI-GrundsÃ¤tze

Die OberflÃ¤che ist fÃ¼r Touch-Bedienung im Einsatz gedacht.

- groÃŸe SchaltflÃ¤chen und Druck-Schnellwerte
- keine kleinen Browser-Spinner fÃ¼r Druckwerte
- mÃ¶glichst keine verschachtelten oder modalen Scrollbalken
- Dialoge nutzen verfÃ¼gbare Bildschirmbreite
- sicherheitsrelevante ZustÃ¤nde mÃ¼ssen ohne Detailnavigation erkennbar sein
- AtemnotfÃ¤lle werden in Lageansichten priorisiert
- beendete Trupps bleiben fÃ¼r Nachdokumentation erreichbar

## 10. Lokaler Start

Voraussetzung ist das .NET 10 SDK.

```powershell
dotnet restore
dotnet run
```

AnschlieÃŸend die von `Now listening on:` ausgegebene Adresse Ã¶ffnen. Die konkrete Portnummer kann durch das lokale Launch-Profil abweichen.

### Demo-Daten zurÃ¼cksetzen

Anwendung beenden und `fireops.db` lÃ¶schen. Beim nÃ¤chsten Start wird die lokale Datenbank neu angelegt.

## 11. Vor einem Commit

Mindestens folgende PrÃ¼fungen durchfÃ¼hren:

```powershell
dotnet restore
dotnet build
```

Danach die wichtigsten AblÃ¤ufe manuell testen:

1. Trupp anlegen und Atemschutzeinsatz starten.
2. Druckmeldungen fÃ¼r beide Personen erfassen.
3. Einsatzziel bestÃ¤tigen und individuellen Umkehrdruck prÃ¼fen.
4. PrÃ¼fen, dass der zuerst erreichte Grenzwert den RÃ¼ckzug des gesamten Trupps auslÃ¶st.
5. 50-bar-Warnung prÃ¼fen.
6. Atemnotfall fÃ¼r eine konkrete Person auslÃ¶sen und Lageansichten prÃ¼fen.
7. Sicherheitstrupp mit Reserveflasche anlegen und weiteren Flaschendruck dokumentieren.
8. RÃ¼ckzug, RÃ¼ckkehr und Abschluss prÃ¼fen.

## 12. Git-Konventionen

Nicht versionieren:

- `bin/`, `obj/`, `artifacts/`
- `.vs/` und benutzerspezifische IDE-Dateien
- lokale SQLite-Datenbanken
- Logs und temporÃ¤re Dateien

Commit-Nachrichten sollten beschreiben, **warum bzw. welche fachliche Ã„nderung** enthalten ist, z. B.:

```text
Add individual turnaround pressure warnings
Add emergency firefighter tracking
Document safety bag reserve pressure
```

## 13. Sinnvolle nÃ¤chste technische Schritte

- Unit-Tests fÃ¼r Umkehrdruck, Verbrauchsprognose und Warnstufen
- Fachberechnungen aus `FireOpsService` in eigene Services/Domain-Klassen extrahieren
- EF-Core-Migrationsstrategie statt ausschlieÃŸlich Initialisierung
- Authentifizierung/PIN und Rollenmodell
- Offline-/MehrgerÃ¤te-Synchronisation
- PWA/Outbox-Konzept
- strukturierte Protokollierung und Fehlerbehandlung
- Alamos-/Einsatzschnittstelle erst nach Stabilisierung des fachlichen Modells

