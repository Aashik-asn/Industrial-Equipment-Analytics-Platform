using CIIP.Backend.Data;
using CIIP.Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace CIIP.Backend.Services;

public class PlantService
{
    private readonly CiipDbContext _db;

    public PlantService(CiipDbContext db)
    {
        _db = db;
    }

    public async Task<Plant> UpsertPlant(
        Guid tenantId,
        string plantCode,
        string plantName,
        string city)
    {
        var existing = await _db.Plants
            .FirstOrDefaultAsync(p =>
                p.TenantId == tenantId &&
                p.PlantCode == plantCode);

        if (existing != null)
        {
            existing.PlantName = plantName;
            existing.City = city;
            await _db.SaveChangesAsync();
            return existing;
        }

        var plant = new Plant
        {
            PlantId = Guid.NewGuid(),
            TenantId = tenantId,
            PlantCode = plantCode,
            PlantName = plantName,
            City = city,
            Status = "A",
            CreatedAt = DateTime.UtcNow
        };

        _db.Plants.Add(plant);
        await _db.SaveChangesAsync();

        return plant;
    }
}
