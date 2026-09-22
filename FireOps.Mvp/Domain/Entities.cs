namespace FireOps.Mvp.Domain;

public sealed class Incident
{
    public Guid Id { get; set; }
    public string? ExternalId { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Address { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public IncidentStatus Status { get; set; }
}

public sealed class Unit
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public string CallSign { get; set; } = string.Empty;
    public string? VehicleType { get; set; }
    public UnitStatus Status { get; set; }
}

public sealed class Firefighter
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string DisplayName => string.Join(" ", new[] { FirstName, LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

public sealed class Qualification
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class FirefighterQualification
{
    public Guid Id { get; set; }
    public Guid FirefighterId { get; set; }
    public Guid QualificationId { get; set; }
}

public sealed class BreathingTeam
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public Guid UnitId { get; set; }
    public string Name { get; set; } = string.Empty;
    public BreathingTeamRole Role { get; set; }
    public BreathingTeamStatus Status { get; set; }
    public Guid MonitoringStationId { get; set; }
    public string? Mission { get; set; }
    public string? Location { get; set; }
    public string? AccessRoute { get; set; }
    public int ExpectedDurationMinutes { get; set; } = 30;
    public DateTimeOffset? AirSupplyStartedAt { get; set; }
    public DateTimeOffset? TargetReachedAt { get; set; }
    public DateTimeOffset? ReturnStartedAt { get; set; }
    public DateTimeOffset? ReturnedAt { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
    public long Version { get; set; }
}

public sealed class BreathingTeamMember
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid FirefighterId { get; set; }
    public int Position { get; set; }
    public int? InitialPressureBar { get; set; }
}

public sealed class MonitoringStation
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public MonitoringStationType Type { get; set; }
    public Guid? UnitId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PressureReading
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public Guid TeamId { get; set; }
    public Guid FirefighterId { get; set; }
    public int PressureBar { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? UserId { get; set; }
    public InformationSource Source { get; set; }
    public bool IsCorrection { get; set; }
    public Guid? CorrectsReadingId { get; set; }
}

public sealed class RadioContact
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public Guid TeamId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public RadioContactType Type { get; set; }
    public RadioContactResult Result { get; set; }
    public string? Note { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? UserId { get; set; }
}

public sealed class MonitoringTransfer
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public Guid TeamId { get; set; }
    public Guid FromStationId { get; set; }
    public Guid ToStationId { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public TransferStatus Status { get; set; }
}

public sealed class IncidentEvent
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? UserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
}
