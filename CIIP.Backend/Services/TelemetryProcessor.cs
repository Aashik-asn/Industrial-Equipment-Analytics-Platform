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
    // MACHINE HEALTH ENGINE
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

        foreach (var t in telemetry)
        {
            var timeUtc = DateTime.SpecifyKind(t.RecordedAt, DateTimeKind.Utc);

            double healthScore =
                100
                - (double)(t.Mechanical?.Vibration ?? 0) * 2
                - (double)(t.Electrical?.RCurrent ?? 0) * 0.1;

            decimal runtime = runtimeCache.ContainsKey(t.MachineId)
                ? runtimeCache[t.MachineId]
                : 0;

            if ((t.Mechanical?.Rpm ?? 0) > 0)
                runtime += INTERVAL_HOURS;

            runtimeCache[t.MachineId] = runtime;

            decimal avgLoad = 0;
            if (t.Electrical != null)
            {
                avgLoad =
                    ((t.Electrical.RCurrent ?? 0)
                    + (t.Electrical.YCurrent ?? 0)
                    + (t.Electrical.BCurrent ?? 0)) / 3m;
            }

            if (newHealthRows.Any(x =>
                x.MachineId == t.MachineId &&
                x.RecordedAt == timeUtc))
                continue;

            newHealthRows.Add(new MachineHealth
            {
                MachineId = t.MachineId,
                RecordedAt = timeUtc,
                HealthScore = (int)Math.Max(0, healthScore),
                RuntimeHours = runtime,
                AvgLoad = avgLoad
            });
        }

        await db.MachineHealth.AddRangeAsync(newHealthRows);
        await db.SaveChangesAsync();
    }

    // ======================================================
    // MACHINE STATUS ENGINE
    // ======================================================
    private async Task UpdateMachineStatus(CiipDbContext db)
    {
        var machines = await db.Machines
            .Include(m => m.Plant)       // ✅ needed for tenant resolution later
            .AsTracking()
            .ToDictionaryAsync(x => x.MachineId);

        var latestPerMachine = await db.TelemetryIngestions
            .Include(t => t.Mechanical)
            .OrderByDescending(t => t.RecordedAt)
            .AsNoTracking()
            .ToListAsync();

        var latest = latestPerMachine
            .GroupBy(t => t.MachineId)
            .Select(g => g.First())
            .ToList();

        foreach (var t in latest)
        {
            if (!machines.TryGetValue(t.MachineId, out var machine))
                continue;

            decimal rpm = (decimal)(t.Mechanical?.Rpm ?? 0);
            machine.Status = rpm > 200 ? "RUNNING" : "IDLE";
        }

        await db.SaveChangesAsync();
    }

    // ======================================================
    // ALERT ENGINE
    // ======================================================
    private async Task ProcessAlerts(CiipDbContext db)
    {
        var thresholdService = new ThresholdService(db);

        var machines = await db.Machines
            .Include(m => m.Plant)     // ⭐ CRITICAL FIX
            .AsNoTracking()
            .ToDictionaryAsync(x => x.MachineId);

        var activeAlertSet = (await db.AlertEvents
            .Where(a => a.AlertStatus == "ACTIVE")
            .Select(a => new { a.MachineId, a.Parameter })
            .ToListAsync())
            .Select(a => (a.MachineId, a.Parameter))
            .ToHashSet();

        bool HasActive(Guid machineId, string parameter) =>
            activeAlertSet.Contains((machineId, parameter));

        var telemetry = await db.TelemetryIngestions
            .Include(t => t.Mechanical)
            .Include(t => t.Electrical)
            .Include(t => t.Environmental)
            .OrderByDescending(t => t.RecordedAt)
            .Take(300)
            .AsNoTracking()
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

            decimal vibration = (decimal)(t.Mechanical?.Vibration ?? 0);
            decimal rpm = (decimal)(t.Mechanical?.Rpm ?? 0);
            decimal temperature = (decimal)(t.Environmental?.Temperature ?? 0);

            decimal maxCurrent = new[]
            {
                t.Electrical?.RCurrent ?? 0,
                t.Electrical?.YCurrent ?? 0,
                t.Electrical?.BCurrent ?? 0
            }.Max();

            // VIBRATION
            if (!HasActive(t.MachineId, "Vibration"))
            {
                if (vibration >= threshold.VibrationCritical)
                    newAlerts.Add(CreateAlert(t.MachineId, "Vibration", vibration, "CRITICAL", timeUtc, threshold.VibrationThresholdId));
                else if (vibration >= threshold.VibrationWarning)
                    newAlerts.Add(CreateAlert(t.MachineId, "Vibration", vibration, "WARNING", timeUtc, threshold.VibrationThresholdId));
            }

            // CURRENT
            if (!HasActive(t.MachineId, "Current"))
            {
                if (maxCurrent >= threshold.CurrentCritical)
                    newAlerts.Add(CreateAlert(t.MachineId, "Current", maxCurrent, "CRITICAL", timeUtc, threshold.CurrentThresholdId));
                else if (maxCurrent >= threshold.CurrentWarning)
                    newAlerts.Add(CreateAlert(t.MachineId, "Current", maxCurrent, "WARNING", timeUtc, threshold.CurrentThresholdId));
            }

            // RPM (FIXED VERSION LOCKING)
            if (!HasActive(t.MachineId, "RPM"))
            {
                if (rpm < threshold.RpmCriticalLow)
                    newAlerts.Add(CreateAlert(t.MachineId, "RPM", rpm, "CRITICAL", timeUtc, threshold.RpmLowThresholdId));

                else if (rpm > threshold.RpmCriticalHigh)
                    newAlerts.Add(CreateAlert(t.MachineId, "RPM", rpm, "CRITICAL", timeUtc, threshold.RpmHighThresholdId));

                else if (rpm < threshold.RpmWarningLow)
                    newAlerts.Add(CreateAlert(t.MachineId, "RPM", rpm, "WARNING", timeUtc, threshold.RpmLowThresholdId));

                else if (rpm > threshold.RpmWarningHigh)
                    newAlerts.Add(CreateAlert(t.MachineId, "RPM", rpm, "WARNING", timeUtc, threshold.RpmHighThresholdId));
            }

            // TEMPERATURE
            if (!HasActive(t.MachineId, "Temperature"))
            {
                if (temperature >= threshold.TemperatureCritical)
                    newAlerts.Add(CreateAlert(t.MachineId, "Temperature", temperature, "CRITICAL", timeUtc, threshold.TemperatureThresholdId));
                else if (temperature >= threshold.TemperatureWarning)
                    newAlerts.Add(CreateAlert(t.MachineId, "Temperature", temperature, "WARNING", timeUtc, threshold.TemperatureThresholdId));
            }
        }

        // MACHINE STATUS ALERT
        var latestStatusTelemetry = telemetry
            .GroupBy(t => t.MachineId)
            .Select(g => g.OrderByDescending(x => x.RecordedAt).First())
            .ToList();

        foreach (var t in latestStatusTelemetry)
        {
            if (!machines.TryGetValue(t.MachineId, out var machine))
                continue;

            if (machine.Status == "IDLE" && !HasActive(t.MachineId, "MachineStatus"))
            {
                newAlerts.Add(CreateAlert(
                    t.MachineId,
                    "MachineStatus",
                    0,
                    "WARNING",
                    DateTime.SpecifyKind(t.RecordedAt, DateTimeKind.Utc),
                    null));
            }
        }

        if (newAlerts.Count > 0)
        {
            await db.AlertEvents.AddRangeAsync(newAlerts);
            await db.SaveChangesAsync();
        }
    }

    private AlertEvent CreateAlert(
        Guid machineId,
        string parameter,
        decimal value,
        string severity,
        DateTime timeUtc,
        Guid? thresholdId)
    {
        return new AlertEvent
        {
            AlertId = Guid.NewGuid(),
            MachineId = machineId,
            Parameter = parameter,
            Severity = severity,
            ActualValue = value,
            AlertStatus = "ACTIVE",
            GeneratedAt = timeUtc,
            ThresholdId = thresholdId
        };
    }
}
