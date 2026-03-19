using MediatR;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Npgsql;

using QltS.Core.Persistence.EF.Extensions;
using QltS.Core.Persistence.EF.Relational.Configurations;

namespace QltS.Core.Persistence.EF.Relational;

/// <summary>
/// Base DbContext for all QltS contexts. 
/// Provides common functionality like domain events dispatching, audit logging, and transaction management.
/// </summary>
public class QltDBContext : DBContextBase
{
    public QltDBContext(DbContextOptions options, IMediator mediator, QltDbContextConfiguration configuration) : base(options, mediator, configuration)
    {
    }



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (_configuration.UseAuditLogs)
        {
            modelBuilder.UseAuditLogs();
        }
    }
    public override bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (IsSqlServeUniqueConstraintViolation(ex))
        {
            return true;
        }
        if (IsPostgresUniqueConstraintViolation(ex))
        {
            return true;
        }
        if (IsGenericUniqueConstraintViolation(ex))
        {
            return true;
        }
        return false;
    }

    private bool IsSqlServeUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sqlEx
            && (sqlEx.Number == 2627 || sqlEx.Number == 2601);
    }

    private bool IsPostgresUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pgEx
            && pgEx.SqlState == "23505"; // unique_violation
    }

    private bool IsGenericUniqueConstraintViolation(DbUpdateException ex)
    {
        // Check the inner exception message for unique constraint violation keywords
        var innerMessage = ex.InnerException?.Message ?? string.Empty;

        return innerMessage.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || innerMessage.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || innerMessage.Contains("constraint", StringComparison.OrdinalIgnoreCase)
            || innerMessage.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase)
            || innerMessage.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase);
    }

}
