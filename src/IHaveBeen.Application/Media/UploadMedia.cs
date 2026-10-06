using System.Security.Cryptography;
using IHaveBeen.Application.Observability;
using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Common;
using IHaveBeen.Application.TravelLogs;
using IHaveBeen.Domain.Media;
using IHaveBeen.Domain.Accounts;

namespace IHaveBeen.Application.Media;

public sealed record UploadMedia(Guid LogId, Stream Original, string OriginalName, string ContentType, long Length, string Caption);
public sealed class UploadMediaHandler(ITravelLogRepository logs, IMediaRepository media, IObjectStorage storage,
    IObjectDeletionQueue deletions, ITemporaryFileFactory temporaryFiles, IUnitOfWork unitOfWork,
    ICurrentUser user, TimeProvider clock, MediaLimits limits, IAccountMediaPolicy accounts, IMalwareScanner scanner)
{
    private static Result<MediaDto> Observed(UploadOutcome outcome, Result<MediaDto> result)
    { ApplicationTelemetry.Upload(outcome); return result; }

    public async Task<Result<MediaDto>> HandleAsync(UploadMedia command, CancellationToken cancellationToken)
    {
        var owner = user.RequireUserId();
        var log = await logs.GetOwnedAsync(command.LogId, owner, cancellationToken);
        if (log is null) return Observed(UploadOutcome.NotFound, Result<MediaDto>.NotFound());
        var account = await accounts.GetAsync(owner, cancellationToken);
        if (account is null) return Observed(UploadOutcome.Forbidden, Result<MediaDto>.Forbidden());
        if (account.UserType != UserType.Manager && account.Count >= MediaLimits.UserMediaCount)
            return Observed(UploadOutcome.Quota, Result<MediaDto>.Conflict("Your account can store up to 100 photos/videos. Remove a file before uploading another."));
        var maxBytes = limits.ForFile(account.UserType, command.ContentType);
        if (command.Length <= 0) return Observed(UploadOutcome.Invalid, Result<MediaDto>.Invalid("Empty files cannot be uploaded."));
        if (command.Length > maxBytes)
            return Observed(UploadOutcome.Size, Result<MediaDto>.TooLarge($"This file exceeds the {maxBytes / (1024 * 1024)} MiB limit."));
        if (command.Caption.Length > 500) return Observed(UploadOutcome.Invalid, Result<MediaDto>.Invalid("Captions cannot exceed 500 characters."));
        var header = new byte[64];
        var count = await command.Original.ReadAtLeastAsync(header, 12, throwOnEndOfStream: false, cancellationToken);
        var type = DetectMediaType.FromHeader(header.AsSpan(0, count));
        if (type is null || !string.Equals(type, command.ContentType, StringComparison.OrdinalIgnoreCase))
            return Observed(UploadOutcome.Invalid, Result<MediaDto>.Invalid("Use JPEG, PNG, WebP, MP4, MOV or WebM with a matching file type."));
        var name = Path.GetFileName(command.OriginalName.Replace('\\', '/'));
        name = new string(name.Where(ch => !char.IsControl(ch)).Take(200).ToArray());
        if (string.IsNullOrEmpty(name)) name = "travel-media";
        await using var buffer = temporaryFiles.Create();
        await buffer.WriteAsync(header.AsMemory(0, count), cancellationToken);
        // Never trust declared lengths; stop reading as soon as the actual byte ceiling is exceeded.
        var chunk = new byte[65536];
        while (true)
        {
            var read = await command.Original.ReadAsync(chunk, cancellationToken);
            if (read == 0) break;
            if (buffer.Length + read > maxBytes) return Observed(UploadOutcome.Size, Result<MediaDto>.TooLarge("This file exceeds your per-file upload limit."));
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        if (buffer.Length != command.Length) return Observed(UploadOutcome.Invalid, Result<MediaDto>.Invalid("The file length did not match its upload metadata."));
        buffer.Position = 0;
        var verdict = await scanner.ScanAsync(buffer, cancellationToken);
        if (verdict == MalwareScanResult.Rejected)
            return Observed(UploadOutcome.Malware, Result<MediaDto>.Invalid("This file failed the security scan and was not stored."));
        if (verdict is not (MalwareScanResult.Clean or MalwareScanResult.Skipped))
            return Observed(UploadOutcome.ScannerUnavailable, Result<MediaDto>.Unavailable("File scanning is temporarily unavailable. No file was stored; please try again later."));
        buffer.Position = 0;
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(buffer, cancellationToken));
        var id = Guid.NewGuid();
        var key = $"users/{owner}/logs/{command.LogId}/{id}";
        var pending = deletions.Enqueue(key, clock.GetUtcNow(), clock.GetUtcNow().AddHours(1));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        buffer.Position = 0;
        await storage.PutAsync(key, buffer, type, cancellationToken);
        var asset = MediaAsset.Create(id, command.LogId, key, name, type, buffer.Length, hash, command.Caption,
            await media.CountForLogAsync(command.LogId, cancellationToken), clock.GetUtcNow());
        log.AttachMedia(asset);
        media.Add(asset);
        await deletions.RemoveAsync(pending, cancellationToken);
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (MediaQuotaExceededException)
        {
            // PostgreSQL rejected a concurrent 101st upload. Its earlier durable
            // deletion record survives the failed transaction and removes the orphan.
            return Observed(UploadOutcome.Quota, Result<MediaDto>.Conflict("Your account can store up to 100 photos/videos. Remove a file before uploading another."));
        }
        return Observed(UploadOutcome.Accepted, Result<MediaDto>.Success(MediaDto.From(asset)));
    }
}
