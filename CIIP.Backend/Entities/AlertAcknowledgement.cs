using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CIIP.Backend.Entities;

[Table("alert_acknowledgement", Schema = "ciip")]
public class AlertAcknowledgement
{
    [Key]
    [Column("acknowledgement_id")]
    public Guid AcknowledgementId { get; set; }

    [Column("alert_id")]
    public Guid AlertId { get; set; }

    [Column("technician_name")]
    public string TechnicianName { get; set; } = default!;

    [Column("reason")]
    public string Reason { get; set; } = default!;

    [Column("action_taken")]
    public string? ActionTaken { get; set; }

    [Column("acknowledged_at")]
    public DateTime AcknowledgedAt { get; set; }

    // Navigation
    public AlertEvent? AlertEvent { get; set; }
}
