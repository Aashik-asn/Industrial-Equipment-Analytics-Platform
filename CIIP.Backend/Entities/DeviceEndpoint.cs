using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CIIP.Backend.Entities;

[Table("endpoint", Schema = "ciip")]
public class DeviceEndpoint
{
    [Key]
    [Column("endpoint_id")]
    public Guid EndpointId { get; set; }

    [Column("gateway_id")]
    public Guid GatewayId { get; set; }

    [Column("endpoint_type")]
    public string? EndpointType { get; set; }

    [Column("protocol")]
    public string? Protocol { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    public Gateway? Gateway { get; set; }
    public ICollection<EndpointMachineMap>? MachineMaps { get; set; }
    public ICollection<TelemetryIngestion>? Telemetry { get; set; }


}
