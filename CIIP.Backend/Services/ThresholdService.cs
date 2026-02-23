using CIIP.Backend.Data;
using CIIP.Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace CIIP.Backend.Services;

public class ThresholdService
{
    private readonly CiipDbContext _db;

    public ThresholdService(CiipDbContext db)
    {
        _db = db;
    }

    public async Task<ThresholdConfig> GetThresholds(Guid tenantId, string machineType)
    {
        var rows = await _db.AlertThresholds
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId || x.TenantId == null)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        AlertThreshold Resolve(string parameter)
        {
            var tMachine = rows.FirstOrDefault(x =>
                x.Parameter == parameter &&
                x.TenantId == tenantId &&
                x.MachineType == machineType);

            if (tMachine != null)
                return tMachine;

            var tGlobal = rows.FirstOrDefault(x =>
                x.Parameter == parameter &&
                x.TenantId == tenantId &&
                x.MachineType == null);

            if (tGlobal != null)
                return tGlobal;

            return rows.First(x =>
                x.Parameter == parameter &&
                x.TenantId == null &&
                x.MachineType == null);
        }

        var vibration = Resolve("Vibration");
        var current = Resolve("Current");
        var rpmLow = Resolve("RPM_LOW");
        var rpmHigh = Resolve("RPM_HIGH");
        var temp = Resolve("Temperature");
        var loadHigh = Resolve("LOAD_HIGH");
        var loadLow = Resolve("LOAD_LOW");

        return new ThresholdConfig
        {
            VibrationWarning = vibration.WarningValue,
            VibrationCritical = vibration.CriticalValue,
            VibrationThresholdId = vibration.ThresholdId,

            CurrentWarning = current.WarningValue,
            CurrentCritical = current.CriticalValue,
            CurrentThresholdId = current.ThresholdId,

            RpmWarningLow = rpmLow.WarningValue,
            RpmCriticalLow = rpmLow.CriticalValue,
            RpmLowThresholdId = rpmLow.ThresholdId,

            RpmWarningHigh = rpmHigh.WarningValue,
            RpmCriticalHigh = rpmHigh.CriticalValue,
            RpmHighThresholdId = rpmHigh.ThresholdId,

            TemperatureWarning = temp.WarningValue,
            TemperatureCritical = temp.CriticalValue,
            TemperatureThresholdId = temp.ThresholdId,

            LoadHighWarning = loadHigh.WarningValue,
            LoadHighCritical = loadHigh.CriticalValue,
            LoadHighThresholdId = loadHigh.ThresholdId,

            LoadLowWarning = loadLow.WarningValue,
            LoadLowCritical = loadLow.CriticalValue,
            LoadLowThresholdId = loadLow.ThresholdId
        };
    }


}
