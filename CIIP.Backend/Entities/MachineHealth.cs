using System.ComponentModel.DataAnnotations.Schema;

namespace CIIP.Backend.Entities;

[Table("machine_health", Schema = "ciip")]
public class MachineHealth
{
    [Column("machine_id")]
    public Guid MachineId { get; set; }

    [Column("recorded_at")]
    public DateTime RecordedAt { get; set; }

    [Column("health_score")]
    public int HealthScore { get; set; }

    [Column("avg_load")]
    public decimal AvgLoad { get; set; }

    [Column("runtime_hours")]
    public decimal RuntimeHours { get; set; }

    public Machine? Machine { get; set; }
    //public AlertAcknowledgement? Acknowledgement { get; set; }

}
