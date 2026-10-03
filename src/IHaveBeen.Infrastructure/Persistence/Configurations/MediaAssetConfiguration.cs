using IHaveBeen.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IHaveBeen.Infrastructure.Persistence.Configurations;

internal sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> entity)
    {
        entity.HasOne(x => x.TravelLog).WithMany(x => x.Media).HasForeignKey(x => x.TravelLogId).OnDelete(DeleteBehavior.Cascade);
        entity.Property(x => x.ObjectKey).HasMaxLength(300);
        entity.Property(x => x.OriginalName).HasMaxLength(200);
        entity.Property(x => x.ContentType).HasMaxLength(80);
        entity.Property(x => x.Caption).HasMaxLength(500);
        entity.Property(x => x.Sha256).HasMaxLength(64);
        entity.HasIndex(x => x.ObjectKey).IsUnique();
    }
}
