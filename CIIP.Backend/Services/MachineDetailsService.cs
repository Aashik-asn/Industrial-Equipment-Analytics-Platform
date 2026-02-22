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
            .FirstOrDefaultAsync(x => x.MachineId == machineId);

        if (machine == null)
            return result;

        result.MachineId = machine.MachineId;
        result.MachineCode = machine.MachineCode;
        result.MachineName = machine.MachineName;
        result.Status = machine.Status;

        // ======================================================
        // HEALTH DATA (LAST 12)
        // ======================================================
        var healthData = await _db.MachineHealth
            .Where(x => x.MachineId == machineId)
            .OrderByDescending(x => x.RecordedAt)
            .Take(12)
            .OrderBy(x => x.RecordedAt)
            .ToListAsync();

        var latestHealth = healthData.LastOrDefault();

        result.HealthScore = latestHealth?.HealthScore ?? 0;
        result.RuntimeHours = (double)(latestHealth?.RuntimeHours ?? 0);

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
        // VIBRATION TREND (LAST 12)
        // ======================================================
        var mechanicalTrend = await _db.TelemetryMechanical
            .Include(x => x.Ingestion)
            .Where(x => x.Ingestion != null && x.Ingestion.MachineId == machineId)
            .OrderByDescending(x => x.Ingestion!.RecordedAt)
            .Take(12)
            .OrderBy(x => x.Ingestion!.RecordedAt)
            .ToListAsync();

        result.VibrationTrend = mechanicalTrend.Select(x => new TrendPoint
        {
            Time = x.Ingestion!.RecordedAt,
            Value = x.VibrationX ?? 0m
        }).ToList();

        // ======================================================
        // POWER CONSUMPTION TREND (LAST 12)  -> FROM TELEMETRY_ENERGY
        // ======================================================
        var powerTrend = await _db.TelemetryEnergy
            .Include(x => x.Ingestion)
            .Where(x => x.Ingestion != null && x.Ingestion.MachineId == machineId)
            .OrderByDescending(x => x.Ingestion!.RecordedAt)
            .Take(12)
            .OrderBy(x => x.Ingestion!.RecordedAt)
            .ToListAsync();

        result.PowerConsumptionTrend = powerTrend.Select(x => new TrendPoint
        {
            Time = x.Ingestion!.RecordedAt,
            Value = x.EnergyImportKwh ?? 0m   // ✅ since PowerConsumption column doesn't exist
        }).ToList();


        // ======================================================
        // TEMPERATURE TREND (LAST 12)
        // ======================================================
        var tempTrend = await _db.TelemetryEnvironmental
            .Include(x => x.Ingestion)
            .Where(x => x.Ingestion != null && x.Ingestion.MachineId == machineId)
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
        // LATEST TELEMETRY SNAPSHOTS
        // ======================================================
        var latestElectrical = await _db.TelemetryElectrical
            .Include(x => x.Ingestion)
            .Where(x => x.Ingestion != null && x.Ingestion.MachineId == machineId)
            .OrderByDescending(x => x.Ingestion!.RecordedAt)
            .FirstOrDefaultAsync();
        var latestEnergy = await _db.TelemetryEnergy
            .Include(x => x.Ingestion)
            .Where(x => x.Ingestion != null && x.Ingestion.MachineId == machineId)
            .OrderByDescending(x => x.Ingestion!.RecordedAt)
            .FirstOrDefaultAsync();

        var latestEnv = await _db.TelemetryEnvironmental
            .Include(x => x.Ingestion)
            .Where(x => x.Ingestion != null && x.Ingestion.MachineId == machineId)
            .OrderByDescending(x => x.Ingestion!.RecordedAt)
            .FirstOrDefaultAsync();

        var latestMechanical = await _db.TelemetryMechanical
            .Include(x => x.Ingestion)
            .Where(x => x.Ingestion != null && x.Ingestion.MachineId == machineId)
            .OrderByDescending(x => x.Ingestion!.RecordedAt)
            .FirstOrDefaultAsync();

        // ======================================================
        // ELECTRICAL SNAPSHOT
        // ======================================================
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

                EnergyImportKwh = latestEnergy.EnergyImportKwh ?? 0m,
                EnergyExportKwh = latestEnergy.EnergyExportKwh ?? 0m,
                EnergyImportKvah = latestEnergy.EnergyImportKvah ?? 0m
            };
        }

        // ======================================================
        // ENVIRONMENTAL SNAPSHOT
        // ======================================================
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

        // ======================================================
        // MECHANICAL SNAPSHOT
        // ======================================================
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
        // SYSTEM HEALTH INDICATORS
        // ======================================================
        result.SystemHealth = new SystemHealthIndicators
        {
            OverallHealth = result.HealthScore,
            PerformanceIndex = result.LoadTrend.Any()
                ? (double)result.LoadTrend.Average(x => x.Value)
                : 0,
            EfficiencyScore = (double)((latestElectrical?.PowerFactor ?? 0m) * 100)
        };

        // ======================================================
        // ALERTS
        // ======================================================
        result.Alerts = await _db.AlertEvents
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
