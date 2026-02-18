using CIIP.Backend.Data;
using CIIP.Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace CIIP.Backend.Services;

public class PlantDashboardService
{
    private readonly CiipDbContext _db;

    public PlantDashboardService(CiipDbContext db)
    {
        _db = db;
    }

    public async Task<PlantDashboardResponse> GetPlantDashboard(
        Guid tenantId,
        Guid plantId,
        DateTime? from,
        DateTime? to)
    {
        var result = new PlantDashboardResponse();

        // ======================================================
        // BASE MACHINE QUERY
        // ======================================================
        var machines = await _db.Machines
            .Include(m => m.Plant)
            .Where(m => m.PlantId == plantId)
            .AsNoTracking()
            .ToListAsync();

        var machineIds = machines.Select(x => x.MachineId).ToList();

        result.TotalMachines = machines.Count;

        result.ActiveMachines = machines.Count(x =>
            x.Status != null && x.Status == "RUNNING");

        // ======================================================
        // MACHINE HEALTH (LATEST SNAPSHOT)
        // ======================================================
        var healthQuery = _db.MachineHealth
            .Where(h => machineIds.Contains(h.MachineId));

        if (from.HasValue)
            healthQuery = healthQuery.Where(x => x.RecordedAt >= from.Value);

        if (to.HasValue)
            healthQuery = healthQuery.Where(x => x.RecordedAt <= to.Value);

        var latestHealth = await healthQuery
            .AsNoTracking()
            .GroupBy(h => h.MachineId)
            .Select(g => g.OrderByDescending(x => x.RecordedAt).First())
            .ToListAsync();

        result.PlantEfficiency = latestHealth.Any()
            ? (double)latestHealth.Average(x => x.HealthScore)
            : 0;

        result.AvgRuntime = latestHealth.Any()
            ? (double)latestHealth.Average(x => x.RuntimeHours)
            : 0;

        // ======================================================
        // ENERGY TREND
        // ======================================================
        var energyQuery = _db.TelemetryElectrical
            .Include(e => e.Ingestion)
            .Where(e => e.Ingestion != null &&
                        machineIds.Contains(e.Ingestion.MachineId));

        if (from.HasValue)
            energyQuery = energyQuery.Where(e =>
                e.Ingestion!.RecordedAt >= from.Value);

        if (to.HasValue)
            energyQuery = energyQuery.Where(e =>
                e.Ingestion!.RecordedAt <= to.Value);

        result.EnergyTrend = await energyQuery
            .OrderBy(x => x.Ingestion!.RecordedAt)
            .Select(x => new EnergyTrendPoint
            {
                Time = x.Ingestion!.RecordedAt,
                Energy =
                    (x.RVoltage ?? 0)
                    + (x.YVoltage ?? 0)
                    + (x.BVoltage ?? 0)
            })
            .Take(24)
            .ToListAsync();

        result.TotalEnergy = result.EnergyTrend.Sum(x => x.Energy);

        // ======================================================
        // PRODUCTION TREND (DERIVED FROM MACHINE HEALTH)
        // ======================================================
        var productionQuery = _db.MachineHealth
            .Where(h => machineIds.Contains(h.MachineId));

        if (from.HasValue)
            productionQuery = productionQuery.Where(x => x.RecordedAt >= from.Value);

        if (to.HasValue)
            productionQuery = productionQuery.Where(x => x.RecordedAt <= to.Value);

        result.ProductionTrend = await productionQuery
            .OrderBy(x => x.RecordedAt)
            .Select(x => new ProductionTrendPoint
            {
                Time = x.RecordedAt,

                // ⭐ Derived production logic
                Actual = x.AvgLoad * 10,
                Target = x.AvgLoad * 11
            })
            .Take(30)
            .ToListAsync();

        // ======================================================
        // UPTIME VS DOWNTIME (REAL CALCULATION)
        // ======================================================
        var healthRecords = await _db.MachineHealth
            .Where(h => machineIds.Contains(h.MachineId))
            .AsNoTracking()
            .ToListAsync();

        result.UptimeDowntime = machines.Select(m =>
        {
            var machineHealth = healthRecords
                .Where(h => h.MachineId == m.MachineId)
                .OrderByDescending(x => x.RecordedAt)
                .FirstOrDefault();

            decimal runtime = machineHealth?.RuntimeHours ?? 0;

            // Assume 24h dashboard window
            decimal totalHours = 24;

            decimal uptimePercent = totalHours == 0
                ? 0
                : Math.Min(100, (runtime / totalHours) * 100);

            decimal downtimePercent = 100 - uptimePercent;

            return new UptimePoint
            {
                Label = m.MachineCode,
                Uptime = (double)uptimePercent,
                Downtime = (double)downtimePercent
            };
        }).ToList();

        // ======================================================
        // MACHINE STATUS OVERVIEW CARDS
        // ======================================================
        result.Machines = machines.Select(m =>
        {
            var health = latestHealth
                .FirstOrDefault(x => x.MachineId == m.MachineId);

            return new MachineOverviewCard
            {
                MachineId = m.MachineId,
                MachineCode = m.MachineCode,
                MachineName = m.MachineName,
                MachineType = m.MachineType,
                Status = m.Status,
                HealthScore = (double)(health?.HealthScore ?? 0),
                RuntimeHours = (double)(health?.RuntimeHours ?? 0)
            };
        }).ToList();

        return result;
    }
}
