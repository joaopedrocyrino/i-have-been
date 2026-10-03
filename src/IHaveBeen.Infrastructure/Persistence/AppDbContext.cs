using IHaveBeen.Domain.Experiences;
using IHaveBeen.Domain.Media;
using IHaveBeen.Domain.Sharing;
using IHaveBeen.Domain.TravelLogs;
using IHaveBeen.Infrastructure.Identity;
using IHaveBeen.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IHaveBeen.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<TravelLog> TravelLogs => Set<TravelLog>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();
    public DbSet<PendingObjectDeletion> PendingObjectDeletions => Set<PendingObjectDeletion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
