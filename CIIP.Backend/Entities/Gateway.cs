using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CIIP.Backend.Entities;

[Table("gateway", Schema = "ciip")]
public class Gateway
{
    [Key]
    [Column("gateway_id")]
    public Guid GatewayId { get; set; }

    [Column("plant_id")]
    public Guid PlantId { get; set; }

    [Column("gateway_code")]
    public string? GatewayCode { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("last_seen")]
    public DateTime? LastSeen { get; set; }

    public Plant? Plant { get; set; }
    public ICollection<DeviceEndpoint>? Endpoints { get; set; }

    public ICollection<TelemetryIngestion>? Telemetry { get; set; }


}
