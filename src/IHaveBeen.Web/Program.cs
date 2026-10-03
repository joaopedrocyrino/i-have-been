using IHaveBeen.Web.Observability;
using IHaveBeen.Infrastructure;
using IHaveBeen.Web.Components;
using IHaveBeen.Web.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.AddPresentation();
if (args.Contains("--migrate")) builder.Configuration["Telemetry:Migration"] = "true";
builder.AddObservability();

var app = builder.Build();
if (args.Contains("--migrate"))
{
    await app.Services.MigrateDatabaseAsync();
    return;
}

app.UsePresentation();
app.MapFeatures();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode(options => options.DisableWebSocketCompression = true);
app.Run();

public partial class Program;
