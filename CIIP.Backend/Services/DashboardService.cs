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
        // 🔥 NORMALIZE DATETIME (timestamp without timezone)
        // ====================================================
        DateTime? fromLocal = from.HasValue
            ? DateTime.SpecifyKind(from.Value, DateTimeKind.Unspecified)
            : null;

        DateTime? toLocal = to.HasValue
            ? DateTime.SpecifyKind(to.Value, DateTimeKind.Unspecified)
            : null;

        bool hourlyMode = true;

        if (fromLocal.HasValue && toLocal.HasValue)
        {
            var diffDays = (toLocal.Value.Date - fromLocal.Value.Date).TotalDays;
            if (diffDays > 1)
                hourlyMode = false;
        }

        // ====================================================
        // ⭐ PLANTS + MACHINES
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
        // ⭐ HEALTH BASE QUERY
        // ====================================================
        var healthQuery = _db.MachineHealth
            .AsNoTracking()
            .Where(h => machineIds.Contains(h.MachineId));

        if (fromLocal.HasValue)
            healthQuery = healthQuery.Where(h => h.RecordedAt >= fromLocal.Value);

        if (toLocal.HasValue)
            healthQuery = healthQuery.Where(h => h.RecordedAt <= toLocal.Value);

        // ====================================================
        // ⭐ NULL FILTER → LATEST DAY ONLY
        // ====================================================
        if (!fromLocal.HasValue && !toLocal.HasValue)
        {
            var latestDate = await healthQuery
                .OrderByDescending(h => h.RecordedAt)
                .Select(h => h.RecordedAt.Date)
                .FirstOrDefaultAsync();

            fromLocal = latestDate;
            toLocal = latestDate.AddDays(1).AddTicks(-1);

            healthQuery = healthQuery
                .Where(h => h.RecordedAt >= fromLocal && h.RecordedAt <= toLocal);

            hourlyMode = true;
        }

        // ====================================================
        // ⭐ ALERTS (USES GeneratedAt)
        // ====================================================
        var alertQuery = _db.AlertEvents
            .AsNoTracking()
            .Where(a =>
                machineIds.Contains(a.MachineId) &&
                a.AlertStatus == "ACTIVE");

        if (fromLocal.HasValue)
            alertQuery = alertQuery.Where(a => a.GeneratedAt >= fromLocal.Value);

        if (toLocal.HasValue)
            alertQuery = alertQuery.Where(a => a.GeneratedAt <= toLocal.Value);

        result.ActiveAlerts = await alertQuery
            .Select(a => a.AlertId)
            .Distinct()
            .CountAsync();

        result.AlertDistribution = await alertQuery
            .GroupBy(x => x.Severity)
            .Select(g => new AlertSeverityDto
            {
                Severity = g.Key!,
                Count = g.Count()
            })
            .ToListAsync();

        // ====================================================
        // ⭐ AVG EFFICIENCY
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
        // ⭐ OEE TREND (CALENDAR AWARE)
        // ====================================================
        if (hourlyMode)
        {
            result.OeeTrend = await healthQuery
                .GroupBy(h => new DateTime(
                    h.RecordedAt.Year,
                    h.RecordedAt.Month,
                    h.RecordedAt.Day,
                    h.RecordedAt.Hour,
                    0,
                    0))
                .Select(g => new OeePoint
                {
                    Time = g.Key,
                    Availability = (double)Math.Min(100m,
                        (g.OrderByDescending(x => x.RecordedAt)
                         .Select(x => x.RuntimeHours)
                         .FirstOrDefault() % 10m) * 10m),

                    Performance = (double)Math.Min(100m,
                        g.Average(x => x.AvgLoad)),

                    Quality = (double)g.Average(x => x.HealthScore)
                })
                .OrderBy(x => x.Time)
                .ToListAsync();
        }
        else
        {
            result.OeeTrend = await healthQuery
                .GroupBy(h => h.RecordedAt.Date)
                .Select(g => new OeePoint
                {
                    Time = g.Key,
                    Availability = (double)Math.Min(100m,
                        (g.OrderByDescending(x => x.RecordedAt)
                         .Select(x => x.RuntimeHours)
                         .FirstOrDefault() % 10m) * 10m),

                    Performance = (double)Math.Min(100m,
                        g.Average(x => x.AvgLoad)),

                    Quality = (double)g.Average(x => x.HealthScore)
                })
                .OrderBy(x => x.Time)
                .ToListAsync();
        }

        // ====================================================
        // ⭐ ENERGY TREND
        // ====================================================
        var energyQuery =
            from e in _db.TelemetryEnergy.AsNoTracking()
            join i in _db.TelemetryIngestions.AsNoTracking()
                on e.IngestionId equals i.IngestionId
            where machineIds.Contains(i.MachineId)
            select new { e, i };

        if (fromLocal.HasValue)
            energyQuery = energyQuery.Where(x => x.i.RecordedAt >= fromLocal.Value);

        if (toLocal.HasValue)
            energyQuery = energyQuery.Where(x => x.i.RecordedAt <= toLocal.Value);

        if (hourlyMode)
        {
            result.EnergyTrend = await energyQuery
                .GroupBy(x => new DateTime(
                    x.i.RecordedAt.Year,
                    x.i.RecordedAt.Month,
                    x.i.RecordedAt.Day,
                    x.i.RecordedAt.Hour, 0, 0))
                .Select(g => new EnergyPoint
                {
                    Time = g.Key,
                    Energy = (double)g.Sum(x => x.e.EnergyImportKwh ?? 0)
                })
                .OrderBy(x => x.Time)
                .ToListAsync();
        }
        else
        {
            result.EnergyTrend = await energyQuery
                .GroupBy(x => x.i.RecordedAt.Date)
                .Select(g => new EnergyPoint
                {
                    Time = g.Key,
                    Energy = (double)g.Sum(x => x.e.EnergyImportKwh ?? 0)
                })
                .OrderBy(x => x.Time)
                .ToListAsync();
        }

        // ====================================================
        // ⭐ PRODUCTION TREND
        // ====================================================
        if (hourlyMode)
        {
            result.ProductionTrend = await healthQuery
                .GroupBy(h => new DateTime(
                    h.RecordedAt.Year,
                    h.RecordedAt.Month,
                    h.RecordedAt.Day,
                    h.RecordedAt.Hour, 0, 0))
                .Select(g => new ProductionPoint
                {
                    Time = g.Key,
                    Actual = (double)g.Average(x => x.AvgLoad) * 10,
                    Target = 800
                })
                .OrderBy(x => x.Time)
                .ToListAsync();
        }
        else
        {
            result.ProductionTrend = await healthQuery
                .GroupBy(h => h.RecordedAt.Date)
                .Select(g => new ProductionPoint
                {
                    Time = g.Key,
                    Actual = (double)g.Average(x => x.AvgLoad) * 10,
                    Target = 800
                })
                .OrderBy(x => x.Time)
                .ToListAsync();
        }

        // ====================================================
        // ⭐ PLANT CARDS
        // ====================================================
        result.Plants = await plants
            .Select(p => new PlantCardDto
            {
                PlantId = p.PlantId,
                PlantName = p.PlantName,
                Machines = p.Machines.Count,

                Efficiency =
                    _db.MachineHealth
                        .Where(h => h.Machine.PlantId == p.PlantId)
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