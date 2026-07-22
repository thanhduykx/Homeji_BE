namespace Homeji.Application.Services.RentalPosts;

public static class RentalPostMediaPathPolicy
{
    private const string CloudinaryHost = "res.cloudinary.com";

    public static bool IsOwnedPath(string path, Guid ownerId, Guid postId)
    {
        var expectedPrefix = $"rental-posts/{ownerId:D}/{postId:D}/";
        if (path.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!Uri.TryCreate(path, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Host, CloudinaryHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var decodedPath = Uri.UnescapeDataString(uri.AbsolutePath);
        return decodedPath.Contains($"/{expectedPrefix}", StringComparison.OrdinalIgnoreCase);
    }
}
