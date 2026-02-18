using CIIP.Backend.Data;
using CIIP.Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace CIIP.Backend.GraphQL.Queries;

[ExtendObjectType(typeof(Query))]
public class ProfileQuery
{
    public async Task<UserAccount?> GetTenantProfile(
        Guid userId,
        [Service] CiipDbContext db)
    {
        return await db.UserAccounts
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.UserId == userId);
    }

    public async Task<List<Plant>> GetPlants(
        Guid tenantId,
        [Service] CiipDbContext db)
    {
        return await db.Plants
            .Where(p => p.TenantId == tenantId)
            .ToListAsync();
    }

    public async Task<List<Machine>> GetMachines(
        Guid plantId,
        [Service] CiipDbContext db)
    {
        return await db.Machines
            .Where(m => m.PlantId == plantId)
            .ToListAsync();
    }
}
