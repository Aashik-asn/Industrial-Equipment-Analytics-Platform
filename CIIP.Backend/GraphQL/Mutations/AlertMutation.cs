using CIIP.Backend.DTOs;
using CIIP.Backend.Services;

namespace CIIP.Backend.GraphQL.Mutations;

[ExtendObjectType(typeof(Mutation))]
public class AlertMutation
{
    public Task<bool> AcknowledgeAlert(
        [Service] AlertService service,
        AcknowledgementDto input)
        => service.Acknowledge(input);
}
