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
        Guid machineId)
    {
        var result = new MachineDetailsResponse();

        // ======================================================
        // MACHINE INFO
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
        // MACHINE HEALTH (LAST 12 RECORDS)
        // ======================================================
        var healthData = await _db.MachineHealth
            .AsNoTracking()
            .Where(x => x.MachineId == machineId)
            .OrderByDescending(x => x.RecordedAt)
            .Take(12)
            .OrderBy(x => x.RecordedAt)
            .ToListAsync();

        var latestHealth = healthData.LastOrDefault();

        result.HealthScore = latestHealth?.HealthScore ?? 0;
        result.RuntimeHours = (double)(latestHealth?.RuntimeHours ?? 0m);

        result.HealthTrend = healthData.Select(x => new TrendPoint
        {
            Time = x.RecordedAt,
            Value = x.HealthScore
        }).ToList();

        result.LoadTrend = healthData.Select(x => new TrendPoint
        {
            Time = x.RecordedAt,
            Value = x.AvgLoad
        }).ToList();

        // ======================================================
        // ⭐ TRUE INDUSTRIAL VIBRATION TREND (OEE STYLE STRUCTURE)
        // ======================================================
        var mechanicalTrend = await _db.TelemetryMechanical
            .Include(x => x.Ingestion)
            .AsNoTracking()
            .Where(x => x.Ingestion != null &&
                        x.Ingestion.MachineId == machineId)
            .OrderByDescending(x => x.Ingestion!.RecordedAt)
            .Take(12)
            .OrderBy(x => x.Ingestion!.RecordedAt)
            .ToListAsync();

        result.VibrationTrend = mechanicalTrend.Select(x => new VibrationTrendPoint
        {
            Time = x.Ingestion!.RecordedAt,
            VibrationX = x.VibrationX ?? 0m,
            VibrationY = x.VibrationY ?? 0m,
            VibrationZ = x.VibrationZ ?? 0m
        }).ToList();

        // ======================================================
        // POWER TREND (FROM TELEMETRY ENERGY)
        // ======================================================
        var energyTrend = await _db.TelemetryEnergy
            .Include(x => x.Ingestion)
            .AsNoTracking()
            .Where(x => x.Ingestion != null &&
                        x.Ingestion.MachineId == machineId)
            .OrderByDescending(x => x.Ingestion!.RecordedAt)
            .Take(12)
            .OrderBy(x => x.Ingestion!.RecordedAt)
            .ToListAsync();

        result.PowerConsumptionTrend = energyTrend.Select(x => new TrendPoint
        {
            Time = x.Ingestion!.RecordedAt,
            Value = x.EnergyImportKwh ?? 0m
        }).ToList();

        // ======================================================
        // TEMPERATURE TREND
        // ======================================================
        var tempTrend = await _db.TelemetryEnvironmental
            .Include(x => x.Ingestion)
            .AsNoTracking()
            .Where(x => x.Ingestion != null &&
                        x.Ingestion.MachineId == machineId)
            .OrderByDescending(x => x.Ingestion!.RecordedAt)
            .Take(12)
            .OrderBy(x => x.Ingestion!.RecordedAt)
            .ToListAsync();

        result.TemperatureTrend = tempTrend.Select(x => new TrendPoint
        {
            Time = x.Ingestion!.RecordedAt,
            Value = x.Temperature ?? 0m
        }).ToList();

        // ======================================================
        // LATEST SNAPSHOTS
        // ======================================================
        var latestElectrical = await _db.TelemetryElectrical
            .Include(x => x.Ingestion)
            .AsNoTracking()
            .Where(x => x.Ingestion != null &&
                        x.Ingestion.MachineId == machineId)
            .OrderByDescending(x => x.Ingestion!.RecordedAt)
            .FirstOrDefaultAsync();

        var latestEnergy = energyTrend.LastOrDefault();
        var latestEnv = tempTrend.LastOrDefault();
        var latestMechanical = mechanicalTrend.LastOrDefault();

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
        // SYSTEM HEALTH (INDUSTRIAL LOGIC)
        // ======================================================
        decimal avgLoad = healthData.Any()
            ? healthData.Average(x => x.AvgLoad)
            : 0m;

        result.SystemHealth = new SystemHealthIndicators
        {
            OverallHealth = result.HealthScore,

            PerformanceIndex = healthData.Any()
                ? (double)avgLoad
                : 0,

            EfficiencyScore =
                (double)((latestElectrical?.PowerFactor ?? 0m) * 100m)
                - (double)(rms * 2m)
                - (double)(avgLoad * 0.05m)
        };

        // ======================================================
        // ALERTS
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
