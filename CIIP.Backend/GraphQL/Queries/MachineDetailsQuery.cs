using CIIP.Backend.DTOs;
using CIIP.Backend.Services;
using HotChocolate.Authorization;
using System.Security.Claims;
namespace CIIP.Backend.GraphQL.Queries;

[ExtendObjectType(typeof(Query))]
public class MachineDetailsQuery
{

    [Authorize]
    public async Task<MachineDetailsResponse> MachineDetails(
    ClaimsPrincipal user,
    Guid plantId,
    Guid machineId,
    DateTime? from,
    DateTime? to,
    [Service] MachineDetailsService service)
    {
        var tenantId = Guid.Parse(
            user.FindFirst("tenantId")!.Value
        );

        return await service.GetMachineDetails(
            tenantId,
            plantId,
            machineId,
            from,
            to
        );
    }
}
