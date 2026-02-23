using CIIP.Backend.Data;
using CIIP.Backend.Entities;
using CIIP.Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class TelemetryProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private const decimal INTERVAL_HOURS = 0.05m;

    public TelemetryProcessor(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CiipDbContext>();

            await ProcessMachineHealth(db);
            await UpdateMachineStatus(db);
            await ProcessAlerts(db);

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    // ======================================================
    // MACHINE HEALTH ENGINE (STABLE VERSION)
    // ======================================================
    private async Task ProcessMachineHealth(CiipDbContext db)
    {
        var lastHealthTimeRaw = await db.MachineHealth
            .OrderByDescending(x => x.RecordedAt)
            .Select(x => (DateTime?)x.RecordedAt)
            .FirstOrDefaultAsync();

        DateTime? lastHealthTime = lastHealthTimeRaw.HasValue
            ? DateTime.SpecifyKind(lastHealthTimeRaw.Value, DateTimeKind.Utc)
            : null;

        IQueryable<TelemetryIngestion> telemetryQuery = db.TelemetryIngestions
            .Include(t => t.Mechanical)
            .Include(t => t.Electrical)
            .AsNoTracking();

        if (lastHealthTime != null)
            telemetryQuery = telemetryQuery.Where(t => t.RecordedAt > lastHealthTime);

        telemetryQuery = telemetryQuery.Where(t =>
            !db.MachineHealth.Any(h =>
                h.MachineId == t.MachineId &&
                h.RecordedAt == t.RecordedAt));

        var telemetry = await telemetryQuery
            .OrderBy(t => t.RecordedAt)
            .Take(300)
            .ToListAsync();

        if (!telemetry.Any())
            return;

        var maxCurrentPerMachine = await db.TelemetryElectrical
            .Include(e => e.Ingestion)
            .Where(e => e.Ingestion != null)
            .GroupBy(e => e.Ingestion!.MachineId)
            .Select(g => new
            {
                MachineId = g.Key,
                MaxCurrent = g.Max(e =>
                    ((e.RCurrent ?? 0)
                    + (e.YCurrent ?? 0)
                    + (e.BCurrent ?? 0)) / 3m)
            })
            .ToDictionaryAsync(x => x.MachineId, x => x.MaxCurrent);

        var maxRpmPerMachine = await db.TelemetryMechanical
            .Include(m => m.Ingestion)
            .Where(m => m.Ingestion != null)
            .GroupBy(m => m.Ingestion!.MachineId)
            .Select(g => new
            {
                MachineId = g.Key,
                MaxRpm = g.Max(m => m.Rpm ?? 0m)
            })
            .ToDictionaryAsync(x => x.MachineId, x => x.MaxRpm);

        var runtimeCache = await db.MachineHealth
            .GroupBy(h => h.MachineId)
            .Select(g => new
            {
                MachineId = g.Key,
                Runtime = g.OrderByDescending(x => x.RecordedAt)
                           .Select(x => x.RuntimeHours)
                           .FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.MachineId, x => x.Runtime);

        var newHealthRows = new List<MachineHealth>();
        var processedKeys = new HashSet<string>();

        foreach (var t in telemetry)
        {
            var timeUtc = DateTime.SpecifyKind(t.RecordedAt, DateTimeKind.Utc);
            var key = $"{t.MachineId}-{timeUtc:O}";

            if (!processedKeys.Add(key))
                continue;

            decimal vx = t.Mechanical?.VibrationX ?? 0;
            decimal vy = t.Mechanical?.VibrationY ?? 0;
            decimal vz = t.Mechanical?.VibrationZ ?? 0;

            decimal vibrationRms =
                (decimal)Math.Sqrt((double)((vx * vx + vy * vy + vz * vz) / 3m));

            decimal avgLoad = 0;

            if (t.Electrical != null && t.Mechanical != null)
            {
                decimal avgCurrent =
                    ((t.Electrical.RCurrent ?? 0)
                    + (t.Electrical.YCurrent ?? 0)
                    + (t.Electrical.BCurrent ?? 0)) / 3m;

                decimal rpm = t.Mechanical.Rpm ?? 0m;
                decimal pf = t.Electrical.PowerFactor ?? 1m;

                maxCurrentPerMachine.TryGetValue(t.MachineId, out var maxCurrent);
                maxRpmPerMachine.TryGetValue(t.MachineId, out var maxRpm);

                decimal currentRatio = maxCurrent == 0 ? 0 : avgCurrent / maxCurrent;
                decimal rpmRatio = maxRpm == 0 ? 0 : rpm / maxRpm;

                avgLoad =
                    (currentRatio * 0.6m
                    + rpmRatio * 0.2m
                    + pf * 0.2m) * 100m;

                if (avgLoad < 0) avgLoad = 0;
                if (avgLoad > 150) avgLoad = 150;
            }

            double healthScore =
                100
                - (double)(vibrationRms * 3m)
                - (double)(avgLoad * 0.1m);

            decimal runtime = runtimeCache.ContainsKey(t.MachineId)
                ? runtimeCache[t.MachineId]
                : 0;

            if ((t.Mechanical?.Rpm ?? 0) > 0)
                runtime += INTERVAL_HOURS;

            runtimeCache[t.MachineId] = runtime;

            newHealthRows.Add(new MachineHealth
            {
                MachineId = t.MachineId,
                RecordedAt = timeUtc,
                HealthScore = (int)Math.Max(0, Math.Min(100, healthScore)),
                RuntimeHours = runtime,
                AvgLoad = avgLoad
            });
        }

        if (newHealthRows.Count > 0)
        {
            await db.MachineHealth.AddRangeAsync(newHealthRows);
            await db.SaveChangesAsync();
        }
    }

    // ======================================================
    // MACHINE STATUS ENGINE
    // ======================================================
    private async Task UpdateMachineStatus(CiipDbContext db)
    {
        var machines = await db.Machines
            .AsTracking()
            .ToDictionaryAsync(x => x.MachineId);

        var latestPerMachine = await db.TelemetryIngestions
            .Include(t => t.Mechanical)
            .OrderByDescending(t => t.RecordedAt)
            .AsNoTracking()
            .ToListAsync();

        var latest = latestPerMachine
            .GroupBy(t => t.MachineId)
            .Select(g => g.First());

        foreach (var t in latest)
        {
            if (!machines.TryGetValue(t.MachineId, out var machine))
                continue;

            decimal rpm = t.Mechanical?.Rpm ?? 0;
            machine.Status = rpm > 200 ? "RUNNING" : "IDLE";
        }

        await db.SaveChangesAsync();
    }

    // ======================================================
    // ALERT ENGINE (NO LOAD ALERTS)
    // ======================================================
    private async Task ProcessAlerts(CiipDbContext db)
    {
        var thresholdService = new ThresholdService(db);

        var machines = await db.Machines
            .Include(m => m.Plant)
            .AsNoTracking()
            .ToDictionaryAsync(x => x.MachineId);

        var telemetry = await db.TelemetryIngestions
            .Include(t => t.Mechanical)
            .Include(t => t.Electrical)
            .Include(t => t.Environmental)
            .AsNoTracking()
            .OrderBy(t => t.RecordedAt)
            .Take(300)
            .ToListAsync();

        if (!telemetry.Any())
            return;

        var newAlerts = new List<AlertEvent>();

        foreach (var t in telemetry)
        {
            if (!machines.TryGetValue(t.MachineId, out var machine))
                continue;

            if (machine.Plant == null)
                continue;

            var threshold = await thresholdService
                .GetThresholds(machine.Plant.TenantId, machine.MachineType);

            var timeUtc = DateTime.SpecifyKind(t.RecordedAt, DateTimeKind.Utc);

            async Task TryAdd(string parameter, decimal value, string severity, Guid? thresholdId)
            {
                bool exists = await db.AlertEvents.AnyAsync(a =>
                    a.MachineId == t.MachineId &&
                    a.Parameter == parameter &&
                    a.GeneratedAt == timeUtc);

                if (!exists)
                {
                    newAlerts.Add(new AlertEvent
                    {
                        AlertId = Guid.NewGuid(),
                        MachineId = t.MachineId,
                        Parameter = parameter,
                        Severity = severity,
                        ActualValue = value,
                        AlertStatus = "ACTIVE",
                        GeneratedAt = timeUtc,
                        ThresholdId = thresholdId
                    });
                }
            }

            decimal vibration =
                ((t.Mechanical?.VibrationX ?? 0)
                + (t.Mechanical?.VibrationY ?? 0)
                + (t.Mechanical?.VibrationZ ?? 0)) / 3m;

            decimal rpm = t.Mechanical?.Rpm ?? 0;
            decimal temperature = t.Environmental?.Temperature ?? 0;

            decimal maxCurrent = new[]
            {
                t.Electrical?.RCurrent ?? 0,
                t.Electrical?.YCurrent ?? 0,
                t.Electrical?.BCurrent ?? 0
            }.Max();

            if (rpm <= 200m)
                await TryAdd("MachineStatus", rpm, "WARNING", null);

            if (vibration >= threshold.VibrationCritical)
                await TryAdd("Vibration", vibration, "CRITICAL", threshold.VibrationThresholdId);
            else if (vibration >= threshold.VibrationWarning)
                await TryAdd("Vibration", vibration, "WARNING", threshold.VibrationThresholdId);

            if (maxCurrent >= threshold.CurrentCritical)
                await TryAdd("Current", maxCurrent, "CRITICAL", threshold.CurrentThresholdId);
            else if (maxCurrent >= threshold.CurrentWarning)
                await TryAdd("Current", maxCurrent, "WARNING", threshold.CurrentThresholdId);

            if (rpm < threshold.RpmCriticalLow)
                await TryAdd("RPM", rpm, "CRITICAL", threshold.RpmLowThresholdId);
            else if (rpm > threshold.RpmCriticalHigh)
                await TryAdd("RPM", rpm, "CRITICAL", threshold.RpmHighThresholdId);

            if (temperature >= threshold.TemperatureCritical)
                await TryAdd("Temperature", temperature, "CRITICAL", threshold.TemperatureThresholdId);
            else if (temperature >= threshold.TemperatureWarning)
                await TryAdd("Temperature", temperature, "WARNING", threshold.TemperatureThresholdId);
        }

        if (newAlerts.Count > 0)
        {
            await db.AlertEvents.AddRangeAsync(newAlerts);
            await db.SaveChangesAsync();
        }
    }
}
