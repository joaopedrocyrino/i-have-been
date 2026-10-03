using System.Text;

namespace IHaveBeen.Application.Media;

public static class DetectMediaType
{
    public static string? FromHeader(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) return "image/jpeg";
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes.Length >= 12 && Encoding.ASCII.GetString(bytes[..4]) == "RIFF" && Encoding.ASCII.GetString(bytes.Slice(8, 4)) == "WEBP") return "image/webp";
        if (bytes.Length >= 12 && Encoding.ASCII.GetString(bytes.Slice(4, 4)) == "ftyp") return Encoding.ASCII.GetString(bytes.Slice(8, 4)) == "qt  " ? "video/quicktime" : "video/mp4";
        if (bytes.Length >= 4 && bytes[..4].SequenceEqual(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 })) return "video/webm";
        return null;
    }
}
