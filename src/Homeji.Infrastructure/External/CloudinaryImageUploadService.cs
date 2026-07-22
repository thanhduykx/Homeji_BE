using System.Net.Http.Headers;
using System.Text.Json;
using Homeji.Application.DTOs.Upload;
using Homeji.Application.IServices.Upload;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.External;

/// <summary>
/// Uploads images to Cloudinary using the unsigned upload preset.
/// No API key/secret required – the upload preset must be configured
/// as "unsigned" in the Cloudinary dashboard.
/// </summary>
public sealed class CloudinaryImageUploadService : IImageUploadService
{
    private static readonly Action<ILogger, string, string, Exception?> UploadStarted =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(2001, nameof(UploadStarted)),
            "Uploading image '{FileName}' to Cloudinary folder '{Folder}'.");

    private static readonly Action<ILogger, int, string, Exception?> UploadFailed =
        LoggerMessage.Define<int, string>(
            LogLevel.Error,
            new EventId(2002, nameof(UploadFailed)),
            "Cloudinary upload failed with status {StatusCode}: {Error}.");

    private static readonly Action<ILogger, string, string, Exception?> UploadSucceeded =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(2003, nameof(UploadSucceeded)),
            "Cloudinary upload succeeded: {PublicId} → {Url}.");

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/svg+xml",
        "image/bmp",
    };

    private readonly HttpClient _httpClient;
    private readonly CloudinaryOptions _options;
    private readonly ILogger<CloudinaryImageUploadService> _logger;

    public CloudinaryImageUploadService(
        HttpClient httpClient,
        IOptions<CloudinaryOptions> options,
        ILogger<CloudinaryImageUploadService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<UploadImageResultDto> UploadAsync(
        string fileName,
        string contentType,
        Stream fileStream,
        string? folder,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Tên tệp là bắt buộc.", nameof(fileName));
        }

        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new InvalidOperationException(
                $"Loại tệp '{contentType}' không được phép. Các loại chấp nhận: {string.Join(", ", AllowedContentTypes)}.");
        }

        if (fileStream.Length > _options.MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"Kích thước tệp vượt quá giới hạn {_options.MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        var uploadUrl = $"https://api.cloudinary.com/v1_1/{_options.CloudName}/image/upload";

        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(_options.UploadPreset), "upload_preset");

        if (!string.IsNullOrWhiteSpace(folder))
        {
            form.Add(new StringContent(folder), "folder");
        }

        UploadStarted(_logger, fileName, folder ?? "(root)", null);

        var response = await _httpClient.PostAsync(uploadUrl, form, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            UploadFailed(_logger, (int)response.StatusCode, errorBody, null);

            throw new InvalidOperationException(
                $"Tải ảnh lên Cloudinary thất bại ({(int)response.StatusCode}): {errorBody}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var result = new UploadImageResultDto(
            Url: root.GetProperty("secure_url").GetString()!,
            PublicId: root.GetProperty("public_id").GetString()!,
            Width: root.GetProperty("width").GetInt32(),
            Height: root.GetProperty("height").GetInt32(),
            Bytes: root.GetProperty("bytes").GetInt64(),
            Format: root.GetProperty("format").GetString()!);

        UploadSucceeded(_logger, result.PublicId, result.Url, null);

        return result;
    }
}
