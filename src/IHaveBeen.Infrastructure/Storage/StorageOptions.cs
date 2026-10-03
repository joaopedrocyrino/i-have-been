using System.ComponentModel.DataAnnotations;

namespace IHaveBeen.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    [Required] public string Endpoint { get; set; } = "";
    [Required] public string Bucket { get; set; } = "";
    [Required] public string AccessKey { get; set; } = "";
    [Required] public string SecretKey { get; set; } = "";
    [Range(1, 104857600)] public long MaxUploadBytes { get; set; } = 104857600;
    [Range(1, 104857600)] public long UserPhotoMaxBytes { get; set; } = 20971520;
    [Range(1, 104857600)] public long UserVideoMaxBytes { get; set; } = 104857600;
}
