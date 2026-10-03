namespace IHaveBeen.Infrastructure.Persistence.Entities;

public sealed class PendingObjectDeletion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ObjectKey { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset NotBefore { get; set; }
}
