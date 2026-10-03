using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Accounts;
using IHaveBeen.Application.Experiences;
using IHaveBeen.Application.Media;
using IHaveBeen.Application.Sharing;
using IHaveBeen.Application.TravelLogs;
using IHaveBeen.Web.Common;

namespace IHaveBeen.Web.Configuration;

internal static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<CreateTravelLogHandler>();
        services.AddScoped<CreateExperienceHandler>();
        services.AddScoped<UpdateExperienceHandler>();
        services.AddScoped<DeleteExperienceHandler>();
        services.AddScoped<UpdateTravelLogHandler>();
        services.AddScoped<DeleteTravelLogHandler>();
        services.AddScoped<ListTravelLogsHandler>();
        services.AddScoped<UploadMediaHandler>();
        services.AddScoped<GetMediaUsageHandler>();
        services.AddScoped<DeleteMediaHandler>();
        services.AddScoped<GetOwnedMediaHandler>();
        services.AddScoped<MediaContentReader>();
        services.AddScoped<CleanupObjectsHandler>();
        services.AddScoped<CreateShareLinkHandler>();
        services.AddScoped<ListShareLinksHandler>();
        services.AddScoped<RevokeShareLinkHandler>();
        services.AddScoped<ExchangeShareTokenHandler>();
        services.AddScoped<AuthorizeShareHandler>();
        services.AddScoped<GetSharedJournalHandler>();
        services.AddScoped<GetSharedMediaHandler>();
        services.AddScoped<LoginAccountHandler>();
        services.AddScoped<RegisterAccountHandler>();
        services.AddScoped<LogoutAccountHandler>();
        services.AddScoped<GetMyProfileHandler>();
        return services;
    }
}
