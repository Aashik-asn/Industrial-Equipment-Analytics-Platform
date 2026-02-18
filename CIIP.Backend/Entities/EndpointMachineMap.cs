using System.ComponentModel.DataAnnotations.Schema;

namespace CIIP.Backend.Entities;

[Table("endpoint_machine_map", Schema = "ciip")]
public class EndpointMachineMap
{
    [Column("endpoint_id")]
    public Guid EndpointId { get; set; }

    [Column("machine_id")]
    public Guid MachineId { get; set; }

    public DeviceEndpoint? Endpoint { get; set; }
    public Machine? Machine { get; set; }

}
