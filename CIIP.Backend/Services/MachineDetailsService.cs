using CIIP.Backend.Data;
using CIIP.Backend.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CIIP.Backend.Services;

public class MachineDetailsService
{
    private readonly CiipDbContext _db;

    public MachineDetailsService(CiipDbContext db)
    {
        _db = db;
    }

    public async Task<MachineDetailsResponse> GetMachineDetails(
        Guid tenantId,
        Guid plantId,
        Guid machineId,
        DateTime? from,
        DateTime? to)
    {
        var result = new MachineDetailsResponse();

        // ======================================================
        // MACHINE INFO (NOT AFFECTED BY CALENDAR)
        // ======================================================
        var machine = await _db.Machines
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MachineId == machineId);

        if (machine == null)
            return result;

        result.MachineId = machine.MachineId;
        result.MachineCode = machine.MachineCode;
        result.MachineName = machine.MachineName;
        result.Status = machine.Status;

        // ======================================================
        // INDUSTRIAL CALENDAR ENGINE (TREND ONLY)
        // ======================================================

        // ======================================================
        // 🔥 PROPER INCLUSIVE DATE RANGE (SAME AS DASHBOARD)
        // ======================================================

        DateTime? fromLocal = null;
        DateTime? toLocal = null;
        bool hourlyMode = true;

        if (from.HasValue && to.HasValue)
        {
            var fromValue = from.Value;
            var toValue = to.Value;

            // Convert UTC → Local if necessary
            if (fromValue.Kind == DateTimeKind.Utc)
                fromValue = fromValue.ToLocalTime();

            if (toValue.Kind == DateTimeKind.Utc)
                toValue = toValue.ToLocalTime();

            var fromDate = fromValue.Date;
            var toDate = toValue.Date;

            fromLocal = DateTime.SpecifyKind(
                fromDate,
                DateTimeKind.Unspecified);

            // exclusive upper bound
            toLocal = DateTime.SpecifyKind(
                toDate.AddDays(1),
                DateTimeKind.Unspecified);

            var inclusiveDays = (toDate - fromDate).Days + 1;

            if (inclusiveDays == 1)
                hourlyMode = true;
            else
                hourlyMode = false;
        }
        else if (!from.HasValue && !to.HasValue)
        {
            var latest = await _db.TelemetryIngestions
                .Where(x => x.MachineId == machineId)
                .MaxAsync(x => (DateTime?)x.RecordedAt);

            if (latest.HasValue)
            {
                var latestDate = latest.Value.Date;

                fromLocal = DateTime.SpecifyKind(
                    latestDate,
                    DateTimeKind.Unspecified);

                toLocal = DateTime.SpecifyKind(
                    latestDate.AddDays(1),
                    DateTimeKind.Unspecified);

                hourlyMode = true;
            }
        }

        // ======================================================
        // HEALTH + LOAD TREND (CALENDAR CONTROLLED)
        // ======================================================

        var healthQuery = _db.MachineHealth
            .AsNoTracking()
            .Where(x => x.MachineId == machineId);

        if (fromLocal.HasValue && toLocal.HasValue)
        {
            healthQuery = healthQuery.Where(x =>
                x.RecordedAt >= fromLocal.Value &&
                x.RecordedAt <= toLocal.Value);
        }

        var healthData = await healthQuery.ToListAsync();

        var groupedHealth = hourlyMode
            ? healthData.GroupBy(x =>
                new DateTime(x.RecordedAt.Year,
                             x.RecordedAt.Month,
                             x.RecordedAt.Day,
                             x.RecordedAt.Hour, 0, 0))
            : healthData.GroupBy(x => x.RecordedAt.Date);

        result.HealthTrend = groupedHealth
            .OrderBy(g => g.Key)
            .Select(g => new TrendPoint
            {
                Time = g.Key,
                Value = (decimal)g.Average(x => x.HealthScore)
            }).ToList();

        result.LoadTrend = groupedHealth
            .OrderBy(g => g.Key)
            .Select(g => new TrendPoint
            {
                Time = g.Key,
                Value = g.Average(x => x.AvgLoad)
            }).ToList();

        var latestHealth = await _db.MachineHealth
        .AsNoTracking()
        .Where(x => x.MachineId == machineId)
        .OrderByDescending(x => x.RecordedAt)
        .FirstOrDefaultAsync();

        result.HealthScore = latestHealth?.HealthScore ?? 0;
        result.RuntimeHours = (double)(latestHealth?.RuntimeHours ?? 0m);

        // ======================================================
        // VIBRATION TREND (CALENDAR CONTROLLED)
        // ======================================================

