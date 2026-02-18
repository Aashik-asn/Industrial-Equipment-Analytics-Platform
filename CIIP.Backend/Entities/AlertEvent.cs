using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CIIP.Backend.Entities;

[Table("alert_event", Schema = "ciip")]
public class AlertEvent
{
    [Key]
    [Column("alert_id")]
    public Guid AlertId { get; set; }

    [Column("machine_id")]
    public Guid MachineId { get; set; }

    [Column("parameter")]
    public string? Parameter { get; set; }

    [Column("actual_value")]
    public decimal? ActualValue { get; set; }

    [Column("severity")]
    public string? Severity { get; set; }

    [Column("alert_status")]
    public string? AlertStatus { get; set; }

    [Column("generated_at")]
    public DateTime GeneratedAt { get; set; }

    [Column("threshold_id")]
    public Guid? ThresholdId { get; set; }

    public Machine? Machine { get; set; }
    public AlertAcknowledgement? Acknowledgement { get; set; }

    [ForeignKey(nameof(ThresholdId))]
    public AlertThreshold? AlertThreshold { get; set; }


}
