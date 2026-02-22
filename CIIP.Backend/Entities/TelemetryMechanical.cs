using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CIIP.Backend.Entities;

[Table("telemetry_mechanical", Schema = "ciip")]
public class TelemetryMechanical
{
    [Key]
    [Column("ingestion_id")]
    public long IngestionId { get; set; }

    [Column("vibration_x", TypeName = "double precision")] public decimal? VibrationX { get; set; }
    [Column("vibration_y", TypeName = "double precision")] public decimal? VibrationY { get; set; }
    [Column("vibration_z", TypeName = "double precision")] public decimal? VibrationZ { get; set; }

    [Column("rpm")] public decimal? Rpm { get; set; }

    public TelemetryIngestion? Ingestion { get; set; }

}
