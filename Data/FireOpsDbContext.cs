using FireOps.Domain;
using Microsoft.EntityFrameworkCore;

namespace FireOps.Data;

public sealed class FireOpsDbContext(DbContextOptions<FireOpsDbContext> options) : DbContext(options)
{
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Firefighter> Firefighters => Set<Firefighter>();
    public DbSet<Qualification> Qualifications => Set<Qualification>();
    public DbSet<FirefighterQualification> FirefighterQualifications => Set<FirefighterQualification>();
    public DbSet<BreathingTeam> BreathingTeams => Set<BreathingTeam>();
    public DbSet<BreathingTeamMember> BreathingTeamMembers => Set<BreathingTeamMember>();
    public DbSet<MonitoringStation> MonitoringStations => Set<MonitoringStation>();
    public DbSet<PressureReading> PressureReadings => Set<PressureReading>();
    public DbSet<RadioContact> RadioContacts => Set<RadioContact>();
    public DbSet<MonitoringTransfer> MonitoringTransfers => Set<MonitoringTransfer>();
    public DbSet<IncidentEvent> IncidentEvents => Set<IncidentEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Incident>().HasKey(x => x.Id);
        modelBuilder.Entity<Unit>().HasKey(x => x.Id);
        modelBuilder.Entity<Firefighter>().HasKey(x => x.Id);
        modelBuilder.Entity<Qualification>().HasKey(x => x.Id);
        modelBuilder.Entity<FirefighterQualification>().HasKey(x => x.Id);
        modelBuilder.Entity<BreathingTeam>().HasKey(x => x.Id);
        modelBuilder.Entity<BreathingTeamMember>().HasKey(x => x.Id);
        modelBuilder.Entity<MonitoringStation>().HasKey(x => x.Id);
        modelBuilder.Entity<PressureReading>().HasKey(x => x.Id);
        modelBuilder.Entity<RadioContact>().HasKey(x => x.Id);
        modelBuilder.Entity<MonitoringTransfer>().HasKey(x => x.Id);
        modelBuilder.Entity<IncidentEvent>().HasKey(x => x.Id);

        modelBuilder.Entity<Unit>().HasIndex(x => x.IncidentId);
        modelBuilder.Entity<BreathingTeam>().HasIndex(x => new { x.IncidentId, x.UnitId });
        modelBuilder.Entity<BreathingTeamMember>().HasIndex(x => new { x.TeamId, x.Position }).IsUnique();
        modelBuilder.Entity<PressureReading>().HasIndex(x => new { x.TeamId, x.RecordedAt });
        modelBuilder.Entity<IncidentEvent>().HasIndex(x => new { x.IncidentId, x.OccurredAt });
        modelBuilder.Entity<MonitoringTransfer>().HasIndex(x => new { x.TeamId, x.Status });
    }
}

