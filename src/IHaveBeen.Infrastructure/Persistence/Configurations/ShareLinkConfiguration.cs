using IHaveBeen.Domain.Sharing;
using IHaveBeen.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IHaveBeen.Infrastructure.Persistence.Configurations;

internal sealed class ShareLinkConfiguration : IEntityTypeConfiguration<ShareLink>
{
    public void Configure(EntityTypeBuilder<ShareLink> entity)
    {
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        entity.Property(x => x.Label).HasMaxLength(80);
        entity.Property(x => x.TokenHash).HasMaxLength(64);
        entity.HasIndex(x => new { x.OwnerId, x.CreatedAt });
        entity.HasIndex(x => x.TokenHash).IsUnique();
    }
}
