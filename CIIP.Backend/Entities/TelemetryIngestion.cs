using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CIIP.Backend.Entities;

[Table("telemetry_ingestion", Schema = "ciip")]
public class TelemetryIngestion
{
    [Key]
    [Column("ingestion_id")]
    public long IngestionId { get; set; }

    [Column("machine_id")]
    public Guid MachineId { get; set; }

    [Column("gateway_id")]
    public Guid GatewayId { get; set; }

    [Column("endpoint_id")]
    public Guid EndpointId { get; set; }

    [Column("recorded_at")]
    public DateTime RecordedAt { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    public Machine? Machine { get; set; }
    public Gateway? Gateway { get; set; }
    public DeviceEndpoint? Endpoint { get; set; }

    public TelemetryElectrical? Electrical { get; set; }
    public TelemetryPower? Power { get; set; }
    public TelemetryEnergy? Energy { get; set; }
    public TelemetryEnvironmental? Environmental { get; set; }
    public TelemetryMechanical? Mechanical { get; set; }

}
