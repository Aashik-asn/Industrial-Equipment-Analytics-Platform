using CIIP.Backend.DTOs.Dashboard;
using CIIP.Backend.Services;
using HotChocolate;

namespace CIIP.Backend.GraphQL.Queries;

[ExtendObjectType(typeof(Query))]
public class DashboardQuery
{
    public async Task<DashboardResponse> Dashboard(
        Guid tenantId,
        Guid? plantId,
        DateTime? from,
        DateTime? to,
        [Service] DashboardService service)
    {
        return await service.GetDashboard(tenantId, plantId, from, to);
    }
}
