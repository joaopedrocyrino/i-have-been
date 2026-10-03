using IHaveBeen.Domain.Experiences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IHaveBeen.Infrastructure.Persistence.Configurations;

internal sealed class ExperienceConfiguration : IEntityTypeConfiguration<Experience>
{
    public void Configure(EntityTypeBuilder<Experience> entity)
    {
        entity.HasOne(x => x.TravelLog).WithMany(x => x.Experiences).HasForeignKey(x => x.TravelLogId).OnDelete(DeleteBehavior.Cascade);
        entity.Property(x => x.Title).HasMaxLength(160);
        entity.Property(x => x.Description).HasMaxLength(10000);
        entity.Property(x => x.Address).HasMaxLength(300);
        entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
        entity.HasIndex(x => new { x.TravelLogId, x.VisitedOn });
        entity.ToTable(t => t.HasCheckConstraint("CK_Experience_Rating", "\"Rating\" BETWEEN 0 AND 5"));
    }
}
