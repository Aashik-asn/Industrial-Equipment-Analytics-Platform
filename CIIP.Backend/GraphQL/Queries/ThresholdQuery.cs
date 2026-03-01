using CIIP.Backend.Data;
using CIIP.Backend.Entities;
using CIIP.Backend.DTOs;
using Microsoft.EntityFrameworkCore;
using HotChocolate.Authorization;
using System.Security.Claims;

namespace CIIP.Backend.GraphQL.Queries;

[ExtendObjectType(typeof(Query))]
public class ThresholdQuery
{
    [Authorize]
    public async Task<List<ThresholdDto>> Thresholds(
        ClaimsPrincipal user,
        string? machineType,
        [Service] CiipDbContext db)
    {
        var tenantId = Guid.Parse(user.FindFirst("tenantId")!.Value);

        var rows = await db.AlertThresholds
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId || x.TenantId == null)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        AlertThreshold Resolve(string parameter)
        {
            var tMachine = rows.FirstOrDefault(x =>
                x.Parameter == parameter &&
                x.TenantId == tenantId &&
                x.MachineType == machineType);

            if (tMachine != null)
                return tMachine;

            var tGlobal = rows.FirstOrDefault(x =>
                x.Parameter == parameter &&
                x.TenantId == tenantId &&
                x.MachineType == null);

            if (tGlobal != null)
                return tGlobal;

            return rows.First(x =>
                x.Parameter == parameter &&
                x.TenantId == null &&
                x.MachineType == null);
        }

        var parameters = new[]
        {
            "Vibration","Current","RPM_LOW","RPM_HIGH","Temperature",
            "LOAD_LOW","LOAD_HIGH"
        };

        return parameters
            .Select(p => Resolve(p))
            .Select(x => new ThresholdDto
            {
                Parameter = x.Parameter,
                WarningValue = x.WarningValue,
                CriticalValue = x.CriticalValue,
                MachineType = x.MachineType,
                TenantId = x.TenantId
            })
            .ToList();
    }
}
