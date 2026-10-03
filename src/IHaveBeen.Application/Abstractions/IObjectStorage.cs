namespace IHaveBeen.Application.Abstractions;

public interface IObjectStorage
{
    Task PutAsync(string key, Stream stream, string contentType, CancellationToken cancellationToken);
    // Returned streams own their provider response and must be disposed by the caller.
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken, long? start = null, long? end = null);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
