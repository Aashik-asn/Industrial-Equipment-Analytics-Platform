using CIIP.Backend.Data;
using CIIP.Backend.Entities;
using Microsoft.EntityFrameworkCore;
using System;

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
        // MACHINES
        // ======================================================
        var machines = await _db.Machines
            .Include(m => m.Plant)
            .Where(m => m.PlantId == plantId)
            .AsNoTracking()
            .ToListAsync();

        var machineIds = machines.Select(x => x.MachineId).ToList();

        result.TotalMachines = machines.Count;

        result.ActiveMachines =
            machines.Count(x => x.Status == "RUNNING");

        // ======================================================
        // MACHINE HEALTH SNAPSHOT
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

        result.PlantEfficiency =
            latestHealth.Any()
            ? (double)latestHealth.Average(x => x.HealthScore)
            : 0;

        result.AvgRuntime =
            latestHealth.Any()
            ? (double)latestHealth.Average(x => x.RuntimeHours)
            : 0;

        // ======================================================
        // ⭐ ENERGY TREND (REAL ENERGY TABLE)
        // ======================================================
        var energyQuery =
            from e in _db.TelemetryEnergy.AsNoTracking()
            join i in _db.TelemetryIngestions.AsNoTracking()
                on e.IngestionId equals i.IngestionId
            where machineIds.Contains(i.MachineId)
            select new { e, i };

        if (from.HasValue)
            energyQuery = energyQuery.Where(x => x.i.RecordedAt >= from.Value);

        if (to.HasValue)
            energyQuery = energyQuery.Where(x => x.i.RecordedAt <= to.Value);

        result.EnergyTrend = await energyQuery
            .OrderByDescending(x => x.i.RecordedAt)
            .Take(12) // last 24hrs
            .Select(x => new EnergyTrendPoint
            {
                Time = x.i.RecordedAt,
                Energy = x.e.EnergyImportKwh ?? 0
            })
            .ToListAsync();

        result.TotalEnergy = result.EnergyTrend.Sum(x => x.Energy);

        // ======================================================
        // ⭐ PRODUCTION TREND (INDUSTRIAL DERIVED)
        // ======================================================
        var productionData = await _db.MachineHealth
            .Where(h => machineIds.Contains(h.MachineId))
            .OrderByDescending(x => x.RecordedAt)
            .Take(12)
            .AsNoTracking()
            .ToListAsync();

        result.ProductionTrend = productionData
            .OrderBy(x => x.RecordedAt)
            .Select(x => new ProductionTrendPoint
            {
                Time = x.RecordedAt,

                // Actual = Load × Runtime delta factor
                Actual = x.AvgLoad * 0.8m,

                // Target = Ideal load baseline
                Target = 80
            })
            .ToList();

        // ======================================================
        // ⭐ UPTIME VS DOWNTIME (LAST 336 RECORDS)
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

            if (!records.Any())
                return new UptimePoint
                {
                    Label = m.MachineCode,
                    Uptime = 0,
                    Downtime = 100
                };

            decimal runtimeDelta =
                records.Last().RuntimeHours -
                records.First().RuntimeHours;

            decimal totalWindowHours = 336 * 2m; // your 2hr ingestion window

            decimal uptimePercent =
                totalWindowHours == 0
                ? 0
                : Math.Min(100, (runtimeDelta / totalWindowHours) * 100);

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
