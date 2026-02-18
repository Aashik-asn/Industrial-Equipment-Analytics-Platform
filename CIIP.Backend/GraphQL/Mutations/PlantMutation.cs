using CIIP.Backend.Entities;
using CIIP.Backend.Services;
using HotChocolate;

namespace CIIP.Backend.GraphQL.Mutations;

[ExtendObjectType(typeof(Mutation))]
public class PlantMutation
{
    public async Task<Plant> UpsertPlant(
        Guid tenantId,
        string plantCode,
        string plantName,
        string city,
        [Service] PlantService service)
    {
        return await service.UpsertPlant(
            tenantId,
            plantCode,
            plantName,
            city);
    }
}
