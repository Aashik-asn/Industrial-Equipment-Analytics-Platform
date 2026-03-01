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
    // MACHINE HEALTH ENGINE (UNCHANGED)
    // ======================================================
    private async Task ProcessMachineHealth(CiipDbContext db)
    {
        var lastHealthPerMachine = await db.MachineHealth
            .GroupBy(h => h.MachineId)
            .Select(g => new
            {
                MachineId = g.Key,
                LastTime = g.Max(x => x.RecordedAt),
                Runtime = g.OrderByDescending(x => x.RecordedAt)
                           .Select(x => x.RuntimeHours)
                           .FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.MachineId);

        var telemetry = await db.TelemetryIngestions
            .Include(t => t.Mechanical)
            .Include(t => t.Electrical)
            .AsNoTracking()
            .OrderByDescending(t => t.RecordedAt)
            .Take(500)
            .OrderBy(t => t.RecordedAt)
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

        var newHealthRows = new List<MachineHealth>();

        foreach (var t in telemetry)
        {
            var timeUtc = t.RecordedAt;

            bool exists = await db.MachineHealth.AnyAsync(h =>
                h.MachineId == t.MachineId &&
                h.RecordedAt == timeUtc);

            if (exists)
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

            // ======================================================
            // ✅ NEW RUNTIME LOGIC (TIMESTAMP BASED)
            // ======================================================

            decimal runtime = 0;

            if (lastHealthPerMachine.TryGetValue(t.MachineId, out var last))
            {
                runtime = last.Runtime;

                if ((t.Mechanical?.Rpm ?? 0) > 0)
                {
                    var delta =
                        (timeUtc - last.LastTime)
                        .TotalHours;

                    if (delta > 0)
                        runtime += (decimal)delta;
                }

                lastHealthPerMachine[t.MachineId] =
                    new { last.MachineId, LastTime = timeUtc, Runtime = runtime };
            }
            else
            {
                runtime = 0;
                lastHealthPerMachine[t.MachineId] =
                    new { MachineId = t.MachineId, LastTime = timeUtc, Runtime = runtime };
            }

            newHealthRows.Add(new MachineHealth
            {
                MachineId = t.MachineId,
                RecordedAt = timeUtc,   // ✔ EXACT MATCH WITH TELEMETRY
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
    // MACHINE STATUS ENGINE (UNCHANGED)
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
    // ALERT ENGINE (ONLY LOAD LOGIC CHANGED)
    // ======================================================
    // ONLY REPLACE ProcessAlerts METHOD

    private async Task ProcessAlerts(CiipDbContext db)
    {
        var thresholdService = new ThresholdService(db);

        var machines = await db.Machines
            .Include(m => m.Plant)
            .AsNoTracking()
            .ToDictionaryAsync(x => x.MachineId);

        var lastAlertTime = await db.AlertEvents
            .OrderByDescending(a => a.GeneratedAt)
            .Select(a => (DateTime?)a.GeneratedAt)
            .FirstOrDefaultAsync();

        var telemetryQuery = db.TelemetryIngestions
            .Include(t => t.Mechanical)
            .Include(t => t.Electrical)
            .Include(t => t.Environmental)
            .AsNoTracking();

        if (lastAlertTime.HasValue)
            telemetryQuery = telemetryQuery
                .Where(t => t.RecordedAt > lastAlertTime.Value);

        var lastProcessedTime = await db.AlertEvents
    .OrderByDescending(a => a.GeneratedAt)
    .Select(a => (DateTime?)a.GeneratedAt)
    .FirstOrDefaultAsync();

        if (lastProcessedTime.HasValue)
        {
            telemetryQuery = telemetryQuery
                .Where(t => t.RecordedAt > lastProcessedTime.Value);
        }

        var telemetry = await telemetryQuery
            .OrderBy(t => t.RecordedAt)
            .ToListAsync();

        if (!telemetry.Any())
            return;

        foreach (var t in telemetry)
        {
            if (!machines.TryGetValue(t.MachineId, out var machine))
                continue;

            if (machine.Plant == null)
                continue;

            var threshold = await thresholdService.GetThresholds(
                machine.Plant.TenantId,
                machine.MachineType,
                t.RecordedAt);

            decimal rpm = t.Mechanical?.Rpm ?? 0;
            decimal vibration =
                ((t.Mechanical?.VibrationX ?? 0)
                + (t.Mechanical?.VibrationY ?? 0)
                + (t.Mechanical?.VibrationZ ?? 0)) / 3m;

            decimal temperature = t.Environmental?.Temperature ?? 0;

            decimal maxCurrent = new[]
            {
            t.Electrical?.RCurrent ?? 0,
            t.Electrical?.YCurrent ?? 0,
            t.Electrical?.BCurrent ?? 0
        }.Max();

            // 🔧 Generic state-based alert handler
            async Task HandleAlert(
                string parameter,
                bool condition,
                string severity,
                decimal value,
                Guid? thresholdId)
            {
                var activeAlert = await db.AlertEvents
                    .FirstOrDefaultAsync(a =>
                        a.MachineId == t.MachineId &&
                        a.Parameter == parameter &&
                        a.AlertStatus == "ACTIVE");

                if (condition)
                {
                    if (activeAlert == null)
                    {
                        db.AlertEvents.Add(new AlertEvent
                        {
                            AlertId = Guid.NewGuid(),
                            MachineId = t.MachineId,
                            Parameter = parameter,
                            Severity = severity,
                            ActualValue = value,
                            AlertStatus = "ACTIVE",
                            GeneratedAt = t.RecordedAt,
                            ThresholdId = thresholdId
                        });
                    }
                }
                else
                {
                    if (activeAlert != null)
                    {
                        activeAlert.AlertStatus = "PENDING";
                    }
                }
            }

            // ================= MACHINE STATUS =================
            await HandleAlert(
                "MachineStatus",
                rpm <= 200m,
                "WARNING",
                rpm,
                null);

            // ================= RPM HIGH =================
            await HandleAlert(
                "RPM_HIGH",
                rpm >= threshold.RpmWarningHigh,
                rpm >= threshold.RpmCriticalHigh ? "CRITICAL" : "WARNING",
                rpm,
                threshold.RpmHighThresholdId);

            // ================= RPM LOW =================
            await HandleAlert(
                "RPM_LOW",
                rpm <= threshold.RpmWarningLow,
                rpm <= threshold.RpmCriticalLow ? "CRITICAL" : "WARNING",
                rpm,
                threshold.RpmLowThresholdId);

            // ================= VIBRATION =================
            await HandleAlert(
                "Vibration",
                vibration >= threshold.VibrationWarning,
                vibration >= threshold.VibrationCritical ? "CRITICAL" : "WARNING",
                vibration,
                threshold.VibrationThresholdId);

            // ================= CURRENT =================
            await HandleAlert(
                "Current",
                maxCurrent >= threshold.CurrentWarning,
                maxCurrent >= threshold.CurrentCritical ? "CRITICAL" : "WARNING",
                maxCurrent,
                threshold.CurrentThresholdId);

            // ================= TEMPERATURE =================
            await HandleAlert(
                "Temperature",
                temperature >= threshold.TemperatureWarning,
                temperature >= threshold.TemperatureCritical ? "CRITICAL" : "WARNING",
                temperature,
                threshold.TemperatureThresholdId);
        }

        // ================= LOAD ALERTS (STATE BASED) =================

        var lastLoadAlertTime = await db.AlertEvents
    .Where(a => a.Parameter == "LOAD_HIGH")
    .OrderByDescending(a => a.GeneratedAt)
    .Select(a => (DateTime?)a.GeneratedAt)
    .FirstOrDefaultAsync();

        var healthQuery = db.MachineHealth.AsNoTracking();

        if (lastLoadAlertTime.HasValue)
        {
            healthQuery = healthQuery
                .Where(h => h.RecordedAt > lastLoadAlertTime.Value);
        }

        var healthRows = await healthQuery
            .OrderBy(h => h.RecordedAt)
            .ToListAsync();

        foreach (var h in healthRows)
        {
            if (!machines.TryGetValue(h.MachineId, out var machine))
                continue;

            if (machine.Plant == null)
                continue;

            var threshold = await thresholdService.GetThresholds(
                machine.Plant.TenantId,
                machine.MachineType,
                h.RecordedAt);

            var activeAlert = await db.AlertEvents
                .FirstOrDefaultAsync(a =>
                    a.MachineId == h.MachineId &&
                    a.Parameter == "LOAD_HIGH" &&
                    a.AlertStatus == "ACTIVE");

            bool condition = h.AvgLoad >= threshold.LoadHighWarning;

            if (condition)
            {
                if (activeAlert == null)
                {
                    db.AlertEvents.Add(new AlertEvent
                    {
                        AlertId = Guid.NewGuid(),
                        MachineId = h.MachineId,
                        Parameter = "LOAD_HIGH",
                        Severity = h.AvgLoad >= threshold.LoadHighCritical ? "CRITICAL" : "WARNING",
                        ActualValue = h.AvgLoad,
                        AlertStatus = "ACTIVE",
                        GeneratedAt = h.RecordedAt,
                        ThresholdId = threshold.LoadHighThresholdId
                    });
                }
            }
            else
            {
                if (activeAlert != null)
                    activeAlert.AlertStatus = "PENDING";
            }
        }

        // ================= ACKNOWLEDGEMENT SYNC =================

        var acknowledgedIds = await db.AlertAcknowledgements
            .Select(x => x.AlertId)
            .ToListAsync();

        var activeAlerts = await db.AlertEvents
            .Where(a =>
                a.AlertStatus == "ACTIVE" &&
                acknowledgedIds.Contains(a.AlertId))
            .ToListAsync();

        foreach (var alert in activeAlerts)
        {
            alert.AlertStatus = "ACKNOWLEDGED";
        }

        await db.SaveChangesAsync();
    }
}