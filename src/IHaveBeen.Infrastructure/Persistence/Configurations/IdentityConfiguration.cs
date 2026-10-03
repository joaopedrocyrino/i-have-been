using IHaveBeen.Infrastructure.Identity;
using IHaveBeen.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata;
using IHaveBeen.Domain.Accounts;

namespace IHaveBeen.Infrastructure.Persistence.Configurations;

internal sealed class IdentityConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> entity)
    {
        entity.Property(x => x.DisplayName).HasMaxLength(80);
        entity.HasIndex(x => x.NormalizedEmail).IsUnique();
        var type = entity.Property(x => x.UserType).HasMaxLength(16)
            .HasConversion(v => v.ToString().ToLowerInvariant(), v => Enum.Parse<UserType>(v, true))
            .HasDefaultValue(UserType.User).ValueGeneratedOnAdd();
        // Inserts use the database default. Identity/profile saves never write this column.
        type.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        type.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        entity.ToTable(t => t.HasCheckConstraint("CK_User_Type", "\"UserType\" IN ('user', 'manager')"));
    }
}
internal sealed class ObjectDeletionConfiguration : IEntityTypeConfiguration<PendingObjectDeletion>
{
    public void Configure(EntityTypeBuilder<PendingObjectDeletion> entity) => entity.Property(x => x.ObjectKey).HasMaxLength(300);
}
