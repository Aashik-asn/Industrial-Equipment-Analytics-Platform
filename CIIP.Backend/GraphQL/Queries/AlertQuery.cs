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
    public Task<AlertDashboardDto> AlertDashboard(
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

        return service.GetAlertDashboard(
            tenantId,
            plantId,
            severity,
            status,
            fromDate,
            toDate);
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
