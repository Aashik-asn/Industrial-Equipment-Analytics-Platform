using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CIIP.Backend.Entities;

[Table("telemetry_power", Schema = "ciip")]
public class TelemetryPower
{
    [Key]
    [Column("ingestion_id")]
    public long IngestionId { get; set; }

    [Column("active_power_kw")] public decimal? ActivePowerKw { get; set; }
    [Column("reactive_power_kvar")] public decimal? ReactivePowerKvar { get; set; }
    [Column("apparent_power_kva")] public decimal? ApparentPowerKva { get; set; }

    public TelemetryIngestion? Ingestion { get; set; }

}
