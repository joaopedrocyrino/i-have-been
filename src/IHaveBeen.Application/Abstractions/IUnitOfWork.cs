namespace IHaveBeen.Application.Abstractions;

public interface IUnitOfWork { Task SaveChangesAsync(CancellationToken cancellationToken); }
