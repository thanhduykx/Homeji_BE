using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.Services.RentalPosts;
using Homeji.Application.Services.RentalPosts.Validation;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.RentalPosts;

public sealed class RentalPostMediaValidationTests
{
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PostId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly string OwnedPath = $"rental-posts/{OwnerId:D}/{PostId:D}/room.jpg";

    [Theory]
    [InlineData("homeji-media")]
    [InlineData("cloudinary")]
    public void FrontendMediaPayload_AcceptsSupportedProvider(string bucket)
    {
        var path = bucket == "cloudinary"
            ? $"https://res.cloudinary.com/homeji/image/upload/f_webp/q_auto/v123/{OwnedPath}"
            : OwnedPath;
        Assert.True(RentalPostMediaPathPolicy.IsOwnedPath(path, OwnerId, PostId));
        var result = new AddRentalPostMediaDtoValidator().Validate(
            new AddRentalPostMediaDto(MediaType.Image, bucket, path, true, 0));
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(error => error.ErrorMessage)));
    }

    [Theory]
    [InlineData("other", "https://res.cloudinary.com/homeji/image/upload/image.jpg")]
    [InlineData("cloudinary", "https://example.test/image.jpg")]
    [InlineData("cloudinary", "http://res.cloudinary.com/homeji/image/upload/image.jpg")]
    [InlineData("cloudinary", "rental-posts/room.jpg")]
    public void UnsupportedProviderOrCloudinaryLocation_IsRejected(string bucket, string path)
    {
        var result = new AddRentalPostMediaDtoValidator().Validate(
            new AddRentalPostMediaDto(MediaType.Image, bucket, path, true, 0));
        Assert.False(result.IsValid);
    }
}
