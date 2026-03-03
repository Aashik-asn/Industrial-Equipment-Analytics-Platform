using CIIP.Backend.Data;
using CIIP.Backend.DTOs;
using CIIP.Backend.Entities;
using Microsoft.EntityFrameworkCore;

public class AuthService
{
    private readonly CiipDbContext _db;

    public AuthService(CiipDbContext db)
    {
        _db = db;
    }

    public async Task<Tenant> Register(
    string tenantName,
    string firstName,
    string lastName,
    string email,
    string password)
    {
        // Create tenant
        var tenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            TenantName = tenantName,
            Status = "A",
            CreatedAt = DateTime.UtcNow
        };

        _db.Tenants.Add(tenant);

        // Create ADMIN USER automatically
        var user = new UserAccount
        {
            UserId = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            FirstName = firstName,        // ✅ NEW
            LastName = lastName,          // ✅ NEW
            Email = email,
            PasswordHash = password,      // (you should hash this properly later)
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
    public async Task CreateUser(Guid tenantId, CreateUserInput input)
    {
        // Optional: prevent duplicate email inside same tenant
        var exists = await _db.UserAccounts
            .AnyAsync(u => u.Email == input.Email && u.TenantId == tenantId);

        if (exists)
            throw new Exception("User already exists");

        var user = new UserAccount
        {
            UserId = Guid.NewGuid(),
            TenantId = tenantId,
            FirstName = input.FirstName,
            LastName = input.LastName,
            Email = input.Email,
            PasswordHash = input.Password, // hash later properly
            Role = input.Role,
            CreatedAt = DateTime.UtcNow
        };

        _db.UserAccounts.Add(user);
        await _db.SaveChangesAsync();
    }
    public async Task UpdateUserName(Guid userId, UpdateUserNameInput input)
    {
        var user = await _db.UserAccounts
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
            throw new Exception("User not found");

        user.FirstName = input.FirstName;
        user.LastName = input.LastName;

        await _db.SaveChangesAsync();
    }
}
