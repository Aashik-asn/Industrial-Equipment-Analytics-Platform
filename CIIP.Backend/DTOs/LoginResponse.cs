namespace CIIP.Backend.DTOs;

public class LoginResponse
{
    public string Token { get; set; } = default!;
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string Role { get; set; } = default!;
    public string TenantName { get; set; } = default!;
}
