using CIIP.Backend.Services;
using HotChocolate.Authorization;
using System.Security.Claims;

namespace CIIP.Backend.GraphQL.Queries;

[ExtendObjectType("Query")]
public class PlantDashboardQuery
{
    [Authorize]
    public async Task<PlantDashboardResponse> GetPlantDashboard(
        [Service] PlantDashboardService service,
        ClaimsPrincipal user,
        Guid plantId,
        DateTime? from,
        DateTime? to)
    {
        var tenantId = Guid.Parse(
            user.FindFirst("tenantId")!.Value
        );

        return await service.GetPlantDashboard(
            tenantId,
            plantId,
            from,
            to);
    }
}
