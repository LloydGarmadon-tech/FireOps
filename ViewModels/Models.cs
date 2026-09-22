using FireOps.Domain;

namespace FireOps.ViewModels;

public sealed record IncidentListItem(Guid Id, string Keyword, string? Address, DateTimeOffset StartedAt, int UnitCount, int ActiveTeamCount);

public sealed class IncidentOverviewVm
{
    public required Incident Incident { get; init; }
    public required IReadOnlyList<UnitOverviewVm> Units { get; init; }
    public required MonitoringStation CentralStation { get; init; }
}

public sealed class UnitOverviewVm
{
    public required Unit Unit { get; init; }
    public required IReadOnlyList<TeamCardVm> Teams { get; init; }
}

public sealed class TeamCardVm
{
    public required BreathingTeam Team { get; init; }
    public required string UnitCallSign { get; init; }
    public required string MonitoringStationName { get; init; }
    public required IReadOnlyList<MemberPressureVm> Members { get; init; }
    public DateTimeOffset? LastReadingAt { get; init; }
    public DateTimeOffset? EmergencyDeclaredAt { get; init; }
    public Guid? EmergencyFirefighterId { get; init; }
    public string? EmergencyFirefighterName { get; init; }
    public int? SafetyBagPressureBar { get; init; }
    public DateTimeOffset? SafetyBagPressureAt { get; init; }
}

public sealed class TeamDetailsVm
{
    public required BreathingTeam Team { get; init; }
    public required Unit Unit { get; init; }
    public required MonitoringStation MonitoringStation { get; init; }
    public required IReadOnlyList<MemberPressureVm> Members { get; init; }
    public required IReadOnlyList<PressureHistoryVm> PressureHistory { get; init; }
    public required IReadOnlyList<IncidentEvent> Events { get; init; }
    public DateTimeOffset? EmergencyDeclaredAt { get; init; }
    public Guid? EmergencyFirefighterId { get; init; }
    public string? EmergencyFirefighterName { get; init; }
    public string? EmergencyNote { get; init; }
    public required IReadOnlyList<SafetyBagPressureHistoryVm> SafetyBagPressureHistory { get; init; }
    public int? SafetyBagPressureBar => SafetyBagPressureHistory.OrderByDescending(x => x.RecordedAt).FirstOrDefault()?.PressureBar;
    public DateTimeOffset? SafetyBagPressureAt => SafetyBagPressureHistory.OrderByDescending(x => x.RecordedAt).FirstOrDefault()?.RecordedAt;
    public DateTimeOffset? LastReadingAt => PressureHistory.OrderByDescending(x => x.RecordedAt).FirstOrDefault()?.RecordedAt;
}

public sealed class MemberPressureVm
{
    public required Guid FirefighterId { get; init; }
    public required string Name { get; init; }
    public required string FunctionLabel { get; init; }
    public int? InitialPressureBar { get; init; }
    public int? LatestPressureBar { get; init; }
    public DateTimeOffset? LatestPressureAt { get; init; }
    public int? TargetPressureBar { get; init; }
    public int? TurnaroundPressureBar { get; init; }
    public int ReservePressureBar { get; init; } = 50;
    public double? ConsumptionRateBarPerMinute { get; init; }

    public int? OutboundConsumptionBar => InitialPressureBar.HasValue && LatestPressureBar.HasValue
        ? Math.Max(0, InitialPressureBar.Value - LatestPressureBar.Value)
        : null;

    // Vor Erreichen des Einsatzziels ist dies ein laufender Sicherheitswert.
    // Am Einsatzziel wird der individuelle Umkehrdruck festgeschrieben.
    public int? ProvisionalTurnaroundPressureBar => TurnaroundPressureBar.HasValue
        ? null
        : OutboundConsumptionBar is > 0
            ? Math.Min(400, OutboundConsumptionBar.Value * 2 + ReservePressureBar)
            : null;

    public int? EffectiveTurnaroundPressureBar => TurnaroundPressureBar ?? ProvisionalTurnaroundPressureBar;

    public double? MinutesToTurnaround
    {
        get
        {
            if (!LatestPressureBar.HasValue || !EffectiveTurnaroundPressureBar.HasValue ||
                !ConsumptionRateBarPerMinute.HasValue || ConsumptionRateBarPerMinute.Value <= 0)
                return null;

            var pressureMargin = LatestPressureBar.Value - EffectiveTurnaroundPressureBar.Value;
            if (pressureMargin <= 0) return 0;

            // Solange das Einsatzziel noch nicht erreicht ist, steigt der benÃ¶tigte
            // RÃ¼ckwegdruck mit jedem zusÃ¤tzlich verbrauchten bar um 2 bar.
            var divisor = TurnaroundPressureBar.HasValue ? ConsumptionRateBarPerMinute.Value : ConsumptionRateBarPerMinute.Value * 3d;
            return pressureMargin / divisor;
        }
    }

    public bool TurnaroundReached => LatestPressureBar.HasValue && EffectiveTurnaroundPressureBar.HasValue &&
                                     LatestPressureBar.Value <= EffectiveTurnaroundPressureBar.Value;
    public bool EarlyTurnaroundWarning => !TurnaroundReached && MinutesToTurnaround is > 0 and <= 5;
    public bool CriticalReserveReached => LatestPressureBar.HasValue && LatestPressureBar.Value <= ReservePressureBar;
}

public sealed record PressureHistoryVm(Guid Id, Guid FirefighterId, string Name, int PressureBar, DateTimeOffset RecordedAt, InformationSource Source, bool IsCorrection);

public sealed record SafetyBagPressureHistoryVm(Guid EventId, int PressureBar, DateTimeOffset RecordedAt, string? Note);

public sealed class PendingTransferVm
{
    public required MonitoringTransfer Transfer { get; init; }
    public required string TeamName { get; init; }
    public required string UnitCallSign { get; init; }
    public required string FromStation { get; init; }
    public required string ToStation { get; init; }
}

