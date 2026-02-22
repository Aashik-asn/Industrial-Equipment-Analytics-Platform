using CIIP.Backend.Entities;
using CIIP.Backend.Services;
using HotChocolate.Authorization;
using System.Security.Claims;

namespace CIIP.Backend.GraphQL.Mutations;

[ExtendObjectType(typeof(Mutation))]
public class InfrastructureMutation
{
    [Authorize]
    public async Task<Machine> AddMachine(
        ClaimsPrincipal user,
        Guid plantId,
        string machineCode,
        string machineName,
        string machineType,
        string gatewayCode,
        string endpointType,
        string protocol,
        [Service] InfrastructureService service)
    {
        var tenantId = Guid.Parse(
            user.FindFirst("tenantId")!.Value
        );

        return await service.AddMachine(
            tenantId,        // ⭐ add this parameter in service
            plantId,
            machineCode,
            machineName,
            machineType,
            gatewayCode,
            endpointType,
            protocol);
    }
}
