using System.ComponentModel.DataAnnotations;

namespace IHaveBeen.Web.Configuration;

public sealed class RequestLimitOptions
{
    [Range(1, 100000)] public int IpPerMinute { get; set; } = 600;
    [Range(1, 100000)] public int AccountPerMinute { get; set; } = 300;
    [Range(1, 1000)] public int ConcurrentRequests { get; set; } = 64;
    [Range(1, 10000)] public int ConcurrentConnections { get; set; } = 128;
    [Range(1, 100)] public int ConcurrentConnectionsPerAccount { get; set; } = 5;
    [Range(1, 10000)] public int SensitivePerMinute { get; set; } = 25;
    [Range(1, 10000)] public int UploadsPerMinute { get; set; } = 12;
    [Range(1, 100)] public int ConcurrentUploads { get; set; } = 8;
    [Range(1, 20)] public int ConcurrentUploadsPerAccount { get; set; } = 2;
    [Range(1, 1800)] public int UploadTimeoutSeconds { get; set; } = 300;
}
