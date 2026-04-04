using Microsoft.EntityFrameworkCore;

using Qlt.Infrastructure.Identity.Infrastructure;
using Qlt.Infrastructure.Identity.Models;

namespace Qlt.Infrastructure.Identity.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly IdentityContext _context;

    public TenantRepository(IdentityContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Tenant> CreateAsync(Tenant tenant)
    {
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();
        return tenant;
    }

    public async Task<Tenant?> GetByIdAsync(Guid id)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Tenant?> GetByCompanyIdAsync(Guid companyId)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.CompanyId == companyId);
    }
}
