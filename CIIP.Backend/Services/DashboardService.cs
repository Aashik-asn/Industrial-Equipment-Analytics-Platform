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

        // ⭐ PLANTS FILTER (Tenant Based)
        var plants = _db.Plants
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId);

        if (plantId != null)
            plants = plants.Where(p => p.PlantId == plantId);

        var plantIds = plants.Select(p => p.PlantId);

        // ⭐ MACHINES FILTER
        var machines = _db.Machines
            .AsNoTracking()
            .Where(m => plantIds.Contains(m.PlantId));

        // ⭐ MACHINE IDS FIRST (IMPORTANT ORDER)
        var machineIds = await machines
            .Select(m => m.MachineId)
            .ToListAsync();


        // ====================================================
        // ⭐ TOTAL ACTIVE MACHINES
        // ====================================================
        result.TotalActiveMachines =
            await machines.CountAsync(x => x.Status == "RUNNING");


        // ====================================================
        // ⭐ ACTIVE ALERTS
        // ====================================================
        // ⭐ ALERTS (FIXED COUNT — NO DUPLICATES)

        // ⭐ ALERTS — HARD SAFE COUNT (NO JOINS)

        var alertIds = await _db.AlertEvents
            .AsNoTracking()
            .Where(a => machineIds.Contains(a.MachineId))
            .Select(a => a.AlertId)
            .ToListAsync();

        result.ActiveAlerts = alertIds.Distinct().Count();


        // ====================================================
        // ⭐ AVG EFFICIENCY (LATEST HEALTH PER MACHINE)
        // ====================================================
        var healthQuery = _db.MachineHealth
            .AsNoTracking()
            .Where(h => machineIds.Contains(h.MachineId));

        // calendar filter support
        if (from != null)
            healthQuery = healthQuery.Where(h => h.RecordedAt >= from);

        if (to != null)
            healthQuery = healthQuery.Where(h => h.RecordedAt <= to);

        var latestHealthValues = await healthQuery
            .GroupBy(h => h.MachineId)
            .Select(g => g
                .OrderByDescending(x => x.RecordedAt)
                .Select(x => (double?)x.HealthScore)
                .FirstOrDefault())
            .ToListAsync();

        result.AvgEfficiency =
            latestHealthValues.Any()
                ? latestHealthValues.Average() ?? 0
                : 0;


        // ====================================================
        // ⭐ ALERT DONUT DISTRIBUTION
        // ====================================================
        result.AlertDistribution = await _db.AlertEvents
            .AsNoTracking()
            .Where(a => machineIds.Contains(a.MachineId))
            .GroupBy(x => x.Severity)
            .Select(g => new AlertSeverityDto
            {
                Severity = g.Key!,
                Count = g.Count()
            }).ToListAsync();


        // ====================================================
        // ⭐ ENERGY TREND (Last 24 points)
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
            .Take(24)
            .Select(x => new EnergyPoint
            {
                Time = x.i.RecordedAt,
                Energy = (double)x.e.EnergyImportKwh
            })
            .ToListAsync();

        // ====================================================
        // ⭐ OEE TREND
        // ====================================================
        var oeeQuery = _db.MachineHealth
            .AsNoTracking()
            .Where(h => machineIds.Contains(h.MachineId));

        if (from != null)
            oeeQuery = oeeQuery.Where(h => h.RecordedAt >= from);

        if (to != null)
            oeeQuery = oeeQuery.Where(h => h.RecordedAt <= to);

        result.OeeTrend = await oeeQuery
            .OrderByDescending(h => h.RecordedAt)
            .Take(24)
            .Select(h => new OeePoint
            {
                Time = h.RecordedAt,
                Availability = (double)h.HealthScore,
                Performance = (double)h.HealthScore * 0.98,
                Quality = (double)h.HealthScore * 0.96
            })
            .ToListAsync();


        // ====================================================
        // ⭐ PRODUCTION VS TARGET (LOAD BASED - UNIFIED)
        // ====================================================
        var productionQuery = _db.MachineHealth
            .AsNoTracking()
            .Where(h => machineIds.Contains(h.MachineId));

        if (from != null)
            productionQuery = productionQuery.Where(h => h.RecordedAt >= from);

        if (to != null)
            productionQuery = productionQuery.Where(h => h.RecordedAt <= to);

        result.ProductionTrend = await productionQuery
            .OrderByDescending(h => h.RecordedAt)
            .Take(24)
            .Select(h => new ProductionPoint
            {
                Time = h.RecordedAt,

                // ⭐ SAME FORMULA AS PLANT DASHBOARD
                Actual = (double)(h.AvgLoad * 10m),
                Target = (double)(h.AvgLoad * 11m)
            })
            .ToListAsync();


        // ====================================================
        // ⭐ PLANT CARDS (Dynamic Efficiency Per Plant)
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
