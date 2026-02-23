using CIIP.Backend.Data;
using CIIP.Backend.DTOs.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace CIIP.Backend.Services;

public class DashboardService
{
    private readonly CiipDbContext _db;

    public DashboardService(CiipDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardResponse> GetDashboard(
        Guid tenantId,
        Guid? plantId,
        DateTime? from,
        DateTime? to)
    {
        var result = new DashboardResponse();

        // ====================================================
        // ⭐ PLANT + MACHINE FILTER
        // ====================================================
        var plants = _db.Plants
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId);

        if (plantId != null)
            plants = plants.Where(p => p.PlantId == plantId);

        var plantIds = plants.Select(p => p.PlantId);

        var machines = _db.Machines
            .AsNoTracking()
            .Where(m => plantIds.Contains(m.PlantId));

        var machineIds = await machines
            .Select(m => m.MachineId)
            .ToListAsync();

        // ====================================================
        // ⭐ ACTIVE MACHINES CARD
        // ====================================================
        result.TotalActiveMachines =
            await machines.CountAsync(x => x.Status == "RUNNING");

        // ====================================================
        // ⭐ ACTIVE ALERTS CARD
        // ====================================================
        result.ActiveAlerts = await _db.AlertEvents
            .AsNoTracking()
            .Where(a => machineIds.Contains(a.MachineId))
            .Select(a => a.AlertId)
            .Distinct()
            .CountAsync();

        // ====================================================
        // ⭐ HEALTH BASE QUERY (CORE SOURCE)
        // ====================================================
        var healthQuery = _db.MachineHealth
            .AsNoTracking()
            .Where(h => machineIds.Contains(h.MachineId));

        if (from != null)
            healthQuery = healthQuery.Where(h => h.RecordedAt >= from);

        if (to != null)
            healthQuery = healthQuery.Where(h => h.RecordedAt <= to);

        // ====================================================
        // ⭐ AVG EFFICIENCY CARD
        // ====================================================
        var latestHealthValues = await healthQuery
            .GroupBy(h => h.MachineId)
            .Select(g => g
                .OrderByDescending(x => x.RecordedAt)
                .Select(x => (double?)x.HealthScore)
                .FirstOrDefault())
            .ToListAsync();

        result.AvgEfficiency =
            latestHealthValues.Where(x => x != null).Average() ?? 0;

        // ====================================================
        // ⭐ ALERT DONUT
        // ====================================================
        result.AlertDistribution = await _db.AlertEvents
            .AsNoTracking()
            .Where(a => machineIds.Contains(a.MachineId))
            .GroupBy(x => x.Severity)
            .Select(g => new AlertSeverityDto
            {
                Severity = g.Key!,
                Count = g.Count()
            })
            .ToListAsync();

        // ====================================================
        // ⭐ LAST 12 HEALTH RECORDS (24HRS BASE)
        // ====================================================
        var last12Health = await healthQuery
            .OrderByDescending(h => h.RecordedAt)
            .Take(12)
            .ToListAsync();

        // ====================================================
        // ⭐ OEE TREND (INDUSTRIAL DERIVED)
        // ====================================================
        result.OeeTrend = last12Health
            .OrderBy(h => h.RecordedAt)
            .Select(h => new OeePoint
            {
                Time = h.RecordedAt,

                // Availability → runtime progression
                Availability = (double)Math.Min(100m, h.RuntimeHours * 10m),

                // Performance → electrical load behaviour
                Performance = (double)Math.Min(100m, h.AvgLoad * 4m),

                // Quality → machine health degradation
                Quality = (double)h.HealthScore
            })
            .ToList();

        // ====================================================
        // ⭐ ENERGY TREND (LAST 12)
        // ====================================================
        var energyQuery =
            from e in _db.TelemetryEnergy.AsNoTracking()
            join i in _db.TelemetryIngestions.AsNoTracking()
                on e.IngestionId equals i.IngestionId
            where machineIds.Contains(i.MachineId)
            select new { e, i };

        if (from != null)
            energyQuery = energyQuery.Where(x => x.i.RecordedAt >= from);

        if (to != null)
            energyQuery = energyQuery.Where(x => x.i.RecordedAt <= to);

        result.EnergyTrend = await energyQuery
            .OrderByDescending(x => x.i.RecordedAt)
            .Take(12)
            .Select(x => new EnergyPoint
            {
                Time = x.i.RecordedAt,
                Energy = (double)x.e.EnergyImportKwh
            })
            .OrderBy(x => x.Time)
            .ToListAsync();

        // ====================================================
        // ⭐ PRODUCTION VS TARGET (LOAD-BASED INDUSTRIAL)
        // ====================================================
        result.ProductionTrend = last12Health
            .OrderBy(h => h.RecordedAt)
            .Select(h => new ProductionPoint
            {
                Time = h.RecordedAt,

                // Actual production proportional to load
                Actual = (double)(h.AvgLoad * 10m),

                // Target slightly above load capability
                Target = (double)(h.AvgLoad * 11m)
            })
            .ToList();

        // ====================================================
        // ⭐ PLANT CARDS (EFFICIENCY PER PLANT)
        // ====================================================
        result.Plants = await plants
            .Select(p => new PlantCardDto
            {
                PlantId = p.PlantId,
                PlantName = p.PlantName,
                Machines = p.Machines.Count(),

                Efficiency =
                    _db.MachineHealth
                        .Where(h => p.Machines
                            .Select(m => m.MachineId)
                            .Contains(h.MachineId))
                        .GroupBy(h => h.MachineId)
                        .Select(g => g
                            .OrderByDescending(x => x.RecordedAt)
                            .Select(x => (double?)x.HealthScore)
                            .FirstOrDefault())
                        .Average() ?? 0
            })
            .ToListAsync();

        return result;
    }
}
