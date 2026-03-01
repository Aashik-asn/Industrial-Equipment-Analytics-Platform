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

        // ⭐ Validate machine code uniqueness
        var existingMachine = await _db.Machines
            .FirstOrDefaultAsync(m => m.PlantId == plantId && m.MachineCode == machineCode);
        
        if (existingMachine != null)
            throw new Exception($"Machine code '{machineCode}' already exists in this plant.");

        // ⭐ Find gateway by plantId + gatewayCode
        var gateway = await _db.Gateways
            .FirstOrDefaultAsync(g => 
                g.PlantId == plantId && 
                g.GatewayCode == gatewayCode);

        // If not found → create gateway
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

        // ⭐ Find endpoint by gatewayId + endpointType + protocol
        var endpoint = await _db.DeviceEndpoints
            .FirstOrDefaultAsync(e => 
                e.GatewayId == gateway.GatewayId &&
                e.EndpointType == endpointType &&
                e.Protocol == protocol);

        // If not found → create endpoint
        if (endpoint == null)
        {
            endpoint = new DeviceEndpoint
            {
                EndpointId = Guid.NewGuid(),
                GatewayId = gateway.GatewayId,
                EndpointType = endpointType,
                Protocol = protocol,
                Status = "ACTIVE"
            };

            _db.DeviceEndpoints.Add(endpoint);
        }

        // Create machine
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

        _db.EndpointMachineMaps.Add(new EndpointMachineMap
        {
            EndpointId = endpoint.EndpointId,
            MachineId = machine.MachineId
        });

        await _db.SaveChangesAsync();

        return machine;
    }

}
