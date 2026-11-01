using System.Security.Cryptography;
using Homeji.Application.DTOs.Conversations;
using Homeji.Application.IServices.Upload;
using SkiaSharp;

namespace Homeji.Infrastructure.External;

public sealed class ConversationImageProcessor(ImageProcessingBudget? budget = null) : IConversationImageProcessor
{
    public const int MaxInputBytes = 8 * 1024 * 1024;
    private const int MaxDimension = 2_048;
    private const long MaxDecodedPixels = 4_194_304;
    private const long MaxPixels = 20_000_000;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
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

        using var lease = await (budget ?? ImageProcessingBudget.Shared).EnterAsync(cancellationToken);
        using var data = SKData.CreateCopy(upload.Content);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
        {
            throw new InvalidOperationException("The file signature is not JPEG, PNG, or WebP.");
        }

        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > MaxPixels)
        {
            throw new InvalidOperationException("The image dimensions are too large.");
        }

        // Ask the codec to downsample before allocating pixels; resizing after full decode is too late.
        var desiredScale = Math.Min(1f, (float)MaxDimension / Math.Max(info.Width, info.Height));
        var decodedSize = codec.GetScaledDimensions(desiredScale);
        if (decodedSize.Width <= 0 || decodedSize.Height <= 0
            || (long)decodedSize.Width * decodedSize.Height > MaxDecodedPixels)
        {
            throw new InvalidOperationException("Ảnh này chưa hỗ trợ giải mã trong giới hạn bộ nhớ. Hãy thu nhỏ cạnh dài xuống tối đa 2048 pixel rồi thử lại.");
        }
        using var decoded = new SKBitmap(new SKImageInfo(decodedSize.Width, decodedSize.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
        {
            throw new InvalidOperationException("The uploaded file is not a valid image.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        var scale = Math.Min(1d, (double)MaxDimension / Math.Max(decodedSize.Width, decodedSize.Height));
        var width = Math.Max(1, (int)Math.Round(decodedSize.Width * scale));
        var height = Math.Max(1, (int)Math.Round(decodedSize.Height * scale));
        // Encode a fresh pixel surface: no input EXIF/XMP/IPTC is copied.
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque))
            ?? throw new InvalidOperationException("The image could not be processed.");
        surface.Canvas.Clear(SKColors.White);
        using var image = SKImage.FromBitmap(decoded);
        surface.Canvas.DrawImage(image, new SKRect(0, 0, width, height), new SKSamplingOptions(SKFilterMode.Linear));
        using var snapshot = surface.Snapshot();
        using var output = snapshot.Encode(SKEncodedImageFormat.Jpeg, 86)
            ?? throw new InvalidOperationException("The image could not be encoded.");
        cancellationToken.ThrowIfCancellationRequested();
        var sanitized = output.ToArray();
        var hash = Convert.ToHexStringLower(SHA256.HashData(sanitized));
        return new ProcessedConversationImage("image/jpeg", sanitized, width, height, hash);
    }
}
