using CIIP.Backend.Data;
using CIIP.Backend.DTOs;
using CIIP.Backend.Entities;
using HotChocolate.Authorization;
using System.Security.Claims;

namespace CIIP.Backend.GraphQL.Mutations;

[ExtendObjectType(typeof(Mutation))]
public class ThresholdMutation
{
    [Authorize]
    public async Task<ThresholdDto> InsertThreshold(
        ClaimsPrincipal user,
        ThresholdInput input,
        [Service] CiipDbContext db)
    {
        var tenantId = Guid.Parse(
            user.FindFirst("tenantId")!.Value
        );

        var entity = new AlertThreshold
        {
            ThresholdId = Guid.NewGuid(),
            TenantId = tenantId,      // ⭐ FROM JWT (NOT INPUT)
            MachineType = input.MachineType,
            Parameter = input.Parameter,
            WarningValue = input.WarningValue,
            CriticalValue = input.CriticalValue,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.AlertThresholds.Add(entity);
        await db.SaveChangesAsync();

        return new ThresholdDto
        {
            Parameter = entity.Parameter,
            WarningValue = entity.WarningValue,
            CriticalValue = entity.CriticalValue,
            MachineType = entity.MachineType,
            TenantId = entity.TenantId
        };
    }
}
