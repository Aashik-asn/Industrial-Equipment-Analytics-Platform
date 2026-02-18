using CIIP.Backend.DTOs;
using CIIP.Backend.GraphQL.Mutations;
using CIIP.Backend.Services;

namespace CIIP.Backend.GraphQL.Queries;

[ExtendObjectType(typeof(Query))]
public class AlertQuery
{
    public Task<List<AlertDto>> GetAlerts(
        [Service] AlertService service,
        Guid tenantId,
        Guid? plantId,
        string? severity,
        string? status,
        DateTime? fromDate,
        DateTime? toDate)
        => service.GetAlerts(
            tenantId,
            plantId,
            severity,
            status,
            fromDate,
            toDate);

    public Task<AlertSummaryDto> GetAlertSummary(
        [Service] AlertService service,
        Guid tenantId)
        => service.GetSummary(tenantId);
}
