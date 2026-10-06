using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using IHaveBeen.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace IHaveBeen.Infrastructure.Storage;

internal sealed class GarageObjectStorage : IObjectStorage, IDisposable
{
    private readonly AmazonS3Client client;
    private readonly string bucket;

    public GarageObjectStorage(IOptions<StorageOptions> settings)
    {
        var options = settings.Value;
        bucket = options.Bucket;
        client = new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), new AmazonS3Config
        {
            ServiceURL = options.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "garage",
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        });
    }
    public Task PutAsync(string key, Stream stream, string type, CancellationToken ct) => client.PutObjectAsync(new PutObjectRequest
    {
        BucketName = bucket,
        Key = key,
        InputStream = stream,
        ContentType = type,
        AutoCloseStream = false,
        UseChunkEncoding = false
    }, ct);
    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct, long? start = null, long? end = null)
    {
        var response = await client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = bucket,
            Key = key,
            ByteRange = start is { } s && end is { } e ? new ByteRange(s, e) : null
        }, ct);
        return new GarageReadStream(response);
    }
    public Task DeleteAsync(string key, CancellationToken ct) => client.DeleteObjectAsync(bucket, key, ct);
    // Validate the configured private bucket and application credentials without
    // listing or parsing media objects during deployment readiness.
    public async Task CheckAvailabilityAsync(CancellationToken ct) =>
        await client.HeadBucketAsync(new HeadBucketRequest { BucketName = bucket }, ct);
    public void Dispose() => client.Dispose();

    private sealed class GarageReadStream(GetObjectResponse response) : Stream
    {
        private Stream Inner => response.ResponseStream;
        public override bool CanRead => Inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => Inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => Inner.Read(buffer);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => Inner.ReadAsync(buffer, offset, count, ct);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => Inner.ReadAsync(buffer, ct);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) response.Dispose(); base.Dispose(disposing); }
        public override ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
