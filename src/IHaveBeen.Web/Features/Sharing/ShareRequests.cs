namespace IHaveBeen.Web.Features.Sharing;

public sealed record ShareInput(string Label, DateTimeOffset? ExpiresAt);
public sealed record ExchangeInput(string? Token);
public sealed record ShareView(Guid Id, string Label, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt);
