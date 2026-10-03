using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Accounts;
using IHaveBeen.Application.Experiences;
using IHaveBeen.Application.Media;
using IHaveBeen.Application.Sharing;
using IHaveBeen.Application.TravelLogs;
using IHaveBeen.Infrastructure.BackgroundJobs;
using IHaveBeen.Infrastructure.Observability;
using IHaveBeen.Infrastructure.Health;
using IHaveBeen.Infrastructure.Identity;
using IHaveBeen.Infrastructure.Persistence;
using IHaveBeen.Infrastructure.Persistence.Repositories;
using IHaveBeen.Infrastructure.Security;
using IHaveBeen.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IHaveBeen.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("Set ConnectionStrings__Database.");
        services.AddSingleton<DatabaseTelemetryInterceptor>();
        services.AddDbContext<AppDbContext>((sp, o) => o.UseNpgsql(connection).AddInterceptors(sp.GetRequiredService<DatabaseTelemetryInterceptor>()));
        services.AddIdentity<ApplicationUser, IdentityRole>(o =>
        {
            o.User.RequireUniqueEmail = true;
            o.Password.RequiredLength = 12;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            o.SignIn.RequireConfirmedAccount = false;
        }).AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.SectionName)).ValidateDataAnnotations()
            .Validate(o => Uri.TryCreate(o.Endpoint, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https", "Storage endpoint must be an HTTP(S) URL.")
            .Validate(o => !o.AccessKey.StartsWith("replace-", StringComparison.Ordinal) && !o.SecretKey.StartsWith("replace-", StringComparison.Ordinal), "Configure real storage credentials.")
            .ValidateOnStart();
        services.AddScoped<ITravelLogRepository, TravelLogRepository>();
        services.AddScoped<IMediaRepository, MediaRepository>();
        services.AddScoped<IAccountMediaPolicy, AccountMediaPolicy>();
        services.AddScoped<IExperienceRepository, ExperienceRepository>();
        services.AddScoped<IShareLinkRepository, ShareLinkRepository>();
        services.AddScoped<IObjectDeletionQueue, ObjectDeletionQueue>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IIdentityGateway, IdentityGateway>();
        services.AddSingleton<IShareTokenService, ShareTokenService>();
        services.AddSingleton<ITemporaryFileFactory, TemporaryFileFactory>();
        services.AddSingleton<GarageObjectStorage>();
        services.AddSingleton<IObjectStorage, ObservedObjectStorage>();
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            return new MediaLimits(options.MaxUploadBytes, options.UserPhotoMaxBytes, options.UserVideoMaxBytes);
        });
        services.AddOptions<MalwareScannerOptions>().Bind(configuration.GetSection(MalwareScannerOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<ClamAvMalwareScanner>();
        services.AddSingleton<IMalwareScanner, ObservedMalwareScanner>();
        services.AddSingleton(new AccountPolicy(configuration.GetValue("Security:AllowRegistration", true)));
        services.AddHostedService<ObjectCleanupWorker>();
        services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres").AddCheck<GarageHealthCheck>("garage")
            .AddCheck<MalwareScannerHealthCheck>("malware-scanner");
        return services;
    }
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(ct);
    }
}
