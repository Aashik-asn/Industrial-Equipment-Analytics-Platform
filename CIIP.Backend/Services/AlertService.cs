using CIIP.Backend.Data;
using CIIP.Backend.DTOs;
using CIIP.Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace CIIP.Backend.Services;

public class AlertService
{
    private readonly CiipDbContext _db;

    public AlertService(CiipDbContext db)
    {
        _db = db;
    }

    // ======================================================
    // GET ALERT LIST
    // ======================================================
    public async Task<List<AlertDto>> GetAlerts(
        Guid tenantId,
        Guid? plantId,
        string? severity,
        string? status,
        DateTime? from,
        DateTime? to)
    {
        var query =
            from alert in _db.AlertEvents
            join machine in _db.Machines on alert.MachineId equals machine.MachineId
            join plant in _db.Plants on machine.PlantId equals plant.PlantId
            join ack in _db.AlertAcknowledgements
                on alert.AlertId equals ack.AlertId into ackJoin
            from ack in ackJoin.DefaultIfEmpty()
            where plant.TenantId == tenantId
            select new { alert, machine, plant, ack };

        if (plantId.HasValue)
            query = query.Where(x => x.plant.PlantId == plantId);

        if (!string.IsNullOrEmpty(severity))
            query = query.Where(x => x.alert.Severity == severity);

        if (from.HasValue)
            query = query.Where(x => x.alert.GeneratedAt >= from);

        if (to.HasValue)
            query = query.Where(x => x.alert.GeneratedAt <= to);

        var data = await query
            .OrderByDescending(x => x.alert.GeneratedAt)
            .ToListAsync();

        var result = data.Select(x =>
        {
            var alertStatus = x.ack != null ? "ACKNOWLEDGED" : "PENDING";

            return new AlertDto
            {
                AlertId = x.alert.AlertId,
                Severity = x.alert.Severity,
                Parameter = x.alert.Parameter,
                ActualValue = x.alert.ActualValue??0,

                PlantName = x.plant.PlantName,
                MachineCode = x.machine.MachineCode,
                MachineName = x.machine.MachineName,

                GeneratedAt = x.alert.GeneratedAt,
                Status = alertStatus
            };
        }).ToList();

        if (!string.IsNullOrEmpty(status))
            result = result.Where(x => x.Status == status).ToList();

        return result;
    }

    // ======================================================
    // SUMMARY COUNTS (TOP CARDS)
    // ======================================================
    public async Task<AlertSummaryDto> GetSummary(Guid tenantId)
    {
        var baseQuery =
            from alert in _db.AlertEvents
            join machine in _db.Machines on alert.MachineId equals machine.MachineId
            join plant in _db.Plants on machine.PlantId equals plant.PlantId
            where plant.TenantId == tenantId
            select alert;

        var critical = await baseQuery.CountAsync(x => x.Severity == "CRITICAL");
        var warning = await baseQuery.CountAsync(x => x.Severity == "WARNING");

        var acknowledged =
            await (from ack in _db.AlertAcknowledgements
                   join alert in baseQuery on ack.AlertId equals alert.AlertId
                   select ack).CountAsync();

        return new AlertSummaryDto
        {
            Critical = critical,
            Warning = warning,
            Acknowledged = acknowledged
        };
    }
    public async Task<AcknowledgedAlertDto?> GetAcknowledgedAlert(
    Guid tenantId,
    Guid alertId)
    {
        var query =
            from aa in _db.AlertAcknowledgements
            join ae in _db.AlertEvents
                on aa.AlertId equals ae.AlertId
            join m in _db.Machines
                on ae.MachineId equals m.MachineId
            join p in _db.Plants
                on m.PlantId equals p.PlantId
            where ae.AlertId == alertId
                  && p.TenantId == tenantId
            select new AcknowledgedAlertDto
            {
                AlertId = ae.AlertId,
                Parameter = ae.Parameter,
                PlantName = p.PlantName,
                MachineCode = m.MachineCode,
                TechnicianName = aa.TechnicianName,
                Reason = aa.Reason,
                ActionTaken = aa.ActionTaken,
                AcknowledgedAt = aa.AcknowledgedAt
            };

        return await query.FirstOrDefaultAsync();
    }

    // ======================================================
    // ACKNOWLEDGE ALERT
    // ======================================================
    public async Task<bool> Acknowledge(
    AcknowledgementDto dto,
    Guid tenantId)
    {
        var exists = await _db.AlertAcknowledgements
            .AnyAsync(x => x.AlertId == dto.AlertId);

        if (exists)
            throw new Exception("Alert already acknowledged.");

        // ⭐ Validate tenant ownership
        var alert = await _db.AlertEvents
            .Include(a => a.Machine!)
            .ThenInclude(m => m.Plant!)
            .FirstOrDefaultAsync(a =>
                a.AlertId == dto.AlertId &&
                a.Machine!.Plant!.TenantId == tenantId);

        if (alert == null)
            throw new Exception("Unauthorized alert access.");

        var ack = new AlertAcknowledgement
        {
            AcknowledgementId = Guid.NewGuid(),
            AlertId = dto.AlertId,
            TechnicianName = dto.TechnicianName,
            Reason = dto.Reason,
            ActionTaken = dto.ActionTaken,
            AcknowledgedAt = DateTime.UtcNow
        };

        _db.AlertAcknowledgements.Add(ack);

        alert.AlertStatus = "ACKNOWLEDGED";

        await _db.SaveChangesAsync();

        return true;
    }

}
