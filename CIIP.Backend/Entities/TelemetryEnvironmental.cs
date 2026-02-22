using CIIP.Backend.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("telemetry_environmental", Schema = "ciip")]
public class TelemetryEnvironmental
{
    [Key]
    [Column("ingestion_id")]
    public long IngestionId { get; set; }

    [Column("temperature")]
    public decimal? Temperature { get; set; }

    [Column("humidity")]
    public decimal? Humidity { get; set; }
    [Column("flowrate", TypeName = "double precision")]
    public decimal? Flowrate { get; set; }
    [Column("pressure", TypeName = "double precision")]
    public decimal? Pressure { get; set; }


    // 🔗 LINK TO PARENT TABLE
    public TelemetryIngestion? Ingestion { get; set; }
}
