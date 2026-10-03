using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Observability;
using IHaveBeen.Infrastructure.Storage;

namespace IHaveBeen.Infrastructure.Observability;

internal sealed class ObservedObjectStorage(GarageObjectStorage storage) : IObjectStorage
{
    public async Task PutAsync(string key, Stream stream, string contentType, CancellationToken ct)
    {
        using var operation = ApplicationTelemetry.Dependency(DependencyOperation.GaragePut);
        await storage.PutAsync(key, stream, contentType, ct);
        operation.Complete();
    }
    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct, long? start = null, long? end = null)
    {
        using var operation = ApplicationTelemetry.Dependency(DependencyOperation.GarageOpen);
        var stream = await storage.OpenReadAsync(key, ct, start, end);
        operation.Complete();
        return stream; // Measures response headers; HTTP duration covers streaming the original bytes.
    }
    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        using var operation = ApplicationTelemetry.Dependency(DependencyOperation.GarageDelete);
        await storage.DeleteAsync(key, ct);
        operation.Complete();
    }
}
