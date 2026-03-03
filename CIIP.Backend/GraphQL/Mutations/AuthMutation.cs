using CIIP.Backend.Entities;
using CIIP.Backend.DTOs;
using CIIP.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace CIIP.Backend.GraphQL.Mutations;

[ExtendObjectType(typeof(Mutation))]
public class AuthMutation
{
    // ======================================================
    // REGISTER TENANT (NO CHANGE)
    // ======================================================
    public async Task<Tenant> RegisterTenant(
    string tenantName,
    string firstName,
    string lastName,
    string email,
    string password,
    [Service] AuthService service)
    {
        return await service.Register(
            tenantName,
            firstName,
            lastName,
            email,
            password);
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
            TenantName = user.Tenant?.TenantName ?? "",
            FirstName = user.FirstName,
            LastName = user.LastName
        };
    }
    [Authorize(Roles = "ADMIN")]
    public async Task<bool> CreateUser(
    CreateUserInput input,
    ClaimsPrincipal claims,
    [Service] AuthService service)
    {
        var tenantId = Guid.Parse(claims.FindFirst("tenantId")!.Value);

        await service.CreateUser(tenantId, input);

        return true;
    }
    [Authorize]
    public async Task<bool> UpdateUserName(
    UpdateUserNameInput input,
    ClaimsPrincipal claims,
    [Service] AuthService service)
    {
        var userIdClaim = claims.FindFirst("userId");

        if (userIdClaim == null)
            throw new Exception("Invalid token: userId claim missing");

        var userId = Guid.Parse(userIdClaim.Value);

        await service.UpdateUserName(userId, input);

        return true;
    }
}
