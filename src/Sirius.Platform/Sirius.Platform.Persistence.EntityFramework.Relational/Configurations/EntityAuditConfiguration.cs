using Microsoft.EntityFrameworkCore;

using Sirius.Domain.SeedWork;

namespace Sirius.Platform.Persistence.Configurations;

internal static class EntityAuditConfiguration
{
    public static void ConfigureEntityAuditProperties(this ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(Entity).IsAssignableFrom(entityType.ClrType))
                continue;

            if (entityType.IsOwned())
                continue;

            builder.Entity(entityType.ClrType, entity =>
            {
                entity.Property(nameof(Entity.RowVersion))
                    .IsRowVersion();

                entity.Property(nameof(Entity.CreatedAt))
                    .IsRequired();
            });
        }
    }
}
