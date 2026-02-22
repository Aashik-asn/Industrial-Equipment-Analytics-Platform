using CIIP.Backend.DTOs;
using CIIP.Backend.Services;
using HotChocolate.Authorization;
using System.Security.Claims;

namespace CIIP.Backend.GraphQL.Mutations;

[ExtendObjectType(typeof(Mutation))]
public class AlertMutation
{
    [Authorize]
    public Task<bool> AcknowledgeAlert(
        ClaimsPrincipal user,
        [Service] AlertService service,
        AcknowledgementDto input)
    {
        var tenantId = Guid.Parse(
            user.FindFirst("tenantId")!.Value
        );

        return service.Acknowledge(input, tenantId); // ⭐ add tenant validation
    }
}
