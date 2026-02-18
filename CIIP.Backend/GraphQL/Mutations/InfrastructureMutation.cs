using CIIP.Backend.Entities;
using CIIP.Backend.Services;
using HotChocolate;

namespace CIIP.Backend.GraphQL.Mutations;

[ExtendObjectType(typeof(Mutation))]
public class InfrastructureMutation
{
    public async Task<Machine> AddMachine(
        Guid plantId,
        string machineCode,
        string machineName,
        string machineType,
        string gatewayCode,
        string endpointType,
        string protocol,
        [Service] InfrastructureService service)
    {
        return await service.AddMachine(
            plantId,
            machineCode,
            machineName,
            machineType,
            gatewayCode,
            endpointType,
            protocol);
    }
}
