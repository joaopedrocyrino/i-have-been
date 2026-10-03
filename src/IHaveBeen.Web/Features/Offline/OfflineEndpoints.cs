using System.Security.Cryptography;
using System.Text;
using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Accounts;
using Microsoft.Extensions.FileProviders;

namespace IHaveBeen.Web.Features.Offline;

internal static class OfflineEndpoints
{
    // Deliberate allowlist: authenticated HTML, APIs, framework circuits and media never enter CacheStorage.
    private static readonly string[] Assets = [
        "/offline/index.html", "/offline/offline.css", "/offline/app.js", "/offline/render.js",
        "/offline/database.js", "/offline/crypto.js", "/offline/policy.js", "/offline/register.js",
        "/offline/manifest.webmanifest", "/map-settings.json", "/app.css", "/theme.js", "/journal.js",
        "/map.js", "/favicon.svg", "/assets/countries.geo.json", "/vendor/leaflet/leaflet.js",
        "/vendor/leaflet/leaflet.css", "/vendor/leaflet/images/layers.png", "/vendor/leaflet/images/layers-2x.png",
        "/vendor/leaflet/images/marker-icon.png", "/vendor/leaflet/images/marker-icon-2x.png",
        "/vendor/leaflet/images/marker-shadow.png",
        "/Components/Map/MapCanvas.razor.rz.scp.css", "/Components/Map/MapHeader.razor.rz.scp.css",
        "/Components/Layout/ThemeSwitcher.razor.rz.scp.css", "/Components/Journal/JournalShell.razor.rz.scp.css",
        "/Components/Journal/JournalPanel.razor.rz.scp.css", "/Components/Journal/MemoryDetails.razor.rz.scp.css",
        "/Components/Experiences/ExperienceList.razor.rz.scp.css", "/Components/Experiences/StarRating.razor.rz.scp.css"
    ];

    public static IEndpointRouteBuilder MapOfflineEndpoints(this IEndpointRouteBuilder routes, IFileProvider files)
    {
        var revision = new Lazy<string>(() => Revision(files));
        var manifest = new Lazy<object>(() => new { version = revision.Value, assets = Assets });
        routes.MapGet("/offline-assets.json", () => Results.Json(manifest.Value)).AllowAnonymous();
        routes.MapGet("/service-worker.js", () =>
        {
            using var stream = files.GetFileInfo("offline/worker.js").CreateReadStream();
            using var reader = new StreamReader(stream);
            return Results.Text("const REVISION = \"" + revision.Value + "\";\n" + reader.ReadToEnd(), "text/javascript", Encoding.UTF8);
        }).AllowAnonymous();
        routes.MapGet("/api/offline/session", async (ICurrentUser user, GetMyProfileHandler profile, CancellationToken ct) =>
        {
            var result = await profile.HandleAsync(ct);
            return Results.Ok(new { ownerId = user.RequireUserId(), displayName = result?.DisplayName });
        }).RequireAuthorization();
        return routes;
    }

    private static string Revision(IFileProvider files)
    {
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        digest.AppendData(typeof(OfflineEndpoints).Assembly.ManifestModule.ModuleVersionId.ToByteArray());
        foreach (var path in Assets.Append("/offline/worker.js"))
        {
            var file = files.GetFileInfo(path.TrimStart('/'));
            if (!file.Exists) throw new InvalidOperationException($"Missing offline asset: {path}");
            digest.AppendData(Encoding.UTF8.GetBytes(path));
            using var stream = file.CreateReadStream();
            var buffer = new byte[65536]; int count;
            while ((count = stream.Read(buffer)) > 0) digest.AppendData(buffer.AsSpan(0, count));
        }
        return Convert.ToHexStringLower(digest.GetHashAndReset());
    }
}
