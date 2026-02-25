using CIIP.Backend.DTOs;
using CIIP.Backend.Services;
using HotChocolate.Authorization;
using System.Security.Claims;

namespace CIIP.Backend.GraphQL.Queries;

[ExtendObjectType(typeof(Query))]
public class AlertQuery
{
    // ======================================================
    // ALERT LIST
    // ======================================================

    [Authorize]
    public Task<List<AlertDto>> Alerts(
        [Service] AlertService service,
        ClaimsPrincipal user,
        Guid? plantId,
        string? severity,
        string? status,
        DateTime? fromDate,
        DateTime? toDate)
    {
        var tenantId = Guid.Parse(
            user.FindFirst("tenantId")!.Value
        );

        return service.GetAlerts(
            tenantId,
            plantId,
            severity,
            status,
            fromDate,
            toDate);
    }

    // ======================================================
    // ALERT SUMMARY
    // ======================================================

    [Authorize]
    public Task<AlertSummaryDto> AlertSummary(
        [Service] AlertService service,
        ClaimsPrincipal user)
    {
        Console.WriteLine("######## ALERT SUMMARY EXECUTED ########");
        Console.WriteLine("JWT TenantId = " + user.FindFirst("tenantId")?.Value);

        var tenantId = Guid.Parse(
            user.FindFirst("tenantId")!.Value
        );

        return service.GetSummary(tenantId);
    }
    [Authorize]
    public async Task<AcknowledgedAlertDto?> AcknowledgedAlert(
    ClaimsPrincipal user,
    Guid alertId,
    [Service] AlertService service)
    {
        var tenantId = Guid.Parse(
            user.FindFirst("tenantId")!.Value
        );

        return await service.GetAcknowledgedAlert(tenantId, alertId);
    }
}
