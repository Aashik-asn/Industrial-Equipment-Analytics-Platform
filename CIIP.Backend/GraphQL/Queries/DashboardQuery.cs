using CIIP.Backend.DTOs.Dashboard;
using CIIP.Backend.Services;
using HotChocolate.Authorization;
using System.Security.Claims;

namespace CIIP.Backend.GraphQL.Queries;

[ExtendObjectType(typeof(Query))]
public class DashboardQuery
{
    [Authorize]
    public async Task<DashboardResponse> Dashboard(
        [Service] DashboardService service,
        ClaimsPrincipal user,
        Guid? plantId,
        DateTime? from,
        DateTime? to)
    {
        var tenantId = Guid.Parse(
            user.FindFirst("tenantId")!.Value
        );

        return await service.GetDashboard(
            tenantId,
            plantId,
            from,
            to);
    }
}
