using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using IHaveBeen.Application.Abstractions;
using IHaveBeen.Domain.Accounts;
using IHaveBeen.Infrastructure.Identity;
using IHaveBeen.Infrastructure.Persistence;
using IHaveBeen.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IHaveBeen.InfrastructureTests;

public sealed class UploadSecurityTests
{
    [Fact]
    public void Account_type_uses_database_default_and_cannot_be_written_by_identity_saves()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=model-only").Options);
        var property = db.Model.FindEntityType(typeof(ApplicationUser))!.FindProperty(nameof(ApplicationUser.UserType))!;
        Assert.Equal(UserType.User, property.GetDefaultValue());
        Assert.Equal(PropertySaveBehavior.Ignore, property.GetBeforeSaveBehavior());
        Assert.Equal(PropertySaveBehavior.Ignore, property.GetAfterSaveBehavior());
        Assert.False(typeof(ApplicationUser).GetProperty(nameof(ApplicationUser.UserType))!.SetMethod!.IsPublic);
    }

    [Theory]
    [InlineData("stream: OK", MalwareScanResult.Clean)]
    [InlineData("stream: Eicar-Signature FOUND", MalwareScanResult.Rejected)]
    [InlineData("stream: Heuristics.Limits.Exceeded.MaxFileSize FOUND", MalwareScanResult.Rejected)]
    [InlineData("INSTREAM size limit exceeded. ERROR", MalwareScanResult.Unavailable)]
    [InlineData("unexpected OK", MalwareScanResult.Unavailable)]
    public async Task Scanner_requires_an_explicit_complete_verdict_and_sends_every_byte(string verdict, MalwareScanResult expected)
    {
        var bytes = Enumerable.Range(0, 150000).Select(i => (byte)i).ToArray();
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var ct = TestContext.Current.CancellationToken;
        var server = Task.Run(async () =>
        {
            using (var client = await listener.AcceptTcpClientAsync(ct))
            {
                var socket = client.GetStream(); Assert.Equal("zVERSION", await Command(socket, ct));
                await socket.WriteAsync(Encoding.UTF8.GetBytes($"ClamAV 1.5.4/28143/{DateTimeOffset.UtcNow:ddd MMM d HH:mm:ss yyyy}\0"), ct);
            }
            using (var client = await listener.AcceptTcpClientAsync(ct))
            {
                var socket = client.GetStream(); Assert.Equal("zINSTREAM", await Command(socket, ct));
                using var received = new MemoryStream(); var header = new byte[4];
                while (true)
                {
                    await socket.ReadExactlyAsync(header, ct); var length = BinaryPrimitives.ReadUInt32BigEndian(header);
                    if (length == 0) break;
                    var chunk = new byte[length]; await socket.ReadExactlyAsync(chunk, ct); await received.WriteAsync(chunk, ct);
                }
                Assert.Equal(bytes, received.ToArray()); await socket.WriteAsync(Encoding.UTF8.GetBytes(verdict + "\0"), ct);
            }
        }, ct);
        await using var original = new MemoryStream(bytes);
        Assert.Equal(expected, await Scanner(listener).ScanAsync(original, ct)); await server;
    }

    [Theory]
    [InlineData("ClamAV 1.5.4/28143/Sun Sep 27 12:00:00 2026")]
    [InlineData("COMMAND UNAVAILABLE")]
    public async Task Stale_or_unverifiable_definitions_prevent_file_transmission(string version)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var ct = TestContext.Current.CancellationToken;
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(ct); var socket = client.GetStream();
            Assert.Equal("zVERSION", await Command(socket, ct)); await socket.WriteAsync(Encoding.UTF8.GetBytes(version + "\0"), ct);
        }, ct);
        await using var original = new MemoryStream(new byte[64]);
        Assert.Equal(MalwareScanResult.Unavailable, await Scanner(listener).ScanAsync(original, ct));
        Assert.Equal(0, original.Position); await server;
    }

    [Fact]
    public async Task Offline_scanner_fails_closed()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var scanner = Scanner(listener); listener.Stop();
        await using var original = new MemoryStream(new byte[64]);
        Assert.Equal(MalwareScanResult.Unavailable, await scanner.ScanAsync(original, TestContext.Current.CancellationToken));
    }

    private static ClamAvMalwareScanner Scanner(TcpListener listener) => new(Options.Create(new MalwareScannerOptions
    { Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port, TimeoutSeconds = 5 }), TimeProvider.System, NullLogger<ClamAvMalwareScanner>.Instance);
    private static async Task<string> Command(NetworkStream socket, CancellationToken ct)
    {
        using var buffer = new MemoryStream(); var next = new byte[1];
        while (true) { await socket.ReadExactlyAsync(next, ct); if (next[0] == 0) return Encoding.UTF8.GetString(buffer.ToArray()); buffer.WriteByte(next[0]); }
    }
}
