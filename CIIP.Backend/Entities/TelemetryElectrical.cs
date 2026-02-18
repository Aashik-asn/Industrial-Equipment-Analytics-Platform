using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CIIP.Backend.Entities;

[Table("telemetry_electrical", Schema = "ciip")]
public class TelemetryElectrical
{
    [Key]
    [Column("ingestion_id")]
    public long IngestionId { get; set; }

    [Column("r_voltage")] public decimal? RVoltage { get; set; }
    [Column("y_voltage")] public decimal? YVoltage { get; set; }
    [Column("b_voltage")] public decimal? BVoltage { get; set; }
    [Column("r_current")] public decimal? RCurrent { get; set; }
    [Column("y_current")] public decimal? YCurrent { get; set; }
    [Column("b_current")] public decimal? BCurrent { get; set; }
    [Column("frequency")] public decimal? Frequency { get; set; }
    [Column("power_factor")] public decimal? PowerFactor { get; set; }


    public TelemetryIngestion? Ingestion { get; set; }

}
