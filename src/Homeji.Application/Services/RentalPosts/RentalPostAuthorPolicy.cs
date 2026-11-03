using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.RentalPosts;

/// <summary>Shared by the form and Agent so the authoring path cannot change permissions.</summary>
public static class RentalPostAuthorPolicy
{
    public static void EnsureCanAuthor(UserProfile author, RentalPostType type)
    {
        if (type == RentalPostType.RoomTransfer) UserContext.EnsureRenter(author);
        else if (type == RentalPostType.RoommateShare && author.Role == UserRole.Renter) return;
        else UserContext.EnsureLandlord(author);
    }
}
