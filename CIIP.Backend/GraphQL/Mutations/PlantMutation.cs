using CIIP.Backend.Entities;
using CIIP.Backend.Services;
using HotChocolate.Authorization;
using System.Security.Claims;

namespace CIIP.Backend.GraphQL.Mutations;

[ExtendObjectType(typeof(Mutation))]
public class PlantMutation
{
    [Authorize]
    public async Task<Plant> UpsertPlant(
        ClaimsPrincipal user,
        string plantCode,
        string plantName,
        string city,
        [Service] PlantService service)
    {
        var tenantId = Guid.Parse(
            user.FindFirst("tenantId")!.Value
        );

        return await service.UpsertPlant(
            tenantId,
            plantCode,
            plantName,
            city);
    }
}
