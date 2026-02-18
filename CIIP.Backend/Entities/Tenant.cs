using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CIIP.Backend.Entities;

[Table("tenant", Schema = "ciip")]
public class Tenant
{
    [Key]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("tenant_name")]
    public string? TenantName { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("password_hash")]
    public string? PasswordHash { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public ICollection<Plant>? Plants { get; set; }
    public ICollection<UserAccount>? Users { get; set; }


}
