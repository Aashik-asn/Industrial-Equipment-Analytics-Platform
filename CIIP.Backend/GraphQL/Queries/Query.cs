using CIIP.Backend.Data;
using CIIP.Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace CIIP.Backend.GraphQL.Queries;

public partial class Query
{
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

    public async Task<List<AlertEvent>> GetAlerts(
        Guid tenantId,
        [Service] CiipDbContext db)
    {
        return await db.AlertEvents
            .Include(a => a.Machine)
            .ThenInclude(m => m.Plant)
            .Where(a => a.Machine!.Plant!.TenantId == tenantId)
            .ToListAsync();
    }
}
