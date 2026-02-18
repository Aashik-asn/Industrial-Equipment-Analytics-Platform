using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CIIP.Backend.Entities;

[Table("machine", Schema = "ciip")]
public class Machine
{
    [Key]
    [Column("machine_id")]
    public Guid MachineId { get; set; }

    [Column("plant_id")]
    public Guid PlantId { get; set; }

    [Column("machine_code")]
    public string? MachineCode { get; set; }

    [Column("machine_name")]
    public string? MachineName { get; set; }

    [Column("machine_type")]
    public string? MachineType { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public Plant? Plant { get; set; }

    public ICollection<AlertEvent>? Alerts { get; set; }
    public ICollection<TelemetryIngestion>? Telemetry { get; set; }
    //public ICollection<MachineHealth>? HealthRecords { get; set; }

    public ICollection<EndpointMachineMap>? EndpointMaps { get; set; }


}
