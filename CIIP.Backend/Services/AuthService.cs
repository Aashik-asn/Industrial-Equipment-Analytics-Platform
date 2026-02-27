using CIIP.Backend.Data;
using CIIP.Backend.Entities;
using Microsoft.EntityFrameworkCore;

public class AuthService
{
    private readonly CiipDbContext _db;

    public AuthService(CiipDbContext db)
    {
        _db = db;
    }

    public async Task<Tenant> Register(string tenantName, string email, string passwordHash)
    {
        // Create tenant
        var tenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            TenantName = tenantName,
            Email = email,
            PasswordHash = passwordHash,
            Status = "A",
            CreatedAt = DateTime.UtcNow
        };

        _db.Tenants.Add(tenant);

        // ⭐ Create ADMIN USER automatically
        var user = new UserAccount
        {
            UserId = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            Email = email,
            PasswordHash = passwordHash,
            Role = "ADMIN",
            CreatedAt = DateTime.UtcNow
        };

        _db.UserAccounts.Add(user);

        await _db.SaveChangesAsync();

        return tenant;
    }


    public async Task<UserAccount?> Login(string email, string password)
    {
        return await _db.UserAccounts
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(x => x.Email == email && x.PasswordHash == password);
    }
}
