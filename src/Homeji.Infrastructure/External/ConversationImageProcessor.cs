using System.Security.Cryptography;
using Homeji.Application.DTOs.Conversations;
using Homeji.Application.IServices.Upload;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Homeji.Infrastructure.External;

public sealed class ConversationImageProcessor : IConversationImageProcessor
{
    public const int MaxInputBytes = 8 * 1024 * 1024;
    private const int MaxDimension = 4_096;
    private const long MaxPixels = 20_000_000;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };
    private static readonly HashSet<string> AllowedFormatNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "JPEG",
        "PNG",
        "WEBP",
    };

    public async Task<ProcessedConversationImage> ProcessAsync(
        ConversationImageUpload upload,
        CancellationToken cancellationToken = default)
    {
        if (!AllowedContentTypes.Contains(upload.ContentType))
        {
            throw new InvalidOperationException("Only JPEG, PNG, and WebP images are accepted.");
        }

        if (upload.Content.Length == 0 || upload.Content.Length > MaxInputBytes)
        {
            throw new InvalidOperationException("Each image must be between 1 byte and 8 MB.");
        }

        await using var input = new MemoryStream(upload.Content, writable: false);
        var detectedFormat = await Image.DetectFormatAsync(input, cancellationToken);
        if (detectedFormat is null || !AllowedFormatNames.Contains(detectedFormat.Name))
        {
            throw new InvalidOperationException("The file signature is not JPEG, PNG, or WebP.");
        }

        input.Position = 0;
        var info = await Image.IdentifyAsync(input, cancellationToken)
            ?? throw new InvalidOperationException("The uploaded file is not a valid image.");
        if ((long)info.Width * info.Height > MaxPixels)
        {
            throw new InvalidOperationException("The image dimensions are too large.");
        }

        input.Position = 0;
        using var image = await Image.LoadAsync(input, cancellationToken);
        image.Metadata.ExifProfile = null;
        image.Metadata.XmpProfile = null;
        image.Metadata.IptcProfile = null;

        if (image.Width > MaxDimension || image.Height > MaxDimension)
        {
            image.Mutate(context => context.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(MaxDimension, MaxDimension),
            }));
        }

        await using var output = new MemoryStream();
        await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = 86 }, cancellationToken);
        var sanitized = output.ToArray();
        var hash = Convert.ToHexStringLower(SHA256.HashData(sanitized));
        return new ProcessedConversationImage("image/jpeg", sanitized, image.Width, image.Height, hash);
    }
}
