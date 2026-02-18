using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CIIP.Backend.Entities;

[Table("plant", Schema = "ciip")]
public class Plant
{
    [Key]
    [Column("plant_id")]
    public Guid PlantId { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("plant_code")]
    public string? PlantCode { get; set; }

    [Column("plant_name")]
    public string? PlantName { get; set; }

    [Column("city")]
    public string? City { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public Tenant? Tenant { get; set; }
    public ICollection<Machine>? Machines { get; set; }
    public ICollection<Gateway>? Gateways { get; set; }

}
