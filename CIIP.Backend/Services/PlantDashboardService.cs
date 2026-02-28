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
        // 🔥 PROPER UTC → LOCAL DATE NORMALIZATION
        // ======================================================

        DateTime? fromLocal = null;
        DateTime? toLocal = null;
        bool hourlyMode = true;

        if (from.HasValue && to.HasValue)
        {
            var fromValue = from.Value;
            var toValue = to.Value;

            // Convert UTC → Local if needed
            if (fromValue.Kind == DateTimeKind.Utc)
                fromValue = fromValue.ToLocalTime();

            if (toValue.Kind == DateTimeKind.Utc)
                toValue = toValue.ToLocalTime();

            var fromDate = fromValue.Date;
            var toDate = toValue.Date;

            // START OF DAY
            fromLocal = DateTime.SpecifyKind(
                fromDate,
                DateTimeKind.Unspecified);

            // END OF DAY (inclusive)
            toLocal = DateTime.SpecifyKind(
                toDate.AddDays(1).AddTicks(-1),
                DateTimeKind.Unspecified);

            var inclusiveDays = (toDate - fromDate).Days + 1;

            if (inclusiveDays == 1)
                hourlyMode = true;
            else
                hourlyMode = false;
        }

        // ======================================================
        // MACHINES
        // ======================================================
        var machines = await _db.Machines
            .Include(m => m.Plant)
            .Where(m => m.PlantId == plantId)
            .AsNoTracking()
            .ToListAsync();

        var machineIds = machines.Select(x => x.MachineId).ToList();

        result.TotalMachines = machines.Count;
        result.ActiveMachines = machines.Count(x => x.Status == "RUNNING");

        // ======================================================
        // ⭐ HEALTH BASE QUERY (IDENTICAL TO DASHBOARD SERVICE)
        // ======================================================
        var healthQuery = _db.MachineHealth
            .AsNoTracking()
            .Where(h => machineIds.Contains(h.MachineId));

        if (fromLocal.HasValue)
            healthQuery = healthQuery.Where(h => h.RecordedAt >= fromLocal.Value);

        if (toLocal.HasValue)
            healthQuery = healthQuery.Where(h => h.RecordedAt <= toLocal.Value);

        // NULL FILTER → latest day (same as dashboard)
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

        var latestHealth = await healthQuery
            .GroupBy(h => h.MachineId)
            .Select(g => g.OrderByDescending(x => x.RecordedAt).First())
            .ToListAsync();
        var avgLoadPerMachine = await healthQuery
            .GroupBy(h => h.MachineId)
            .Select(g => new
            {
                MachineId = g.Key,
                AvgLoad = g.Average(x => (double)x.AvgLoad)
            })
            .ToListAsync();
        result.PlantEfficiency =
            latestHealth.Any()
            ? (double)latestHealth.Average(x => x.HealthScore)
            : 0;

        result.AvgRuntime =
            latestHealth.Any()
            ? (double)latestHealth.Average(x => x.RuntimeHours)
            : 0;

        // ======================================================
        // ⭐ ENERGY TREND (EXACT DASHBOARD LOGIC)
        // ======================================================
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
                    x.i.RecordedAt.Hour,
                    0,
                    0))
                .Select(g => new EnergyTrendPoint
                {
                    Time = g.Key,
                    Energy = (decimal)g.Sum(x => (double)(x.e.EnergyImportKwh ?? 0m))
                })
                .OrderBy(x => x.Time)
                .ToListAsync();
        }
        else
        {
            result.EnergyTrend = await energyQuery
                .GroupBy(x => x.i.RecordedAt.Date)
                .Select(g => new EnergyTrendPoint
                {
                    Time = g.Key,
                    Energy = (decimal)g.Sum(x => (double)(x.e.EnergyImportKwh ?? 0))
                })
                .OrderBy(x => x.Time)
                .ToListAsync();
        }

        result.TotalEnergy = result.EnergyTrend.Sum(x => x.Energy);

        // ======================================================
        // ⭐ PRODUCTION TREND (EXACT DASHBOARD LOGIC)
        // ======================================================
        if (hourlyMode)
        {
            result.ProductionTrend = await healthQuery
                .GroupBy(h => new DateTime(
                    h.RecordedAt.Year,
                    h.RecordedAt.Month,
                    h.RecordedAt.Day,
                    h.RecordedAt.Hour,
                    0,
                    0))
                .Select(g => new ProductionTrendPoint
                {
                    Time = g.Key,
                    Actual = (decimal)((double)g.Average(x => x.AvgLoad) * 10),
                    Target = 800
                })
                .OrderBy(x => x.Time)
                .ToListAsync();
        }
        else
        {
            result.ProductionTrend = await healthQuery
                .GroupBy(h => h.RecordedAt.Date)
                .Select(g => new ProductionTrendPoint
                {
                    Time = g.Key,
                    Actual = (decimal)((double)g.Average(x => x.AvgLoad) * 10),
                    Target = 800
                })
                .OrderBy(x => x.Time)
                .ToListAsync();
        }

        // ======================================================
        // ⭐ UPTIME VS DOWNTIME (UNCHANGED ORIGINAL)
        // ======================================================
        var lastHealthRecords = await _db.MachineHealth
            .Where(h => machineIds.Contains(h.MachineId))
            .OrderByDescending(x => x.RecordedAt)
            .Take(336)
            .AsNoTracking()
            .ToListAsync();

        result.UptimeDowntime = machines.Select(m =>
        {
            var records = lastHealthRecords
                .Where(x => x.MachineId == m.MachineId)
                .OrderBy(x => x.RecordedAt)
                .ToList();

            if (records.Count < 2)
                return new UptimePoint
                {
                    Label = m.MachineCode,
                    Uptime = 0,
                    Downtime = 100
                };

            var first = records.First();
            var last = records.Last();

            decimal runtimeDelta = last.RuntimeHours - first.RuntimeHours;
            decimal windowHours =
                (decimal)(last.RecordedAt - first.RecordedAt).TotalHours;

            decimal uptimePercent =
                windowHours <= 0
                ? 0
                : Math.Min(100, (runtimeDelta / windowHours) * 100);

            return new UptimePoint
            {
                Label = m.MachineCode,
                Uptime = (double)uptimePercent,
                Downtime = (double)(100 - uptimePercent)
            };
        }).ToList();

        // ======================================================
        // MACHINE OVERVIEW CARDS
        // ======================================================
        var latestLoadPerMachine = await _db.MachineHealth
            .AsNoTracking()
            .Where(h => machineIds.Contains(h.MachineId))
            .GroupBy(h => h.MachineId)
            .Select(g => g
                .OrderByDescending(x => x.RecordedAt)
                .Select(x => new
                {
                    MachineId = x.MachineId,
                    CurrentLoad = x.AvgLoad
                })
                .First())
            .ToListAsync();
        result.Machines = machines.Select(m =>
        {
            var health = latestHealth
                .FirstOrDefault(x => x.MachineId == m.MachineId);

            var avgLoad = avgLoadPerMachine
                .FirstOrDefault(x => x.MachineId == m.MachineId);

            var currentLoad = latestLoadPerMachine
                .FirstOrDefault(x => x.MachineId == m.MachineId);

            return new MachineOverviewCard
            {
                MachineId = m.MachineId,
                MachineCode = m.MachineCode,
                MachineName = m.MachineName,
                MachineType = m.MachineType,
                Status = m.Status,

                HealthScore = (double)(health?.HealthScore ?? 0),
                RuntimeHours = (double)(health?.RuntimeHours ?? 0),

                AvgLoad = (double)(avgLoad?.AvgLoad ?? 0),
                CurrentLoad = (double)(currentLoad?.CurrentLoad ?? 0)
            };
        }).ToList();

        return result;
    }
}