using CIIP.Backend.DTOs;
using CIIP.Backend.Services;

namespace CIIP.Backend.GraphQL.Queries;

[ExtendObjectType(typeof(Query))]
public class MachineDetailsQuery
{
    public async Task<MachineDetailsResponse> MachineDetails(
        Guid tenantId,
        Guid plantId,
        Guid machineId,
        [Service] MachineDetailsService service)
    {
        return await service.GetMachineDetails(tenantId, plantId, machineId);
    }
}
