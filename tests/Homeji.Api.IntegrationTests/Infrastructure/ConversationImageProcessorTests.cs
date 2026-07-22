using Homeji.Application.DTOs.Conversations;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.External;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class ConversationImageProcessorTests
{
    [Fact]
    public async Task ProcessAsync_ReencodesJpegAndRemovesExif()
    {
        await using var source = new MemoryStream();
        using (var image = new Image<Rgba32>(12, 8, Color.CornflowerBlue))
        {
            image.Metadata.ExifProfile = new ExifProfile();
            image.Metadata.ExifProfile.SetValue(ExifTag.ImageDescription, "private-location-note");
            await image.SaveAsJpegAsync(source, new JpegEncoder { Quality = 90 });
        }

        var processor = new ConversationImageProcessor();
        var result = await processor.ProcessAsync(new ConversationImageUpload(
            "room.jpg",
            "image/jpeg",
            source.ToArray(),
            MessageAttachmentContext.CurrentRoom));

        Assert.Equal("image/jpeg", result.MimeType);
        Assert.Equal(12, result.Width);
        Assert.Equal(8, result.Height);
        Assert.Equal(64, result.Sha256.Length);
        using var sanitized = Image.Load(result.Content);
        Assert.Null(sanitized.Metadata.ExifProfile);
    }

    [Fact]
    public async Task ProcessAsync_WhenGifPretendsToBeJpeg_RejectsMagicBytes()
    {
        await using var source = new MemoryStream();
        using (var image = new Image<Rgba32>(2, 2, Color.Red))
        {
            await image.SaveAsGifAsync(source);
        }

        var processor = new ConversationImageProcessor();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync(
            new ConversationImageUpload(
                "fake.jpg",
                "image/jpeg",
                source.ToArray(),
                MessageAttachmentContext.Other)));
        Assert.Contains("signature", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
