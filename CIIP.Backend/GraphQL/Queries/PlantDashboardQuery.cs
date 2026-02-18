using CIIP.Backend.Services;

namespace CIIP.Backend.GraphQL.Queries;

[ExtendObjectType("Query")]
public class PlantDashboardQuery
{
    public async Task<PlantDashboardResponse> GetPlantDashboard(
        Guid tenantId,
        Guid plantId,
        DateTime? from,
        DateTime? to,
        [Service] PlantDashboardService service)
    {
        return await service.GetPlantDashboard(
            tenantId,
            plantId,
            from,
            to);
    }
}
