using CIIP.Backend.Entities;
using CIIP.Backend.Services;
using HotChocolate;

namespace CIIP.Backend.GraphQL.Mutations;

[ExtendObjectType(typeof(Mutation))]
public class ProfileMutation
{
    public async Task<UserAccount?> UpdateProfile(
        Guid userId,
        string email,
        [Service] ProfileService service)
    {
        return await service.UpdateProfile(userId, email);
    }

    public async Task<bool> ChangePassword(
        Guid userId,
        string newPassword,
        [Service] ProfileService service)
    {
        return await service.ChangePassword(userId, newPassword);
    }

    public async Task<Tenant?> UpdateTenantName(
        Guid tenantId,
        string tenantName,
        [Service] ProfileService service)
    {
        return await service.UpdateTenantName(tenantId, tenantName);
    }
}
