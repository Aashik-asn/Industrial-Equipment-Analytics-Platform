using CIIP.Backend.Entities;

namespace CIIP.Backend.GraphQL.Mutations;

[ExtendObjectType(typeof(Mutation))]
public class AuthMutation
{
    public async Task<Tenant> RegisterTenant(
        string tenantName,
        string email,
        string password,
        [Service] AuthService service)
    {
        // hashing skipped for now
        return await service.Register(tenantName, email, password);
    }

    public async Task<UserAccount?> Login(
        string email,
        string password,
        [Service] AuthService service)
    {
        return await service.Login(email, password);
    }
}
