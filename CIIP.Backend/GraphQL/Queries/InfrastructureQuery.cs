using CIIP.Backend.Data;
using CIIP.Backend.Entities;
using Microsoft.EntityFrameworkCore;
using HotChocolate.Authorization;
using System.Security.Claims;

namespace CIIP.Backend.GraphQL.Queries;

[ExtendObjectType(typeof(Query))]
public class InfrastructureQuery
{
    // ======================================================
    // GATEWAYS
    // ======================================================

    [Authorize]
    public async Task<List<Gateway>> Gateways(
        Guid plantId,
        ClaimsPrincipal user,
        [Service] CiipDbContext db)
    {
        var tenantId = Guid.Parse(
            user.FindFirst("tenantId")!.Value
        );

        // Optional: Ensure plant belongs to tenant
        var isAuthorizedPlant = await db.Plants
            .AnyAsync(p => p.PlantId == plantId && p.TenantId == tenantId);

        if (!isAuthorizedPlant)
            throw new Exception("Unauthorized access to this plant's gateways.");

        return await db.Gateways
            .Where(g => g.PlantId == plantId)
            .ToListAsync();
    }

    // ======================================================
    // DEVICE ENDPOINTS
    // ======================================================

    [Authorize]
    public async Task<List<DeviceEndpoint>> DeviceEndpoints(
        Guid gatewayId,
        ClaimsPrincipal user,
        [Service] CiipDbContext db)
    {
        var tenantId = Guid.Parse(
            user.FindFirst("tenantId")!.Value
        );

        // Optional: Ensure gateway->plant belongs to tenant
        var isAuthorizedGateway = await db.Gateways
            .Include(g => g.Plant)
            .AnyAsync(g => g.GatewayId == gatewayId && g.Plant!.TenantId == tenantId);

        if (!isAuthorizedGateway)
            throw new Exception("Unauthorized access to this gateway's endpoints.");

        return await db.DeviceEndpoints
            .Where(e => e.GatewayId == gatewayId)
            .ToListAsync();
    }
}
