using System;
using System.Threading;
using System.Threading.Tasks;

namespace Miqa.Domain.SeedWork;

public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
