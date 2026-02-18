using CIIP.Backend.Data;
using CIIP.Backend.DTOs;
using CIIP.Backend.Entities;
using CIIP.Backend.GraphQL.Mutations;

[ExtendObjectType(typeof(Mutation))]
public class ThresholdMutation
{
    public async Task<ThresholdDto> InsertThreshold(
        ThresholdInput input,
        [Service] CiipDbContext db)
    {
        var entity = new AlertThreshold
        {
            ThresholdId = Guid.NewGuid(),
            TenantId = input.TenantId,
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
