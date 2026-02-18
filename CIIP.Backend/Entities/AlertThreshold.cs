using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CIIP.Backend.Entities;

[Table("alert_threshold", Schema = "ciip")]
public class AlertThreshold
{
    [Key]
    [Column("threshold_id")]
    public Guid ThresholdId { get; set; }

    [Column("tenant_id")]
    public Guid? TenantId { get; set; }

    [Column("parameter")]
    public string Parameter { get; set; } = default!;

    [Column("warning_value")]
    public decimal WarningValue { get; set; }

    [Column("critical_value")]
    public decimal CriticalValue { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Column("machine_type")]
    public string? MachineType { get; set; }


    public ICollection<AlertEvent>? Alerts { get; set; }

}
