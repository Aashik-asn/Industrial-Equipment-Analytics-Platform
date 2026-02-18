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
        // HEALTH DATA
        // ======================================================
        var healthData = await _db.MachineHealth
            .Where(x => x.MachineId == machineId)
            .OrderBy(x => x.RecordedAt)
            .Take(24)
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
        // VIBRATION TREND
        // ======================================================
        var mechanicalTrend = await _db.TelemetryMechanical
            .Include(x => x.Ingestion)
            .Where(x => x.Ingestion != null && x.Ingestion.MachineId == machineId)
            .OrderBy(x => x.Ingestion!.RecordedAt)
            .Take(24)
            .ToListAsync();

        result.VibrationTrend = mechanicalTrend.Select(x => new TrendPoint
        {
            Time = x.Ingestion!.RecordedAt,
            Value = x.Vibration ?? 0m
        }).ToList();

        // ======================================================
        // LATEST TELEMETRY SNAPSHOTS
        // ======================================================
        var latestElectrical = await _db.TelemetryElectrical
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
                RVoltage = latestElectrical.RVoltage ?? 0,
                YVoltage = latestElectrical.YVoltage ?? 0,
                BVoltage = latestElectrical.BVoltage ?? 0,
                RCurrent = latestElectrical.RCurrent ?? 0,
                YCurrent = latestElectrical.YCurrent ?? 0,
                BCurrent = latestElectrical.BCurrent ?? 0,
                PowerFactor = latestElectrical.PowerFactor ?? 0,
                Frequency = latestElectrical.Frequency ?? 0
            };
        }

        // ======================================================
        // ENVIRONMENTAL SNAPSHOT
        // ======================================================
        if (latestEnv != null)
        {
            result.Environmental = new EnvironmentalSnapshot
            {
                Temperature = latestEnv.Temperature ?? 0,
                Humidity = latestEnv.Humidity ?? 0,
                FlowRate = 0,
                Pressure = 0
            };
        }

        // ======================================================
        // MECHANICAL SNAPSHOT
        // ======================================================
        if (latestMechanical != null)
        {
            result.Mechanical = new MechanicalSnapshot
            {
                VibrationX = latestMechanical.Vibration ?? 0,
                VibrationY = latestMechanical.Vibration ?? 0,
                VibrationZ = latestMechanical.Vibration ?? 0,
                RPM = latestMechanical.Rpm ?? 0
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
            EfficiencyScore = (double)((latestElectrical?.PowerFactor ?? 0) * 100)
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
