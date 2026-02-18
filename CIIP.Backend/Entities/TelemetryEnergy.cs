using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CIIP.Backend.Entities;

[Table("telemetry_energy", Schema = "ciip")]
public class TelemetryEnergy
{
    [Key]
    [Column("ingestion_id")]
    public long IngestionId { get; set; }

    [Column("energy_import_kwh")] public decimal? EnergyImportKwh { get; set; }
    [Column("energy_export_kwh")] public decimal? EnergyExportKwh { get; set; }
    [Column("energy_import_kvah")] public decimal? EnergyImportKvah { get; set; }

    public TelemetryIngestion? Ingestion { get; set; }

}
