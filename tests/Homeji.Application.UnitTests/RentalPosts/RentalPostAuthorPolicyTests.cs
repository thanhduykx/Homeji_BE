using Homeji.Application.Common.Exceptions;
using Homeji.Application.Services.RentalPosts;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.RentalPosts;

public sealed class RentalPostAuthorPolicyTests
{
    [Theory]
    [InlineData(UserRole.Renter, RentalPostType.VacantRoom, false)]
    [InlineData(UserRole.Renter, RentalPostType.RoommateShare, true)]
    [InlineData(UserRole.Renter, RentalPostType.RoomTransfer, true)]
    [InlineData(UserRole.Landlord, RentalPostType.VacantRoom, true)]
    [InlineData(UserRole.Landlord, RentalPostType.RoommateShare, true)]
    [InlineData(UserRole.Landlord, RentalPostType.RoomTransfer, false)]
    [InlineData(UserRole.Admin, RentalPostType.VacantRoom, false)]
    [InlineData(UserRole.Admin, RentalPostType.RoommateShare, false)]
    [InlineData(UserRole.Admin, RentalPostType.RoomTransfer, false)]
    public void Form_and_agent_share_the_existing_authoring_permissions(UserRole role, RentalPostType type, bool allowed)
    {
        var now = DateTimeOffset.UtcNow;
        var author = UserProfile.Create(Guid.NewGuid(), "Người đăng kiểm thử", now);
        author.SetRole(role, now);
        if (allowed) RentalPostAuthorPolicy.EnsureCanAuthor(author, type);
        else Assert.Throws<ForbiddenAccessException>(() => RentalPostAuthorPolicy.EnsureCanAuthor(author, type));
    }
}
