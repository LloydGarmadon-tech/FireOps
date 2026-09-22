using System.Text.Json;
using FireOps.Data;
using FireOps.Domain;
using FireOps.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace FireOps.Services;

public sealed class FireOpsService(
    IDbContextFactory<FireOpsDbContext> dbFactory,
    ChangeNotifier notifier)
{
    // FÃ¼r den MVP reprÃ¤sentiert diese ID das aktuell verwendete lokale GerÃ¤t.
    private static readonly Guid DeviceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public async Task<IReadOnlyList<IncidentListItem>> GetIncidentsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var incidents = await db.Incidents.AsNoTracking().ToListAsync();
        incidents = incidents.OrderByDescending(x => x.StartedAt).ToList();
        var result = new List<IncidentListItem>();

        foreach (var incident in incidents)
        {
            var unitCount = await db.Units.CountAsync(x => x.IncidentId == incident.Id);
            var activeCount = await db.BreathingTeams.CountAsync(x => x.IncidentId == incident.Id &&
                (x.Status == BreathingTeamStatus.Active || x.Status == BreathingTeamStatus.TargetReached || x.Status == BreathingTeamStatus.Returning || x.Status == BreathingTeamStatus.Emergency));
            result.Add(new IncidentListItem(incident.Id, incident.Keyword, incident.Address, incident.StartedAt, unitCount, activeCount));
        }

        return result;
    }

    public async Task<IncidentOverviewVm?> GetIncidentOverviewAsync(Guid incidentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var incident = await db.Incidents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == incidentId);
        if (incident is null) return null;

        var units = await db.Units.AsNoTracking().Where(x => x.IncidentId == incidentId).OrderBy(x => x.CallSign).ToListAsync();
        var stations = await db.MonitoringStations.AsNoTracking().Where(x => x.IncidentId == incidentId).ToListAsync();
        var teams = await db.BreathingTeams.AsNoTracking().Where(x => x.IncidentId == incidentId).OrderBy(x => x.Name).ToListAsync();
        var teamIds = teams.Select(x => x.Id).ToArray();
        var members = await db.BreathingTeamMembers.AsNoTracking().Where(x => teamIds.Contains(x.TeamId)).ToListAsync();
        var firefighterIds = members.Select(x => x.FirefighterId).Distinct().ToArray();
        var firefighters = await db.Firefighters.AsNoTracking().Where(x => firefighterIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var readings = await db.PressureReadings.AsNoTracking().Where(x => teamIds.Contains(x.TeamId)).ToListAsync();
        var targetEvents = await db.IncidentEvents.AsNoTracking()
            .Where(x => x.TeamId.HasValue && teamIds.Contains(x.TeamId.Value) && x.EventType == "TargetReached")
            .ToListAsync();
        var emergencyEvents = await db.IncidentEvents.AsNoTracking()
            .Where(x => x.TeamId.HasValue && teamIds.Contains(x.TeamId.Value) && x.EventType == "EmergencyDeclared")
            .ToListAsync();
        var safetyBagEvents = await db.IncidentEvents.AsNoTracking()
            .Where(x => x.TeamId.HasValue && teamIds.Contains(x.TeamId.Value) && x.EventType == "SafetyBagPressureRecorded")
            .ToListAsync();

        var unitVms = new List<UnitOverviewVm>();
        foreach (var unit in units)
        {
            var teamCards = new List<TeamCardVm>();
            foreach (var team in teams.Where(x => x.UnitId == unit.Id))
            {
                var teamMembers = members.Where(x => x.TeamId == team.Id).OrderBy(x => x.Position).ToList();
                var memberVms = teamMembers.Select(m =>
                {
                    var latest = readings.Where(r => r.TeamId == team.Id && r.FirefighterId == m.FirefighterId)
                        .OrderByDescending(r => r.RecordedAt).FirstOrDefault();
                    var memberReadings = readings.Where(r => r.TeamId == team.Id && r.FirefighterId == m.FirefighterId).ToList();
                    var target = GetTurnaroundCalculation(targetEvents.Where(e => e.TeamId == team.Id), m.FirefighterId);
                    var rate = CalculateConsumptionRate(team, m, memberReadings);
                    return new MemberPressureVm
                    {
                        FirefighterId = m.FirefighterId,
                        Name = firefighters.TryGetValue(m.FirefighterId, out var f) ? f.DisplayName : "Unbekannt",
                        FunctionLabel = m.Position == 1 ? "TruppfÃ¼hrer" : "Truppmann",
                        InitialPressureBar = m.InitialPressureBar,
                        LatestPressureBar = latest?.PressureBar,
                        LatestPressureAt = latest?.RecordedAt,
                        TargetPressureBar = target?.TargetPressureBar,
                        TurnaroundPressureBar = target?.TurnaroundPressureBar,
                        ReservePressureBar = target?.ReserveBar ?? 50,
                        ConsumptionRateBarPerMinute = rate
                    };
                }).ToList();

                teamCards.Add(new TeamCardVm
                {
                    Team = team,
                    UnitCallSign = unit.CallSign,
                    MonitoringStationName = stations.FirstOrDefault(s => s.Id == team.MonitoringStationId)?.Name ?? "-",
                    Members = memberVms,
                    LastReadingAt = memberVms.Max(x => x.LatestPressureAt),
                    EmergencyDeclaredAt = emergencyEvents
                        .Where(x => x.TeamId == team.Id)
                        .OrderByDescending(x => x.OccurredAt)
                        .Select(x => (DateTimeOffset?)x.OccurredAt)
                        .FirstOrDefault(),
                    EmergencyFirefighterId = ExtractEmergencyPerson(emergencyEvents
                        .Where(x => x.TeamId == team.Id)
                        .OrderByDescending(x => x.OccurredAt)
                        .FirstOrDefault()?.PayloadJson).Id,
                    EmergencyFirefighterName = ExtractEmergencyPerson(emergencyEvents
                        .Where(x => x.TeamId == team.Id)
                        .OrderByDescending(x => x.OccurredAt)
                        .FirstOrDefault()?.PayloadJson).Name,
                    SafetyBagPressureBar = ExtractSafetyBagPressure(safetyBagEvents
                        .Where(x => x.TeamId == team.Id)
                        .OrderByDescending(x => x.OccurredAt)
                        .FirstOrDefault()?.PayloadJson),
                    SafetyBagPressureAt = safetyBagEvents
                        .Where(x => x.TeamId == team.Id)
                        .OrderByDescending(x => x.OccurredAt)
                        .Select(x => (DateTimeOffset?)x.OccurredAt)
                        .FirstOrDefault()
                });
            }

            unitVms.Add(new UnitOverviewVm { Unit = unit, Teams = teamCards });
        }

        var central = stations.First(x => x.Type == MonitoringStationType.Central);
        return new IncidentOverviewVm { Incident = incident, Units = unitVms, CentralStation = central };
    }

    public async Task<TeamDetailsVm?> GetTeamDetailsAsync(Guid teamId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var team = await db.BreathingTeams.AsNoTracking().SingleOrDefaultAsync(x => x.Id == teamId);
        if (team is null) return null;

        var unit = await db.Units.AsNoTracking().SingleAsync(x => x.Id == team.UnitId);
        var station = await db.MonitoringStations.AsNoTracking().SingleAsync(x => x.Id == team.MonitoringStationId);
        var members = await db.BreathingTeamMembers.AsNoTracking().Where(x => x.TeamId == teamId).OrderBy(x => x.Position).ToListAsync();
        var firefighterIds = members.Select(x => x.FirefighterId).ToArray();
        var firefighters = await db.Firefighters.AsNoTracking().Where(x => firefighterIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var readings = await db.PressureReadings.AsNoTracking().Where(x => x.TeamId == teamId).ToListAsync();
        readings = readings.OrderByDescending(x => x.RecordedAt).ToList();
        var allEvents = await db.IncidentEvents.AsNoTracking().Where(x => x.TeamId == teamId).ToListAsync();
        var events = allEvents.OrderByDescending(x => x.OccurredAt).Take(30).ToList();

        var memberVms = members.Select(m =>
        {
            var latest = readings.FirstOrDefault(r => r.FirefighterId == m.FirefighterId);
            var memberReadings = readings.Where(r => r.FirefighterId == m.FirefighterId).ToList();
            var target = GetTurnaroundCalculation(allEvents.Where(e => e.EventType == "TargetReached"), m.FirefighterId);
            var rate = CalculateConsumptionRate(team, m, memberReadings);
            return new MemberPressureVm
            {
                FirefighterId = m.FirefighterId,
                Name = firefighters[m.FirefighterId].DisplayName,
                FunctionLabel = m.Position == 1 ? "TruppfÃ¼hrer" : "Truppmann",
                InitialPressureBar = m.InitialPressureBar,
                LatestPressureBar = latest?.PressureBar,
                LatestPressureAt = latest?.RecordedAt,
                TargetPressureBar = target?.TargetPressureBar,
                TurnaroundPressureBar = target?.TurnaroundPressureBar,
                ReservePressureBar = target?.ReserveBar ?? 50,
                ConsumptionRateBarPerMinute = rate
            };
        }).ToList();

        var history = readings.Select(r => new PressureHistoryVm(
            r.Id,
            r.FirefighterId,
            firefighters.TryGetValue(r.FirefighterId, out var f) ? f.DisplayName : "Unbekannt",
            r.PressureBar,
            r.RecordedAt,
            r.Source,
            r.IsCorrection)).ToList();

        var latestEmergency = allEvents
            .Where(x => x.EventType == "EmergencyDeclared")
            .OrderByDescending(x => x.OccurredAt)
            .FirstOrDefault();
        var emergencyPerson = ExtractEmergencyPerson(latestEmergency?.PayloadJson);
        var safetyBagHistory = allEvents
            .Where(x => x.EventType == "SafetyBagPressureRecorded")
            .OrderByDescending(x => x.OccurredAt)
            .Select(x => new SafetyBagPressureHistoryVm(
                x.Id,
                ExtractSafetyBagPressure(x.PayloadJson) ?? 0,
                x.OccurredAt,
                ExtractSafetyBagNote(x.PayloadJson)))
            .Where(x => x.PressureBar > 0)
            .ToList();

        return new TeamDetailsVm
        {
            Team = team,
            Unit = unit,
            MonitoringStation = station,
            Members = memberVms,
            PressureHistory = history,
            Events = events,
            EmergencyDeclaredAt = latestEmergency?.OccurredAt,
            EmergencyFirefighterId = emergencyPerson.Id,
            EmergencyFirefighterName = emergencyPerson.Name,
            EmergencyNote = ExtractEmergencyNote(latestEmergency?.PayloadJson),
            SafetyBagPressureHistory = safetyBagHistory
        };
    }

    public async Task<IReadOnlyList<Firefighter>> GetAvailableFirefightersAsync(Guid incidentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var used = await db.BreathingTeamMembers
            .Join(db.BreathingTeams.Where(t => t.IncidentId == incidentId && t.Status != BreathingTeamStatus.Completed), m => m.TeamId, t => t.Id, (m, t) => m.FirefighterId)
            .Distinct().ToListAsync();
        return await db.Firefighters.AsNoTracking().Where(x => x.IsActive && !used.Contains(x.Id)).OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToListAsync();
    }


    public async Task<IReadOnlyList<Firefighter>> GetAvailableAgtFirefightersAsync(Guid incidentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var agtId = await db.Qualifications.AsNoTracking()
            .Where(x => x.Code == "AGT")
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync();

        if (agtId is null)
            return Array.Empty<Firefighter>();

        var used = await db.BreathingTeamMembers
            .Join(db.BreathingTeams.Where(t => t.IncidentId == incidentId && t.Status != BreathingTeamStatus.Completed),
                m => m.TeamId, t => t.Id, (m, t) => m.FirefighterId)
            .Distinct()
            .ToListAsync();

        var agtIds = await db.FirefighterQualifications.AsNoTracking()
            .Where(x => x.QualificationId == agtId.Value)
            .Select(x => x.FirefighterId)
            .ToListAsync();

        return await db.Firefighters.AsNoTracking()
            .Where(x => x.IsActive && agtIds.Contains(x.Id) && !used.Contains(x.Id))
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<string>> GetVehicleSuggestionsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var fromDatabase = await db.Units.AsNoTracking()
            .Where(x => x.CallSign != "Ohne Fahrzeug")
            .Select(x => x.CallSign)
            .Distinct()
            .ToListAsync();

        var demoMasterData = new[]
        {
            "HLF 20/1", "LF 10/2", "TLF 16/3", "ELW 1", "MTW 1", "LF 20 KatS"
        };

        return fromDatabase.Concat(demoMasterData)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    public async Task<Guid> CreateTeamAsync(Guid incidentId, Guid unitId, string name, BreathingTeamRole role,
        Guid person1Id, Guid person2Id, int pressure1, int pressure2, string? mission, string? accessRoute)
    {
        if (person1Id == person2Id) throw new InvalidOperationException("Ein Trupp benÃ¶tigt zwei unterschiedliche Personen.");
        ValidatePressure(pressure1);
        ValidatePressure(pressure2);

        await using var db = await dbFactory.CreateDbContextAsync();
        var existingCount = await db.BreathingTeams.CountAsync(x => x.UnitId == unitId && x.Status != BreathingTeamStatus.Completed);
        if (existingCount >= 3) throw new InvalidOperationException("FÃ¼r dieses Fahrzeug sind bereits drei aktive/vorbereitete Trupps angelegt.");

        var station = await db.MonitoringStations.SingleAsync(x => x.IncidentId == incidentId && x.UnitId == unitId && x.Type == MonitoringStationType.Unit);
        var team = new BreathingTeam
        {
            Id = Guid.NewGuid(), IncidentId = incidentId, UnitId = unitId, Name = name.Trim(), Role = role,
            Status = BreathingTeamStatus.Ready, MonitoringStationId = station.Id, Mission = mission?.Trim(), AccessRoute = accessRoute?.Trim(),
            ExpectedDurationMinutes = 30, LastModifiedAt = DateTimeOffset.Now, Version = 1
        };
        db.BreathingTeams.Add(team);
        db.BreathingTeamMembers.AddRange(
            new BreathingTeamMember { Id = Guid.NewGuid(), TeamId = team.Id, FirefighterId = person1Id, Position = 1, InitialPressureBar = pressure1 },
            new BreathingTeamMember { Id = Guid.NewGuid(), TeamId = team.Id, FirefighterId = person2Id, Position = 2, InitialPressureBar = pressure2 });

        db.PressureReadings.AddRange(
            NewPressure(team, person1Id, pressure1, InformationSource.DirectObservation),
            NewPressure(team, person2Id, pressure2, InformationSource.DirectObservation));
        AddEvent(db, team, "TeamCreated", new { team.Name, team.Role, pressure1, pressure2 });
        await db.SaveChangesAsync();
        notifier.Notify(incidentId);
        return team.Id;
    }


    public async Task<Guid> CreateTeamQuickAsync(
        Guid incidentId,
        string name,
        BreathingTeamRole role,
        string teamLeaderName,
        string teamMemberName,
        int pressure1,
        int pressure2,
        string? vehicleCallSign,
        bool useCentralMonitoring,
        string? mission,
        string? accessRoute,
        int? safetyBagPressureBar = null)
    {
        if (string.IsNullOrWhiteSpace(teamLeaderName) || string.IsNullOrWhiteSpace(teamMemberName))
            throw new InvalidOperationException("Bitte TruppfÃ¼hrer und Truppmann eingeben.");
        if (string.Equals(teamLeaderName.Trim(), teamMemberName.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("TruppfÃ¼hrer und Truppmann mÃ¼ssen unterschiedliche Personen sein.");

        ValidatePressure(pressure1);
        ValidatePressure(pressure2);

        await using var db = await dbFactory.CreateDbContextAsync();

        var leader = await ResolveOrCreateFirefighterAsync(db, teamLeaderName);
        var member = await ResolveOrCreateFirefighterAsync(db, teamMemberName);
        if (leader.Id == member.Id)
            throw new InvalidOperationException("TruppfÃ¼hrer und Truppmann mÃ¼ssen unterschiedliche Personen sein.");

        var unit = await ResolveOrCreateUnitAsync(db, incidentId, vehicleCallSign);
        MonitoringStation station;

        if (useCentralMonitoring || string.IsNullOrWhiteSpace(vehicleCallSign))
        {
            station = await db.MonitoringStations.SingleAsync(x => x.IncidentId == incidentId && x.Type == MonitoringStationType.Central);
        }
        else
        {
            station = await db.MonitoringStations.SingleOrDefaultAsync(x => x.IncidentId == incidentId && x.UnitId == unit.Id && x.Type == MonitoringStationType.Unit)
                ?? new MonitoringStation
                {
                    Id = Guid.NewGuid(), IncidentId = incidentId, UnitId = unit.Id,
                    Name = $"ASÃœ {unit.CallSign}", Type = MonitoringStationType.Unit, IsActive = true
                };
            if (db.Entry(station).State == EntityState.Detached)
                db.MonitoringStations.Add(station);
        }

        var team = new BreathingTeam
        {
            Id = Guid.NewGuid(), IncidentId = incidentId, UnitId = unit.Id,
            Name = string.IsNullOrWhiteSpace(name) ? "Trupp" : name.Trim(),
            Role = role, Status = BreathingTeamStatus.Ready, MonitoringStationId = station.Id,
            Mission = mission?.Trim(), AccessRoute = accessRoute?.Trim(), ExpectedDurationMinutes = 30,
            LastModifiedAt = DateTimeOffset.Now, Version = 1
        };

        db.BreathingTeams.Add(team);
        db.BreathingTeamMembers.AddRange(
            new BreathingTeamMember { Id = Guid.NewGuid(), TeamId = team.Id, FirefighterId = leader.Id, Position = 1, InitialPressureBar = pressure1 },
            new BreathingTeamMember { Id = Guid.NewGuid(), TeamId = team.Id, FirefighterId = member.Id, Position = 2, InitialPressureBar = pressure2 });
        db.PressureReadings.AddRange(
            NewPressure(team, leader.Id, pressure1, InformationSource.DirectObservation),
            NewPressure(team, member.Id, pressure2, InformationSource.DirectObservation));

        if (role == BreathingTeamRole.SafetyTeam)
        {
            if (safetyBagPressureBar is null)
                throw new InvalidOperationException("Beim Sicherheitstrupp muss der Druck der Reserveflasche in der Notfalltasche dokumentiert werden.");
            ValidatePressure(safetyBagPressureBar.Value);
            AddEvent(db, team, "SafetyBagPressureRecorded", new
            {
                PressureBar = safetyBagPressureBar.Value,
                Note = "Startdruck Reserveflasche",
                Source = InformationSource.DirectObservation.ToString()
            });
        }

        AddEvent(db, team, "TeamCreated", new
        {
            team.Name, team.Role, TeamLeader = leader.DisplayName, TeamMember = member.DisplayName,
            Vehicle = unit.CallSign, pressure1, pressure2, Monitoring = station.Name
        });

        await db.SaveChangesAsync();
        notifier.Notify(incidentId);
        return team.Id;
    }

    private static async Task<Firefighter> ResolveOrCreateFirefighterAsync(FireOpsDbContext db, string enteredName)
    {
        var normalized = enteredName.Trim();
        var existing = (await db.Firefighters.ToListAsync())
            .FirstOrDefault(x => string.Equals(x.DisplayName, normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var firefighter = new Firefighter
        {
            Id = Guid.NewGuid(),
            FirstName = parts.Length == 1 ? parts[0] : string.Join(" ", parts[..^1]),
            LastName = parts.Length == 1 ? string.Empty : parts[^1],
            IsActive = true
        };
        db.Firefighters.Add(firefighter);
        return firefighter;
    }

    private static async Task<Unit> ResolveOrCreateUnitAsync(FireOpsDbContext db, Guid incidentId, string? enteredCallSign)
    {
        var callSign = string.IsNullOrWhiteSpace(enteredCallSign) ? "Ohne Fahrzeug" : enteredCallSign.Trim();
        var existing = (await db.Units.Where(x => x.IncidentId == incidentId).ToListAsync())
            .FirstOrDefault(x => string.Equals(x.CallSign, callSign, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;

        var unit = new Unit
        {
            Id = Guid.NewGuid(), IncidentId = incidentId, CallSign = callSign,
            VehicleType = callSign == "Ohne Fahrzeug" ? "Nicht zugeordnet" : "Weitere Einheit",
            Status = UnitStatus.OnScene
        };
        db.Units.Add(unit);
        return unit;
    }

    public async Task UpdateTeamDetailsAsync(
        Guid teamId,
        string name,
        BreathingTeamRole role,
        string? vehicleCallSign,
        string? mission,
        string? location,
        string? accessRoute,
        int expectedDurationMinutes)
    {
        if (expectedDurationMinutes is < 1 or > 240)
            throw new InvalidOperationException("Die erwartete Einsatzzeit muss zwischen 1 und 240 Minuten liegen.");

        await using var db = await dbFactory.CreateDbContextAsync();
        var team = await db.BreathingTeams.SingleAsync(x => x.Id == teamId);
        var oldUnit = await db.Units.AsNoTracking().SingleAsync(x => x.Id == team.UnitId);
        var newUnit = await ResolveOrCreateUnitAsync(db, team.IncidentId, vehicleCallSign);

        var before = new
        {
            team.Name,
            team.Role,
            Vehicle = oldUnit.CallSign,
            team.Mission,
            team.Location,
            team.AccessRoute,
            team.ExpectedDurationMinutes
        };

        team.Name = string.IsNullOrWhiteSpace(name) ? team.Name : name.Trim();
        team.Role = role;
        team.UnitId = newUnit.Id;
        team.Mission = CleanOptional(mission);
        team.Location = CleanOptional(location);
        team.AccessRoute = CleanOptional(accessRoute);
        team.ExpectedDurationMinutes = expectedDurationMinutes;
        team.Version++;
        team.LastModifiedAt = DateTimeOffset.Now;

        AddEvent(db, team, "TeamDetailsUpdated", new
        {
            Before = before,
            After = new
            {
                team.Name,
                team.Role,
                Vehicle = newUnit.CallSign,
                team.Mission,
                team.Location,
                team.AccessRoute,
                team.ExpectedDurationMinutes
            }
        });

        await db.SaveChangesAsync();
        notifier.Notify(team.IncidentId);
    }

    private static string? CleanOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public Task StartTeamAsync(Guid teamId) => ChangeStatusAsync(teamId, BreathingTeamStatus.Active, "AirSupplyStarted", team => team.AirSupplyStartedAt = DateTimeOffset.Now);
    public async Task MarkTargetReachedAsync(Guid teamId, Dictionary<Guid, int> values, int reserveBar = 50)
    {
        if (reserveBar is < 0 or > 100)
            throw new InvalidOperationException("Die Reserve muss zwischen 0 und 100 bar liegen.");

        await using var db = await dbFactory.CreateDbContextAsync();
        var team = await db.BreathingTeams.SingleAsync(x => x.Id == teamId);
        var members = await db.BreathingTeamMembers.Where(x => x.TeamId == teamId).OrderBy(x => x.Position).ToListAsync();
        var now = DateTimeOffset.Now;
        var calculations = new List<object>();

        foreach (var member in members)
        {
            if (!values.TryGetValue(member.FirefighterId, out var targetPressure))
                throw new InvalidOperationException("FÃ¼r alle Truppmitglieder muss beim Erreichen des Einsatzziels ein Druckwert vorliegen.");
            ValidatePressure(targetPressure);
            var initialPressure = member.InitialPressureBar ?? targetPressure;
            var outboundConsumption = Math.Max(0, initialPressure - targetPressure);
            var turnaroundPressure = Math.Min(400, outboundConsumption * 2 + reserveBar);

            db.PressureReadings.Add(new PressureReading
            {
                Id = Guid.NewGuid(), IncidentId = team.IncidentId, TeamId = team.Id, FirefighterId = member.FirefighterId,
                PressureBar = targetPressure, RecordedAt = now, DeviceId = DeviceId, Source = InformationSource.Radio
            });

            calculations.Add(new
            {
                member.FirefighterId, InitialPressureBar = initialPressure, TargetPressureBar = targetPressure,
                OutboundConsumptionBar = outboundConsumption, TurnaroundPressureBar = turnaroundPressure, ReserveBar = reserveBar
            });
        }

        team.TargetReachedAt = now;
        team.Status = BreathingTeamStatus.TargetReached;
        team.Version++;
        team.LastModifiedAt = now;
        db.RadioContacts.Add(new RadioContact
        {
            Id = Guid.NewGuid(), IncidentId = team.IncidentId, TeamId = team.Id, OccurredAt = now,
            Type = RadioContactType.TargetReached, Result = RadioContactResult.Successful, DeviceId = DeviceId
        });
        AddEvent(db, team, "TargetReached", new { Status = team.Status.ToString(), team.Version, ReserveBar = reserveBar, Members = calculations });
        await db.SaveChangesAsync();
        notifier.Notify(team.IncidentId);
    }
    public async Task StartReturnAsync(Guid teamId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var team = await db.BreathingTeams.SingleAsync(x => x.Id == teamId);
        var now = DateTimeOffset.Now;
        team.ReturnStartedAt = now;
        // Ein aktiver Atemnotfall bleibt bis zur RÃ¼ckkehr als NOTFALL sichtbar.
        // Der RÃ¼ckzug wird zusÃ¤tzlich zeitlich dokumentiert, ohne die rote PrioritÃ¤t zu verlieren.
        if (team.Status != BreathingTeamStatus.Emergency)
            team.Status = BreathingTeamStatus.Returning;
        team.Version++;
        team.LastModifiedAt = now;
        AddEvent(db, team, "ReturnStarted", new { Status = team.Status.ToString(), EmergencyActive = team.Status == BreathingTeamStatus.Emergency, team.Version });
        await db.SaveChangesAsync();
        notifier.Notify(team.IncidentId);
    }

    public async Task DeclareEmergencyAsync(Guid teamId, Guid firefighterId, string? note = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var team = await db.BreathingTeams.SingleAsync(x => x.Id == teamId);
        if (team.Status is BreathingTeamStatus.Completed or BreathingTeamStatus.Returned)
            throw new InvalidOperationException("FÃ¼r einen bereits zurÃ¼ckgekehrten oder beendeten Trupp kann kein Atemnotfall ausgelÃ¶st werden.");

        var member = await db.BreathingTeamMembers.SingleOrDefaultAsync(x => x.TeamId == teamId && x.FirefighterId == firefighterId)
            ?? throw new InvalidOperationException("Die ausgewÃ¤hlte Person gehÃ¶rt nicht zu diesem Trupp.");
        var firefighter = await db.Firefighters.SingleAsync(x => x.Id == member.FirefighterId);

        var previousStatus = team.Status;
        var now = DateTimeOffset.Now;
        team.Status = BreathingTeamStatus.Emergency;
        team.Version++;
        team.LastModifiedAt = now;

        db.IncidentEvents.Add(new IncidentEvent
        {
            Id = Guid.NewGuid(), IncidentId = team.IncidentId, TeamId = team.Id, DeviceId = DeviceId,
            OccurredAt = now, CreatedAt = DateTimeOffset.UtcNow, EventType = "EmergencyDeclared",
            PayloadJson = JsonSerializer.Serialize(new
            {
                Status = team.Status.ToString(),
                PreviousStatus = previousStatus.ToString(),
                FirefighterId = firefighter.Id,
                PersonName = firefighter.DisplayName,
                Note = CleanOptional(note),
                team.Version
            })
        });

        await db.SaveChangesAsync();
        notifier.Notify(team.IncidentId);
    }

    public async Task RecordSafetyBagPressureAsync(Guid teamId, int pressureBar, string? note = null)
    {
        ValidatePressure(pressureBar);
        await using var db = await dbFactory.CreateDbContextAsync();
        var team = await db.BreathingTeams.SingleAsync(x => x.Id == teamId);
        if (team.Role != BreathingTeamRole.SafetyTeam)
            throw new InvalidOperationException("Eine Notfalltasche wird in dieser Ansicht nur fÃ¼r Sicherheitstrupps gefÃ¼hrt.");

        AddEvent(db, team, "SafetyBagPressureRecorded", new
        {
            PressureBar = pressureBar,
            Note = CleanOptional(note),
            Source = InformationSource.DirectObservation.ToString()
        });
        team.Version++;
        team.LastModifiedAt = DateTimeOffset.Now;
        await db.SaveChangesAsync();
        notifier.Notify(team.IncidentId);
    }

    public async Task MarkReturnedAsync(Guid teamId)
    {
        await ChangeStatusAsync(teamId, BreathingTeamStatus.Returned, "TeamReturned", team => team.ReturnedAt = DateTimeOffset.Now);
    }

    public async Task CompleteTeamAsync(Guid teamId)
    {
        await ChangeStatusAsync(teamId, BreathingTeamStatus.Completed, "TeamCompleted", _ => { });
    }

    public async Task RecordPressureAsync(Guid teamId, Dictionary<Guid, int> values, InformationSource source = InformationSource.Radio)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var team = await db.BreathingTeams.SingleAsync(x => x.Id == teamId);
        foreach (var (firefighterId, pressure) in values)
        {
            ValidatePressure(pressure);
            db.PressureReadings.Add(NewPressure(team, firefighterId, pressure, source));
        }
        db.RadioContacts.Add(new RadioContact
        {
            Id = Guid.NewGuid(), IncidentId = team.IncidentId, TeamId = team.Id, OccurredAt = DateTimeOffset.Now,
            Type = RadioContactType.Pressure, Result = RadioContactResult.Successful, DeviceId = DeviceId
        });
        AddEvent(db, team, "PressureRecorded", values);
        await db.SaveChangesAsync();
        notifier.Notify(team.IncidentId);
    }

    public async Task RequestCentralTransferAsync(Guid teamId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var team = await db.BreathingTeams.SingleAsync(x => x.Id == teamId);
        if (await db.MonitoringTransfers.AnyAsync(x => x.TeamId == teamId && x.Status == TransferStatus.Requested))
            return;

        var central = await db.MonitoringStations.SingleAsync(x => x.IncidentId == team.IncidentId && x.Type == MonitoringStationType.Central);
        if (team.MonitoringStationId == central.Id) return;

        var transfer = new MonitoringTransfer
        {
            Id = Guid.NewGuid(), IncidentId = team.IncidentId, TeamId = team.Id, FromStationId = team.MonitoringStationId,
            ToStationId = central.Id, RequestedAt = DateTimeOffset.Now, Status = TransferStatus.Requested
        };
        db.MonitoringTransfers.Add(transfer);
        AddEvent(db, team, "MonitoringTransferRequested", new { transfer.FromStationId, transfer.ToStationId });
        await db.SaveChangesAsync();
        notifier.Notify(team.IncidentId);
    }

    public async Task<IReadOnlyList<PendingTransferVm>> GetPendingTransfersAsync(Guid incidentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var transfers = await db.MonitoringTransfers.AsNoTracking().Where(x => x.IncidentId == incidentId && x.Status == TransferStatus.Requested).ToListAsync();
        transfers = transfers.OrderBy(x => x.RequestedAt).ToList();
        var teams = await db.BreathingTeams.AsNoTracking().Where(x => x.IncidentId == incidentId).ToDictionaryAsync(x => x.Id);
        var units = await db.Units.AsNoTracking().Where(x => x.IncidentId == incidentId).ToDictionaryAsync(x => x.Id);
        var stations = await db.MonitoringStations.AsNoTracking().Where(x => x.IncidentId == incidentId).ToDictionaryAsync(x => x.Id);

        return transfers.Select(t => new PendingTransferVm
        {
            Transfer = t,
            TeamName = teams[t.TeamId].Name,
            UnitCallSign = units[teams[t.TeamId].UnitId].CallSign,
            FromStation = stations[t.FromStationId].Name,
            ToStation = stations[t.ToStationId].Name
        }).ToList();
    }

    public async Task AcceptTransferAsync(Guid transferId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var transfer = await db.MonitoringTransfers.SingleAsync(x => x.Id == transferId);
        if (transfer.Status != TransferStatus.Requested) return;
        var team = await db.BreathingTeams.SingleAsync(x => x.Id == transfer.TeamId);
        team.MonitoringStationId = transfer.ToStationId;
        team.Version++;
        team.LastModifiedAt = DateTimeOffset.Now;
        transfer.Status = TransferStatus.Accepted;
        transfer.AcceptedAt = DateTimeOffset.Now;
        AddEvent(db, team, "MonitoringTransferAccepted", new { transfer.FromStationId, transfer.ToStationId });
        await db.SaveChangesAsync();
        notifier.Notify(team.IncidentId);
    }

    public async Task RejectTransferAsync(Guid transferId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var transfer = await db.MonitoringTransfers.SingleAsync(x => x.Id == transferId);
        if (transfer.Status != TransferStatus.Requested) return;
        transfer.Status = TransferStatus.Rejected;
        transfer.RejectedAt = DateTimeOffset.Now;
        var team = await db.BreathingTeams.SingleAsync(x => x.Id == transfer.TeamId);
        AddEvent(db, team, "MonitoringTransferRejected", new { transfer.FromStationId, transfer.ToStationId });
        await db.SaveChangesAsync();
        notifier.Notify(team.IncidentId);
    }

    private sealed record TurnaroundCalculation(int TargetPressureBar, int TurnaroundPressureBar, int ReserveBar);

    private static TurnaroundCalculation? GetTurnaroundCalculation(IEnumerable<IncidentEvent> events, Guid firefighterId)
    {
        foreach (var evt in events.OrderByDescending(x => x.OccurredAt))
        {
            try
            {
                using var doc = JsonDocument.Parse(evt.PayloadJson);
                if (!doc.RootElement.TryGetProperty("Members", out var members) || members.ValueKind != JsonValueKind.Array)
                    continue;

                var defaultReserve = doc.RootElement.TryGetProperty("ReserveBar", out var reserveElement) ? reserveElement.GetInt32() : 50;
                foreach (var member in members.EnumerateArray())
                {
                    if (!member.TryGetProperty("FirefighterId", out var idElement) || idElement.GetGuid() != firefighterId)
                        continue;
                    var target = member.GetProperty("TargetPressureBar").GetInt32();
                    var turnaround = member.GetProperty("TurnaroundPressureBar").GetInt32();
                    var reserve = member.TryGetProperty("ReserveBar", out var memberReserve) ? memberReserve.GetInt32() : defaultReserve;
                    return new TurnaroundCalculation(target, turnaround, reserve);
                }
            }
            catch (JsonException)
            {
                // Ã„ltere MVP-Ereignisse ohne Berechnungsdaten werden ignoriert.
            }
        }
        return null;
    }

    private static double? CalculateConsumptionRate(BreathingTeam team, BreathingTeamMember member, IReadOnlyCollection<PressureReading> readings)
    {
        if (team.AirSupplyStartedAt is null || member.InitialPressureBar is null) return null;
        var latest = readings.OrderByDescending(x => x.RecordedAt).FirstOrDefault(x => x.RecordedAt >= team.AirSupplyStartedAt.Value);
        if (latest is null) return null;

        var elapsedMinutes = (latest.RecordedAt - team.AirSupplyStartedAt.Value).TotalMinutes;
        var consumed = Math.Max(0, member.InitialPressureBar.Value - latest.PressureBar);
        if (elapsedMinutes < 0.5 || consumed <= 0) return null;
        return consumed / elapsedMinutes;
    }

    private async Task ChangeStatusAsync(Guid teamId, BreathingTeamStatus status, string eventType, Action<BreathingTeam> mutate)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var team = await db.BreathingTeams.SingleAsync(x => x.Id == teamId);
        mutate(team);
        team.Status = status;
        team.Version++;
        team.LastModifiedAt = DateTimeOffset.Now;
        AddEvent(db, team, eventType, new { Status = status.ToString(), team.Version });
        await db.SaveChangesAsync();
        notifier.Notify(team.IncidentId);
    }

    private static PressureReading NewPressure(BreathingTeam team, Guid firefighterId, int pressure, InformationSource source) => new()
    {
        Id = Guid.NewGuid(), IncidentId = team.IncidentId, TeamId = team.Id, FirefighterId = firefighterId,
        PressureBar = pressure, RecordedAt = DateTimeOffset.Now, DeviceId = DeviceId, Source = source
    };

    private static void AddEvent(FireOpsDbContext db, BreathingTeam team, string eventType, object payload)
    {
        db.IncidentEvents.Add(new IncidentEvent
        {
            Id = Guid.NewGuid(), IncidentId = team.IncidentId, TeamId = team.Id, DeviceId = DeviceId,
            OccurredAt = DateTimeOffset.Now, CreatedAt = DateTimeOffset.UtcNow, EventType = eventType,
            PayloadJson = JsonSerializer.Serialize(payload)
        });
    }

    private static (Guid? Id, string? Name) ExtractEmergencyPerson(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return (null, null);
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            Guid? id = null;
            string? name = null;
            if (doc.RootElement.TryGetProperty("FirefighterId", out var idElement) && idElement.ValueKind == JsonValueKind.String && idElement.TryGetGuid(out var parsed))
                id = parsed;
            if (doc.RootElement.TryGetProperty("PersonName", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
                name = nameElement.GetString();
            return (id, string.IsNullOrWhiteSpace(name) ? null : name);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static int? ExtractSafetyBagPressure(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            return doc.RootElement.TryGetProperty("PressureBar", out var pressure) && pressure.TryGetInt32(out var value) ? value : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractSafetyBagNote(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("Note", out var note) || note.ValueKind != JsonValueKind.String) return null;
            var value = note.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractEmergencyNote(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("Note", out var note) || note.ValueKind == JsonValueKind.Null) return null;
            var value = note.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void ValidatePressure(int pressure)
    {
        if (pressure is < 0 or > 400)
            throw new ArgumentOutOfRangeException(nameof(pressure), "Der Flaschendruck muss zwischen 0 und 400 bar liegen.");
    }
}

