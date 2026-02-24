using CIIP.Backend.Entities;
using CIIP.Backend.Services;
using HotChocolate.Authorization;
using System.Security.Claims;

namespace CIIP.Backend.GraphQL.Mutations;

[ExtendObjectType(typeof(Mutation))]
public class ProfileMutation
{
    // ======================================================
    // INTERNAL HELPERS (NO NEW FILE NEEDED)
    // ======================================================

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var claim =
            user.FindFirst(ClaimTypes.NameIdentifier) ??
            user.FindFirst("sub") ??
            user.FindFirst("userId");

        if (claim == null)
            throw new Exception("UserId claim missing in token.");

        return Guid.Parse(claim.Value);
    }

    private static Guid GetTenantId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst("tenantId");

        if (claim == null)
            throw new Exception("TenantId claim missing in token.");

        return Guid.Parse(claim.Value);
    }

    // ======================================================
    // UPDATE PROFILE
    // ======================================================

    [Authorize]
    public async Task<UserAccount?> UpdateProfile(
        ClaimsPrincipal user,
        string email,
        [Service] ProfileService service)
    {
        var userId = GetUserId(user);

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
        var userId = GetUserId(user);

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
        var tenantId = GetTenantId(user);

        return await service.UpdateTenantName(tenantId, tenantName);
    }
}