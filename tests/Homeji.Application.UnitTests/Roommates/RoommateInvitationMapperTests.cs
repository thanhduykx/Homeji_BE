using Homeji.Application.Mappers.Roommates;
using Homeji.Domain.Entities;

namespace Homeji.Application.UnitTests.Roommates;

public sealed class RoommateInvitationMapperTests
{
    [Fact]
    public void ToDto_IncludesListingTitleAndConversationId()
    {
        var invitation = new RoommateInvitation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 23, 0, 0, 0, TimeSpan.Zero));
        var conversationId = Guid.NewGuid();

        var result = RoommateInvitationMapper.ToDto(
            invitation,
            "Phòng gần Đại học Quốc gia",
            conversationId);

        Assert.Equal("Phòng gần Đại học Quốc gia", result.RentalPostTitle);
        Assert.Equal(conversationId, result.ConversationId);
    }
}
