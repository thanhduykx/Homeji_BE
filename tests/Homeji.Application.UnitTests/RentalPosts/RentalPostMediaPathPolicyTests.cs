using Homeji.Application.Services.RentalPosts;

namespace Homeji.Application.UnitTests.RentalPosts;

public sealed class RentalPostMediaPathPolicyTests
{
    private readonly Guid _ownerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _postId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void IsOwnedPath_AcceptsHomejiCloudinaryUrlInExpectedFolder()
    {
        var path = $"https://res.cloudinary.com/homeji/image/upload/v123/rental-posts/{_ownerId:D}/{_postId:D}/room.jpg";

        Assert.True(RentalPostMediaPathPolicy.IsOwnedPath(path, _ownerId, _postId));
    }

    [Theory]
    [InlineData("http://res.cloudinary.com/homeji/image/upload/rental-posts/11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222/room.jpg")]
    [InlineData("https://example.com/rental-posts/11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222/room.jpg")]
    [InlineData("https://res.cloudinary.com/homeji/image/upload/rental-posts/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/22222222-2222-2222-2222-222222222222/room.jpg")]
    public void IsOwnedPath_RejectsUntrustedOrWrongOwnerUrl(string path)
    {
        Assert.False(RentalPostMediaPathPolicy.IsOwnedPath(path, _ownerId, _postId));
    }
}
