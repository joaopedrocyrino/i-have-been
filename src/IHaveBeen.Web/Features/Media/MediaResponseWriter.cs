using System.Net.Http.Headers;
using IHaveBeen.Application.Media;

namespace IHaveBeen.Web.Features.Media;

internal static class MediaResponseWriter
{
    public static async Task<IResult> WriteAsync(MediaDownload media, HttpContext context, MediaContentReader reader, CancellationToken ct)
    {
        long start = 0, length = media.ByteLength;
        if (context.Request.Headers.TryGetValue("Range", out var header))
        {
            if (!RangeHeaderValue.TryParse(header, out var range) || range.Unit != "bytes" || range.Ranges.Count != 1) return InvalidRange();
            var part = range.Ranges.Single();
            start = part.From ?? Math.Max(0, media.ByteLength - (part.To ?? 0));
            var end = part.From is null ? media.ByteLength - 1 : Math.Min(part.To ?? media.ByteLength - 1, media.ByteLength - 1);
            if (start < 0 || start >= media.ByteLength || end < start) return InvalidRange();
            length = end - start + 1;
            context.Response.StatusCode = 206;
            context.Response.Headers.ContentRange = $"bytes {start}-{end}/{media.ByteLength}";
        }
        var stream = await reader.OpenAsync(media, ct, context.Response.StatusCode == 206 ? start : null, context.Response.StatusCode == 206 ? start + length - 1 : null);
        context.Response.Headers.AcceptRanges = "bytes";
        context.Response.ContentLength = length;
        return Results.Stream(async output =>
        {
            await using (stream)
            {
                var buffer = new byte[65536]; long remaining = length;
                while (remaining > 0)
                {
                    var count = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), ct);
                    if (count == 0) throw new IOException("Object stream ended before the expected content length.");
                    await output.WriteAsync(buffer.AsMemory(0, count), ct);
                    remaining -= count;
                }
            }
        }, media.ContentType, fileDownloadName: context.Request.Query.ContainsKey("download") ? media.OriginalName : null);

        IResult InvalidRange() { context.Response.Headers.ContentRange = $"bytes */{media.ByteLength}"; return Results.StatusCode(416); }
    }
}