        var mechanicalQuery = _db.TelemetryMechanical
            .Include(x => x.Ingestion)
            .AsNoTracking()
            .Where(x => x.Ingestion != null &&
                        x.Ingestion.MachineId == machineId);

        if (fromLocal.HasValue && toLocal.HasValue)
        {
            mechanicalQuery = mechanicalQuery.Where(x =>
                x.Ingestion!.RecordedAt >= fromLocal.Value &&
                x.Ingestion!.RecordedAt <= toLocal.Value);
        }

        var mechanicalData = await mechanicalQuery.ToListAsync();

        var groupedMechanical = hourlyMode
            ? mechanicalData.GroupBy(x =>
                new DateTime(x.Ingestion!.RecordedAt.Year,
                             x.Ingestion.RecordedAt.Month,
                             x.Ingestion.RecordedAt.Day,
                             x.Ingestion.RecordedAt.Hour, 0, 0))
            : mechanicalData.GroupBy(x => x.Ingestion!.RecordedAt.Date);

        result.VibrationTrend = groupedMechanical
            .OrderBy(g => g.Key)
            .Select(g => new VibrationTrendPoint
            {
                Time = g.Key,
                VibrationX = (decimal)g.Average(x => (double)(x.VibrationX ?? 0m)),
                VibrationY = (decimal)g.Average(x => (double)(x.VibrationY ?? 0m)),
                VibrationZ = (decimal)g.Average(x => (double)(x.VibrationZ ?? 0m))
            }).ToList();

        var latestMechanical = await _db.TelemetryMechanical
            .Include(x => x.Ingestion)
            .AsNoTracking()
            .Where(x => x.Ingestion != null &&
                        x.Ingestion.MachineId == machineId)
            .OrderByDescending(x => x.Ingestion!.RecordedAt)
            .FirstOrDefaultAsync();

        // ======================================================
        // POWER TREND (CALENDAR CONTROLLED)
        // ======================================================

        var energyQuery = _db.TelemetryEnergy
            .Include(x => x.Ingestion)
            .AsNoTracking()
            .Where(x => x.Ingestion != null &&
                        x.Ingestion.MachineId == machineId);

        if (fromLocal.HasValue && toLocal.HasValue)
        {
            energyQuery = energyQuery.Where(x =>
                x.Ingestion!.RecordedAt >= fromLocal.Value &&
                x.Ingestion!.RecordedAt <= toLocal.Value);
        }

        var energyData = await energyQuery.ToListAsync();

        var groupedEnergy = hourlyMode
            ? energyData.GroupBy(x =>
                new DateTime(x.Ingestion!.RecordedAt.Year,
                             x.Ingestion.RecordedAt.Month,
                             x.Ingestion.RecordedAt.Day,
                             x.Ingestion.RecordedAt.Hour, 0, 0))
            : energyData.GroupBy(x => x.Ingestion!.RecordedAt.Date);

        result.PowerConsumptionTrend = groupedEnergy
            .OrderBy(g => g.Key)
            .Select(g => new TrendPoint
            {
                Time = g.Key,
                Value = (decimal)g.Sum(x => (double)(x.EnergyImportKwh ?? 0m))
            }).ToList();

        var latestEnergy = await _db.TelemetryEnergy
        .Include(x => x.Ingestion)
        .AsNoTracking()
        .Where(x => x.Ingestion != null &&
                    x.Ingestion.MachineId == machineId)
        .OrderByDescending(x => x.Ingestion!.RecordedAt)
        .FirstOrDefaultAsync();

        // ======================================================
        // TEMPERATURE TREND (CALENDAR CONTROLLED)
        // ======================================================

        var tempQuery = _db.TelemetryEnvironmental
            .Include(x => x.Ingestion)
            .AsNoTracking()
            .Where(x => x.Ingestion != null &&
                        x.Ingestion.MachineId == machineId);

        if (fromLocal.HasValue && toLocal.HasValue)
        {
            tempQuery = tempQuery.Where(x =>
                x.Ingestion!.RecordedAt >= fromLocal.Value &&
                x.Ingestion!.RecordedAt <= toLocal.Value);
        }

        var tempData = await tempQuery.ToListAsync();

        var groupedTemp = hourlyMode
            ? tempData.GroupBy(x =>
                new DateTime(x.Ingestion!.RecordedAt.Year,
                             x.Ingestion.RecordedAt.Month,
                             x.Ingestion.RecordedAt.Day,
                             x.Ingestion.RecordedAt.Hour, 0, 0))
            : tempData.GroupBy(x => x.Ingestion!.RecordedAt.Date);

