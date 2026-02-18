using CIIP.Backend.Data;
using CIIP.Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace CIIP.Backend.Services;

public class ProfileService
{
    private readonly CiipDbContext _db;

    public ProfileService(CiipDbContext db)
    {
        _db = db;
    }

    public async Task<UserAccount?> GetProfile(Guid userId)
    {
        return await _db.UserAccounts
            .FirstOrDefaultAsync(x => x.UserId == userId);
    }

    public async Task<UserAccount?> UpdateProfile(Guid userId, string email)
    {
        var user = await _db.UserAccounts
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (user == null) return null;

        user.Email = email;

        await _db.SaveChangesAsync();

        return user;
    }

    public async Task<bool> ChangePassword(Guid userId, string newPassword)
    {
        var user = await _db.UserAccounts
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (user == null) return false;

        user.PasswordHash = newPassword;

        await _db.SaveChangesAsync();

        return true;
    }
    public async Task<Tenant?> UpdateTenantName(Guid tenantId, string tenantName)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId);
        if (tenant == null) return null;

        tenant.TenantName = tenantName;
        await _db.SaveChangesAsync();

        return tenant;
    }

}
