using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Qlt.Infrastructure.Identity.Models;

namespace Qlt.Infrastructure.Identity.Infrastructure.EntityConfigurations
{
    public partial class TenantProviderConfiguration : IEntityTypeConfiguration<TenantProvider>
    {
        public void Configure(EntityTypeBuilder<TenantProvider> entity)
        {

            entity.HasKey(e => new { e.TenantId, e.SchemaProvider });

            entity.Property(e => e.Enable)
                .HasDefaultValue(true);

            entity.HasOne(d => d.Tenant)
                .WithMany(p => p.TenantProviders)
                .HasForeignKey(d => d.TenantId);


        }

    }
}
