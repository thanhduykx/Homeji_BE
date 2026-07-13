using Homeji.Application.DTOs.Upload;

namespace Homeji.Application.IServices.Upload;

/// <summary>
/// Uploads images to a cloud storage provider and returns the public URL.
/// </summary>
public interface IImageUploadService
{
    /// <summary>
    /// Uploads an image to the cloud storage.
    /// </summary>
    /// <param name="fileName">Original file name including extension.</param>
    /// <param name="contentType">MIME content type (e.g. image/jpeg).</param>
    /// <param name="fileStream">The image file stream.</param>
    /// <param name="folder">Optional folder/path prefix in cloud storage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Upload result with the public URL and metadata.</returns>
    Task<UploadImageResultDto> UploadAsync(
        string fileName,
        string contentType,
        Stream fileStream,
        string? folder,
        CancellationToken cancellationToken);
}
