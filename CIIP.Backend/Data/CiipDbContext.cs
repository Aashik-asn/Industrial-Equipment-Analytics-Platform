using CIIP.Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace CIIP.Backend.Data;

public class CiipDbContext : DbContext
{
    public CiipDbContext(DbContextOptions<CiipDbContext> options)
        : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Plant> Plants => Set<Plant>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<Gateway> Gateways => Set<Gateway>();
    public DbSet<DeviceEndpoint> DeviceEndpoints => Set<DeviceEndpoint>();

    public DbSet<EndpointMachineMap> EndpointMachineMaps => Set<EndpointMachineMap>();

    public DbSet<TelemetryIngestion> TelemetryIngestions => Set<TelemetryIngestion>();
    public DbSet<TelemetryElectrical> TelemetryElectrical => Set<TelemetryElectrical>();
    public DbSet<TelemetryPower> TelemetryPower => Set<TelemetryPower>();
    public DbSet<TelemetryEnergy> TelemetryEnergy => Set<TelemetryEnergy>();
    public DbSet<TelemetryEnvironmental> TelemetryEnvironmental => Set<TelemetryEnvironmental>();
    public DbSet<TelemetryMechanical> TelemetryMechanical => Set<TelemetryMechanical>();

    public DbSet<AlertEvent> AlertEvents => Set<AlertEvent>();
    public DbSet<AlertAcknowledgement> AlertAcknowledgements => Set<AlertAcknowledgement>();
    public DbSet<MachineHealth> MachineHealth => Set<MachineHealth>();

    public DbSet<AlertThreshold> AlertThresholds => Set<AlertThreshold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ✅ Composite Key
        modelBuilder.Entity<EndpointMachineMap>()
            .HasKey(x => new { x.EndpointId, x.MachineId });

        // ✅ MachineHealth has no PK
        modelBuilder.Entity<MachineHealth>()
            .HasKey(x => new { x.MachineId, x.RecordedAt });


        // ✅ Telemetry 1-to-1 mappings
        modelBuilder.Entity<TelemetryElectrical>()
            .HasOne(t => t.Ingestion)
            .WithOne(i => i.Electrical)
            .HasForeignKey<TelemetryElectrical>(t => t.IngestionId);

        modelBuilder.Entity<TelemetryPower>()
            .HasOne(t => t.Ingestion)
            .WithOne(i => i.Power)
            .HasForeignKey<TelemetryPower>(t => t.IngestionId);

        modelBuilder.Entity<TelemetryEnergy>()
            .HasOne(t => t.Ingestion)
            .WithOne(i => i.Energy)
            .HasForeignKey<TelemetryEnergy>(t => t.IngestionId);

        modelBuilder.Entity<TelemetryEnvironmental>()
            .HasOne(t => t.Ingestion)
            .WithOne(i => i.Environmental)
            .HasForeignKey<TelemetryEnvironmental>(t => t.IngestionId);

        modelBuilder.Entity<TelemetryMechanical>()
            .HasOne(t => t.Ingestion)
            .WithOne(i => i.Mechanical)
            .HasForeignKey<TelemetryMechanical>(t => t.IngestionId);

        // ✅ Alert acknowledgement 1-to-1
        // ✅ Proper 1-to-1 mapping between AlertEvent and AlertAcknowledgement

        modelBuilder.Entity<AlertAcknowledgement>()
            .HasKey(a => a.AcknowledgementId);

        modelBuilder.Entity<AlertAcknowledgement>()
            .HasOne(a => a.AlertEvent)
            .WithOne(e => e.Acknowledgement)
            .HasForeignKey<AlertAcknowledgement>(a => a.AlertId)
            .HasPrincipalKey<AlertEvent>(e => e.AlertId)
            .OnDelete(DeleteBehavior.Cascade);


        modelBuilder.Entity<AlertEvent>()
            .HasOne(a => a.AlertThreshold)
            .WithMany(t => t.Alerts)
            .HasForeignKey(a => a.ThresholdId)
            .HasConstraintName("fk_alert_event_threshold")
            .OnDelete(DeleteBehavior.Restrict);



        modelBuilder.Entity<Plant>()
            .HasOne(p => p.Tenant)
            .WithMany(t => t.Plants)
            .HasForeignKey(p => p.TenantId);
        modelBuilder.Entity<Machine>()
            .HasOne(m => m.Plant)
            .WithMany(p => p.Machines)
            .HasForeignKey(m => m.PlantId);

        modelBuilder.Entity<Gateway>()
            .HasOne(g => g.Plant)
            .WithMany(p => p.Gateways)
            .HasForeignKey(g => g.PlantId);

        modelBuilder.Entity<DeviceEndpoint>()
            .HasOne(e => e.Gateway)
            .WithMany(g => g.Endpoints)
            .HasForeignKey(e => e.GatewayId);

        modelBuilder.Entity<AlertEvent>()
            .HasOne(a => a.Machine)
            .WithMany(m => m.Alerts)
            .HasForeignKey(a => a.MachineId);

    }
}
