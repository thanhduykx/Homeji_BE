using System.Text;
using Homeji.Application.DTOs.Conversations;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.External;
using SkiaSharp;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class ConversationImageProcessorTests
{
    [Fact]
    public async Task ProcessAsync_ReencodesJpegAndRemovesExif()
    {
        var jpeg = Encode(12, 8, SKEncodedImageFormat.Jpeg);
        // A TIFF ImageDescription entry embedded in a valid JPEG APP1 EXIF segment.
        var text = Encoding.ASCII.GetBytes("private-location-note\0");
        using var metadata = new MemoryStream();
        using (var writer = new BinaryWriter(metadata, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("Exif\0\0II"));
            writer.Write((ushort)42); writer.Write(8); writer.Write((ushort)1);
            writer.Write((ushort)0x010E); writer.Write((ushort)2); writer.Write(text.Length);
            writer.Write(26); writer.Write(0); writer.Write(text);
        }
        var payload = metadata.ToArray();
        var input = new byte[jpeg.Length + payload.Length + 4];
        jpeg.AsSpan(0, 2).CopyTo(input);
        input[2] = 0xFF; input[3] = 0xE1;
        input[4] = (byte)((payload.Length + 2) >> 8); input[5] = (byte)(payload.Length + 2);
        payload.CopyTo(input, 6); jpeg.AsSpan(2).CopyTo(input.AsSpan(6 + payload.Length));
        var result = await new ConversationImageProcessor().ProcessAsync(Upload(input, "image/jpeg"));
        Assert.Equal("image/jpeg", result.MimeType);
        Assert.Equal(12, result.Width); Assert.Equal(8, result.Height);
        Assert.Equal(64, result.Sha256.Length);
        Assert.False(result.Content.AsSpan().IndexOf(Encoding.ASCII.GetBytes("Exif\0\0")) >= 0);
        Assert.False(result.Content.AsSpan().IndexOf(text) >= 0);
        using var sanitized = SKBitmap.Decode(result.Content);
        Assert.NotNull(sanitized);
        Assert.Equal(12, sanitized.Width);
    }

    [Fact]
    public async Task ProcessAsync_WhenGifPretendsToBeJpeg_RejectsMagicBytes()
    {
        var gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => new ConversationImageProcessor().ProcessAsync(Upload(gif, "image/jpeg")));
        Assert.Contains("signature", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Png, "image/png")]
    [InlineData(SKEncodedImageFormat.Webp, "image/webp")]
    public async Task ProcessAsync_AcceptedFormatsProduceJpeg(SKEncodedImageFormat format, string contentType)
    {
        var result = await new ConversationImageProcessor().ProcessAsync(Upload(Encode(16, 8, format), contentType));
        using var data = SKData.CreateCopy(result.Content);
        using var codec = SKCodec.Create(data);
        Assert.Equal(SKEncodedImageFormat.Jpeg, codec.EncodedFormat);
        Assert.Equal(16, result.Width); Assert.Equal(8, result.Height);
    }

    [Fact]
    public async Task ProcessAsync_ResizesLongEdgeWithoutUpscaling()
    {
        var result = await new ConversationImageProcessor().ProcessAsync(Upload(Encode(5000, 10, SKEncodedImageFormat.Png), "image/png"));
        Assert.Equal(4096, result.Width); Assert.Equal(8, result.Height);
    }

    [Fact]
    public async Task ProcessAsync_RejectsInvalidAndOversizedInputAndHonorsCancellation()
    {
        var processor = new ConversationImageProcessor();
        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync(Upload([1, 2, 3], "image/png")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync(Upload(new byte[ConversationImageProcessor.MaxInputBytes + 1], "image/png")));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.ProcessAsync(Upload(Encode(2, 2, SKEncodedImageFormat.Png), "image/png"), cancellation.Token));
    }

    private static ConversationImageUpload Upload(byte[] content, string contentType) => new("room", contentType, content, MessageAttachmentContext.CurrentRoom);

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height); bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 90);
        return encoded.ToArray();
    }
}
