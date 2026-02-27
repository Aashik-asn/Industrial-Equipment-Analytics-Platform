using CIIP.Backend.Entities;
using CIIP.Backend.DTOs;
using CIIP.Backend.Services;

namespace CIIP.Backend.GraphQL.Mutations;

[ExtendObjectType(typeof(Mutation))]
public class AuthMutation
{
    // ======================================================
    // REGISTER TENANT (NO CHANGE)
    // ======================================================
    public async Task<Tenant> RegisterTenant(
        string tenantName,
        string email,
        string password,
        [Service] AuthService service)
    {
        return await service.Register(tenantName, email, password);
    }

    // ======================================================
    // LOGIN WITH JWT TOKEN
    // ======================================================
    public async Task<LoginResponse?> Login(
    string email,
    string password,
    [Service] AuthService service,
    [Service] JwtService jwtService)
    {
        var user = await service.Login(email, password);

        if (user == null)
            return null;

        var token = jwtService.GenerateToken(user);

        return new LoginResponse
        {
            Token = token,
            UserId = user.UserId,
            TenantId = user.TenantId,
            Role = user.Role ?? "USER",
            TenantName = user.Tenant?.TenantName ?? ""   // ✅ Added
        };
    }
}
