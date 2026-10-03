using IHaveBeen.Domain.TravelLogs;
using IHaveBeen.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IHaveBeen.Infrastructure.Persistence.Configurations;

internal sealed class TravelLogConfiguration : IEntityTypeConfiguration<TravelLog>
{
    public void Configure(EntityTypeBuilder<TravelLog> entity)
    {
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        entity.Property(x => x.Title).HasMaxLength(160);
        entity.Property(x => x.Description).HasMaxLength(10000);
        entity.Property(x => x.City).HasMaxLength(120);
        entity.Property(x => x.Country).HasMaxLength(120);
        entity.HasIndex(x => new { x.OwnerId, x.VisitedOn });
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).HasDefaultValue(TravelLogStatus.Visited);
        entity.Navigation(x => x.Experiences).HasField("experiences").UsePropertyAccessMode(PropertyAccessMode.Field);
        entity.Navigation(x => x.Media).HasField("media").UsePropertyAccessMode(PropertyAccessMode.Field);
        entity.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Log_Coordinates", "\"Latitude\" BETWEEN -90 AND 90 AND \"Longitude\" BETWEEN -180 AND 180");
            t.HasCheckConstraint("CK_Log_Status", "\"Status\" IN ('Visited', 'Wishlist') AND (\"Status\" = 'Wishlist' OR (\"VisitedOn\" IS NOT NULL AND length(trim(\"City\")) > 0))");
            t.HasCheckConstraint("CK_Log_Dates", "\"EndedOn\" IS NULL OR \"EndedOn\" >= \"VisitedOn\"");
        });
    }
}
