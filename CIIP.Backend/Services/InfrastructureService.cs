using CIIP.Backend.Data;
using CIIP.Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace CIIP.Backend.Services;

public class InfrastructureService
{
    private readonly CiipDbContext _db;

    public InfrastructureService(CiipDbContext db)
    {
        _db = db;
    }

    public async Task<Machine> AddMachine(
    Guid tenantId,
    Guid plantId,
    string machineCode,
    string machineName,
    string machineType,
    string gatewayCode,
    string endpointType,
    string protocol)
    {
        // ⭐ Validate plant belongs to tenant
        var plant = await _db.Plants
            .FirstOrDefaultAsync(p =>
                p.PlantId == plantId &&
                p.TenantId == tenantId);

        if (plant == null)
            throw new Exception("Unauthorized plant access.");

        var gateway = await _db.Gateways
            .FirstOrDefaultAsync(g => g.PlantId == plantId);

        if (gateway == null)
        {
            gateway = new Gateway
            {
                GatewayId = Guid.NewGuid(),
                PlantId = plantId,
                GatewayCode = gatewayCode,
                Status = "ACTIVE",
                LastSeen = DateTime.UtcNow
            };

            _db.Gateways.Add(gateway);
        }

        var machine = new Machine
        {
            MachineId = Guid.NewGuid(),
            PlantId = plantId,
            MachineCode = machineCode,
            MachineName = machineName,
            MachineType = machineType,
            Status = "RUNNING",
            CreatedAt = DateTime.UtcNow
        };

        _db.Machines.Add(machine);

        var endpoint = new DeviceEndpoint
        {
            EndpointId = Guid.NewGuid(),
            GatewayId = gateway.GatewayId,
            EndpointType = endpointType,
            Protocol = protocol,
            Status = "ACTIVE"
        };

        _db.DeviceEndpoints.Add(endpoint);

        _db.EndpointMachineMaps.Add(new EndpointMachineMap
        {
            EndpointId = endpoint.EndpointId,
            MachineId = machine.MachineId
        });

        await _db.SaveChangesAsync();

        return machine;
    }

}
