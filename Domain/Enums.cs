namespace FireOps.Domain;

public enum IncidentStatus
{
    Active = 10,
    Closed = 20
}

public enum UnitStatus
{
    Available = 0,
    OnScene = 10,
    OutOfService = 20
}

public enum BreathingTeamStatus
{
    Preparing = 0,
    Ready = 10,
    Active = 20,
    TargetReached = 30,
    Returning = 40,
    Returned = 50,
    Completed = 60,
    Emergency = 100
}

public enum BreathingTeamRole
{
    Regular = 0,
    AttackTeam = 10,
    SafetyTeam = 20
}

public enum InformationSource
{
    Manual = 0,
    Radio = 10,
    DirectObservation = 20,
    Automatic = 30,
    Imported = 40
}

public enum RadioContactType
{
    Pressure = 10,
    TargetReached = 20,
    Return = 30,
    Situation = 40,
    Other = 90
}

public enum RadioContactResult
{
    Successful = 10,
    NoAnswer = 20,
    Unclear = 30
}

public enum MonitoringStationType
{
    Unit = 10,
    Central = 20,
    Section = 30
}

public enum TransferStatus
{
    Requested = 10,
    Accepted = 20,
    Rejected = 30,
    Cancelled = 40
}

