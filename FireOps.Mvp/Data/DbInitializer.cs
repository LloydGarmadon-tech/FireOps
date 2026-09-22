using FireOps.Mvp.Domain;
using Microsoft.EntityFrameworkCore;

namespace FireOps.Mvp.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FireOpsDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();

        if (await db.Incidents.AnyAsync())
        {
            await EnsureDemoAgtMasterDataAsync(db);
            return;
        }

        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            ExternalId = "DEMO-001",
            Keyword = "FEU Gebäude",
            Message = "Demo Atemschutzeinsatz",
            Address = "Hauptstraße 12",
            StartedAt = DateTimeOffset.Now.AddMinutes(-8),
            Status = IncidentStatus.Active
        };

        var hlf = new Unit { Id = Guid.NewGuid(), IncidentId = incident.Id, CallSign = "HLF 20/1", VehicleType = "HLF 20", Status = UnitStatus.OnScene };
        var lf = new Unit { Id = Guid.NewGuid(), IncidentId = incident.Id, CallSign = "LF 10/2", VehicleType = "LF 10", Status = UnitStatus.OnScene };
        var tlf = new Unit { Id = Guid.NewGuid(), IncidentId = incident.Id, CallSign = "TLF 16/3", VehicleType = "TLF", Status = UnitStatus.OnScene };

        var firefighters = new[]
        {
            new Firefighter { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Meier" },
            new Firefighter { Id = Guid.NewGuid(), FirstName = "Tom", LastName = "Schulz" },
            new Firefighter { Id = Guid.NewGuid(), FirstName = "Nina", LastName = "Hansen" },
            new Firefighter { Id = Guid.NewGuid(), FirstName = "Lena", LastName = "Petersen" },
            new Firefighter { Id = Guid.NewGuid(), FirstName = "Kai", LastName = "Müller" },
            new Firefighter { Id = Guid.NewGuid(), FirstName = "Sven", LastName = "Schmidt" },
            new Firefighter { Id = Guid.NewGuid(), FirstName = "Mara", LastName = "Jensen" },
            new Firefighter { Id = Guid.NewGuid(), FirstName = "Tim", LastName = "Kruse" },
            new Firefighter { Id = Guid.NewGuid(), FirstName = "Lea", LastName = "Fischer" },
            new Firefighter { Id = Guid.NewGuid(), FirstName = "Ben", LastName = "Wolf" },
            new Firefighter { Id = Guid.NewGuid(), FirstName = "Sara", LastName = "Koch" },
            new Firefighter { Id = Guid.NewGuid(), FirstName = "Ole", LastName = "Becker" },
            new Firefighter { Id = Guid.NewGuid(), FirstName = "Julia", LastName = "Bollhorn" },
            new Firefighter { Id = Guid.NewGuid(), FirstName = "Collin", LastName = "Jönicke" }
        };

        var hlfStation = new MonitoringStation { Id = Guid.NewGuid(), IncidentId = incident.Id, Name = "ASÜ HLF 20/1", Type = MonitoringStationType.Unit, UnitId = hlf.Id };
        var lfStation = new MonitoringStation { Id = Guid.NewGuid(), IncidentId = incident.Id, Name = "ASÜ LF 10/2", Type = MonitoringStationType.Unit, UnitId = lf.Id };
        var tlfStation = new MonitoringStation { Id = Guid.NewGuid(), IncidentId = incident.Id, Name = "ASÜ TLF 16/3", Type = MonitoringStationType.Unit, UnitId = tlf.Id };
        var centralStation = new MonitoringStation { Id = Guid.NewGuid(), IncidentId = incident.Id, Name = "Zentrale ASÜ", Type = MonitoringStationType.Central };

        db.AddRange(incident, hlf, lf, tlf);
        db.Firefighters.AddRange(firefighters);
        db.MonitoringStations.AddRange(hlfStation, lfStation, tlfStation, centralStation);
        await db.SaveChangesAsync();

        await EnsureDemoAgtMasterDataAsync(db);
    }

    private static async Task EnsureDemoAgtMasterDataAsync(FireOpsDbContext db)
    {
        var agt = await db.Qualifications.SingleOrDefaultAsync(x => x.Code == "AGT");
        if (agt is null)
        {
            agt = new Qualification { Id = Guid.NewGuid(), Code = "AGT", Name = "Atemschutzgeräteträger" };
            db.Qualifications.Add(agt);
            await db.SaveChangesAsync();
        }

        // Für den MVP gelten alle vorhandenen Demo-Stammdaten als AGT. Frei im Einsatz
        // eingegebene Namen erhalten diese Qualifikation bewusst nicht automatisch.
        var firefighters = await db.Firefighters.Where(x => x.IsActive).ToListAsync();
        var existing = await db.FirefighterQualifications
            .Where(x => x.QualificationId == agt.Id)
            .Select(x => x.FirefighterId)
            .ToListAsync();
        var existingSet = existing.ToHashSet();

        foreach (var firefighter in firefighters.Where(x => !existingSet.Contains(x.Id)))
        {
            // Nur typische Demodaten markieren. Fremde, später frei erfasste Kräfte bleiben
            // aus der Stammdaten-Vorschlagsliste heraus.
            var demoLastNames = new[] { "Meier", "Schulz", "Hansen", "Petersen", "Müller", "Schmidt", "Jensen", "Kruse", "Fischer", "Wolf", "Koch", "Becker", "Bollhorn", "Jönicke" };
            if (demoLastNames.Contains(firefighter.LastName, StringComparer.OrdinalIgnoreCase))
            {
                db.FirefighterQualifications.Add(new FirefighterQualification
                {
                    Id = Guid.NewGuid(), FirefighterId = firefighter.Id, QualificationId = agt.Id
                });
            }
        }

        await db.SaveChangesAsync();
    }
}
