namespace Homeji.Application.DTOs.Upload;

public sealed record UploadImageResultDto(
    string Url,
    string PublicId,
    int Width,
    int Height,
    long Bytes,
    string Format);
