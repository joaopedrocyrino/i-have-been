using IHaveBeen.Application.Abstractions;
using IHaveBeen.Infrastructure;
using IHaveBeen.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace IHaveBeen.InfrastructureTests;

public sealed class ScanningConfigurationTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Scanning_is_enabled_by_default_and_only_explicit_false_removes_the_dependency(string? configured, bool enabled)
    {
        var values = new Dictionary<string, string?> { ["ConnectionStrings:Database"] = "Host=localhost;Database=configuration-test" };
        if (configured is not null) values["MalwareScanning:Enabled"] = configured;
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSingleton(TimeProvider.System); services.AddInfrastructure(config);
        using var provider = services.BuildServiceProvider();
        var scanner = provider.GetRequiredService<IMalwareScanner>();
        Assert.Equal(enabled, scanner is not DisabledMalwareScanner);
        Assert.Equal(enabled, provider.GetService<ClamAvMalwareScanner>() is not null);
        var checks = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        Assert.Equal(enabled, checks.Any(check => check.Name == "malware-scanner"));
        Assert.Contains(checks, check => check.Name == "postgres");
        Assert.Contains(checks, check => check.Name == "garage");
    }

    [Fact]
    public async Task Disabled_scanner_reports_skipped_without_reading_original_bytes()
    {
        await using var file = new MemoryStream(new byte[128]);
        Assert.Equal(MalwareScanResult.Skipped, await new DisabledMalwareScanner().ScanAsync(file, TestContext.Current.CancellationToken));
        Assert.Equal(0, file.Position);
    }

    [Fact]
    public async Task Disabled_scanner_still_honors_request_cancellation()
    {
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await using var file = new MemoryStream();
        await Assert.ThrowsAsync<OperationCanceledException>(() => new DisabledMalwareScanner().ScanAsync(file, canceled.Token));
    }
}
