using CIIP.Backend.Data;
using CIIP.Backend.Entities;
using Microsoft.EntityFrameworkCore;
using HotChocolate.Authorization;
using System.Security.Claims;

namespace CIIP.Backend.GraphQL.Queries;

[ExtendObjectType(typeof(Query))]
public class ProfileQuery
{
    // ======================================================
    // TENANT PROFILE
    // ======================================================

    [Authorize]
    public async Task<UserAccount?> TenantProfile(
        ClaimsPrincipal user,
        [Service] CiipDbContext db)
    {

        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier);

        if (idClaim == null)
            throw new Exception("UserId claim missing in JWT.");

        var userId = Guid.Parse(idClaim.Value);



        return await db.UserAccounts
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.UserId == userId);
    }

    // ======================================================
    // PLANTS (TENANT AUTO FROM JWT)
    // ======================================================

    [Authorize]
    public async Task<List<Plant>> Plants(
        ClaimsPrincipal user,
        [Service] CiipDbContext db)
    {
        var tenantId = Guid.Parse(
            user.FindFirst("tenantId")!.Value
        );

        return await db.Plants
            .Where(p => p.TenantId == tenantId)
            .ToListAsync();
    }

    // ======================================================
    // MACHINES (PLANT FILTER ONLY)
    // ======================================================

    [Authorize]
    public async Task<List<Machine>> Machines(
        Guid plantId,
        ClaimsPrincipal user,
        [Service] CiipDbContext db)
    {
        var tenantId = Guid.Parse(
            user.FindFirst("tenantId")!.Value
        );

        // ⭐ extra safety — verify plant belongs to tenant
        return await db.Machines
            .Where(m =>
                m.PlantId == plantId &&
                m.Plant!.TenantId == tenantId)
            .ToListAsync();
    }
}
