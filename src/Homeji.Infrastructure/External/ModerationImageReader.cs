using System.Net;
using Microsoft.Extensions.Options;
using Homeji.Application.DTOs.Conversations;
using Homeji.Application.IServices.Upload;
using Homeji.Domain.Enums;
using SkiaSharp;

namespace Homeji.Infrastructure.External;

/// <summary>Fetches bounded thumbnails only from this application's managed Cloudinary namespace.</summary>
public sealed class ModerationImageReader(HttpClient http, IOptions<CloudinaryOptions> options, IConversationImageProcessor processor)
{
    public const int MaxImageBytes = 512 * 1024;

    public Uri? ThumbnailUri(string value)
    {
        if (string.IsNullOrWhiteSpace(options.Value.CloudName)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != "https" || uri.Host != "res.cloudinary.com" || !uri.IsDefaultPort
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0) return null;
        var prefix = "/" + options.Value.CloudName + "/image/upload/";
        if (!uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal)) return null;
        return new Uri("https://res.cloudinary.com" + prefix
            + "c_limit,w_1024,h_1024,q_70,f_jpg/" + uri.AbsolutePath[prefix.Length..]);
    }

    public async Task<(string MimeType, byte[] Content)?> ReadAsync(string value, CancellationToken cancellationToken)
    {
        var uri = ThumbnailUri(value);
        if (uri is null) return null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentLength > MaxImageBytes) return null;
            var mime = response.Content.Headers.ContentType?.MediaType;
            if (mime is not ("image/jpeg" or "image/png" or "image/webp")) return null;
            await using var source = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var content = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int read;
            while ((read = await source.ReadAsync(buffer, deadline.Token)) != 0)
            {
                if (content.Length + read > MaxImageBytes) return null;
                content.Write(buffer, 0, read);
            }
            if (content.Length == 0) return null;
            var bytes = content.ToArray();
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);
            if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0
                || (long)codec.Info.Width * codec.Info.Height > 1024 * 1024) return null;
            try
            {
                var image = await processor.ProcessAsync(new ConversationImageUpload("moderation", mime, bytes, MessageAttachmentContext.Other), deadline.Token);
                return image.Content.Length <= MaxImageBytes ? (image.MimeType, image.Content) : null;
            }
            catch (InvalidOperationException) { return null; }
        }
        catch (Exception error) when (error is HttpRequestException || error is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        { return null; }
    }
}