        result.TemperatureTrend = groupedTemp
            .OrderBy(g => g.Key)
            .Select(g => new TrendPoint
            {
                Time = g.Key,
                Value = (decimal)g.Average(x => (double)(x.Temperature ?? 0m))
            }).ToList();

        var latestEnv = await _db.TelemetryEnvironmental
            .Include(x => x.Ingestion)
            .AsNoTracking()
            .Where(x => x.Ingestion != null &&
                        x.Ingestion.MachineId == machineId)
            .OrderByDescending(x => x.Ingestion!.RecordedAt)
            .FirstOrDefaultAsync();

        // ======================================================
        // SNAPSHOTS (NOT CALENDAR CONTROLLED)
        // ======================================================

        var latestElectrical = await _db.TelemetryElectrical
            .Include(x => x.Ingestion)
            .AsNoTracking()
            .Where(x => x.Ingestion != null &&
                        x.Ingestion.MachineId == machineId)
            .OrderByDescending(x => x.Ingestion!.RecordedAt)
            .FirstOrDefaultAsync();

        if (latestElectrical != null)
        {
            result.Electrical = new ElectricalSnapshot
            {
                RVoltage = latestElectrical.RVoltage ?? 0m,
                YVoltage = latestElectrical.YVoltage ?? 0m,
                BVoltage = latestElectrical.BVoltage ?? 0m,
                RCurrent = latestElectrical.RCurrent ?? 0m,
                YCurrent = latestElectrical.YCurrent ?? 0m,
                BCurrent = latestElectrical.BCurrent ?? 0m,
                PowerFactor = latestElectrical.PowerFactor ?? 0m,
                Frequency = latestElectrical.Frequency ?? 0m,
                EnergyImportKwh = latestEnergy?.EnergyImportKwh ?? 0m,
                EnergyExportKwh = latestEnergy?.EnergyExportKwh ?? 0m,
                EnergyImportKvah = latestEnergy?.EnergyImportKvah ?? 0m
            };
        }

        if (latestEnv != null)
        {
            result.Environmental = new EnvironmentalSnapshot
            {
                Temperature = latestEnv.Temperature ?? 0m,
                Humidity = latestEnv.Humidity ?? 0m,
                FlowRate = latestEnv.Flowrate ?? 0m,
                Pressure = latestEnv.Pressure ?? 0m
            };
        }

        if (latestMechanical != null)
        {
            result.Mechanical = new MechanicalSnapshot
            {
                VibrationX = latestMechanical.VibrationX ?? 0m,
                VibrationY = latestMechanical.VibrationY ?? 0m,
                VibrationZ = latestMechanical.VibrationZ ?? 0m,
                RPM = latestMechanical.Rpm ?? 0m
            };
        }

        // ======================================================
        // INDUSTRIAL RMS VIBRATION
        // ======================================================

        decimal rms = 0m;

        if (latestMechanical != null)
        {
            decimal vx = latestMechanical.VibrationX ?? 0m;
            decimal vy = latestMechanical.VibrationY ?? 0m;
            decimal vz = latestMechanical.VibrationZ ?? 0m;

            rms = (decimal)Math.Sqrt(
                (double)((vx * vx + vy * vy + vz * vz) / 3m));
        }

        // ======================================================
        // SYSTEM HEALTH (NOT CALENDAR CONTROLLED)
        // ======================================================

        var latestHealthAll = await _db.MachineHealth
    .AsNoTracking()
    .Where(x => x.MachineId == machineId)
    .ToListAsync();

        decimal avgLoad = latestHealthAll.Any()
            ? latestHealthAll.Average(x => x.AvgLoad)
            : 0m;

        result.SystemHealth = new SystemHealthIndicators
        {
            OverallHealth = result.HealthScore,

            PerformanceIndex = latestHealthAll.Any()
            ? (double)avgLoad
            : 0,

            EfficiencyScore =
                (double)((latestElectrical?.PowerFactor ?? 0m) * 100m)
                - (double)(rms * 2m)
                - (double)(avgLoad * 0.05m)
        };

        // ======================================================
        // ALERTS (NOT CALENDAR CONTROLLED)
        // ======================================================

        result.Alerts = await _db.AlertEvents
            .AsNoTracking()
            .Where(x => x.MachineId == machineId)
            .OrderByDescending(x => x.GeneratedAt)
            .Take(20)
            .Select(x => new AlertSummary
            {
                Severity = x.Severity,
                Parameter = x.Parameter
            })
            .ToListAsync();

        return result;
    }
}