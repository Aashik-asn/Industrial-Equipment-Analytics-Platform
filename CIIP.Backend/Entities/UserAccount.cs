using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CIIP.Backend.Entities;

[Table("user_account", Schema = "ciip")]
public class UserAccount
{
    [Key]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("password_hash")]
    public string? PasswordHash { get; set; }

    [Column("role")]
    public string? Role { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public Tenant? Tenant { get; set; }

}
