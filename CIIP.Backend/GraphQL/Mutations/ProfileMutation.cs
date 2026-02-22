using CIIP.Backend.Entities;
using CIIP.Backend.Services;
using HotChocolate.Authorization;
using System.Security.Claims;

namespace CIIP.Backend.GraphQL.Mutations;

[ExtendObjectType(typeof(Mutation))]
public class ProfileMutation
{
    // ======================================================
    // UPDATE PROFILE
    // ======================================================

    [Authorize]
    public async Task<UserAccount?> UpdateProfile(
        ClaimsPrincipal user,
        string email,
        [Service] ProfileService service)
    {
        var userId = Guid.Parse(
            user.FindFirst("userId")!.Value
        );

        return await service.UpdateProfile(userId, email);
    }

    // ======================================================
    // CHANGE PASSWORD
    // ======================================================

    [Authorize]
    public async Task<bool> ChangePassword(
        ClaimsPrincipal user,
        string newPassword,
        [Service] ProfileService service)
    {
        var userId = Guid.Parse(
            user.FindFirst("userId")!.Value
        );

        return await service.ChangePassword(userId, newPassword);
    }

    // ======================================================
    // UPDATE TENANT NAME
    // ======================================================

    [Authorize]
    public async Task<Tenant?> UpdateTenantName(
        ClaimsPrincipal user,
        string tenantName,
        [Service] ProfileService service)
    {
        var tenantId = Guid.Parse(
            user.FindFirst("tenantId")!.Value
        );

        return await service.UpdateTenantName(tenantId, tenantName);
    }
}
