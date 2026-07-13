namespace Homeji.Infrastructure.External;

public sealed class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; set; } = string.Empty;

    public string UploadPreset { get; set; } = string.Empty;

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB
}
