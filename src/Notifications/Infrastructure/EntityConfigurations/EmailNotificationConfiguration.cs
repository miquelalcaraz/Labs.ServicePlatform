using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using QltS.Notifications.Models;


namespace QltS.Notifications.Infrastructure.EntityConfigurations;

internal sealed class EmailNotificationConfiguration : IEntityTypeConfiguration<EmailNotification>
{
    private const string TableName = "EmailNotification";
    public void Configure(EntityTypeBuilder<EmailNotification> entity)
    {
        entity.ToTable(TableName);

        entity.HasKey(e => e.Id);


        entity.Property(x => x.Id)
            .ValueGeneratedNever();


    }

}