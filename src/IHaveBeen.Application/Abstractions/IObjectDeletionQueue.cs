namespace IHaveBeen.Application.Abstractions;

public sealed record ObjectDeletion(Guid Id, string ObjectKey);
public interface IObjectDeletionQueue
{
    Guid Enqueue(string key, DateTimeOffset now, DateTimeOffset? notBefore = null);
    Task RemoveAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ObjectDeletion>> GetDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);
}
