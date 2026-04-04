using Microsoft.EntityFrameworkCore;

using Qlt.Infrastructure.Identity.Infrastructure;
using Qlt.Infrastructure.Identity.Models;

namespace Qlt.Infrastructure.Identity.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IdentityContext _context;

    public UserRepository(IdentityContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ApplicationUser> CreateAsync(ApplicationUser user)
    {
        _context.ApplicationUser.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<ApplicationUser?> GetByIdAsync(string id)
    {
        return await _context.ApplicationUser
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<ApplicationUser?> GetByEmailAsync(string email)
    {
        return await _context.ApplicationUser
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<IEnumerable<ApplicationUser>> GetByTenantIdAsync(Guid tenantId)
    {
        return await _context.ApplicationUser
            .Where(u => u.TenantId == tenantId)
            .ToListAsync();
    }
}
