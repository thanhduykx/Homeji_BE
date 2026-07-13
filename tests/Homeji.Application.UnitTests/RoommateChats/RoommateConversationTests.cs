using Homeji.Domain.Entities;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.RoommateChats;

public sealed class RoommateConversationTests
{
    [Fact]
    public void GetOtherParticipantId_ReturnsCounterpartyForEachParticipant()
    {
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        var conversation = new RoommateConversation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            firstUserId,
            secondUserId,
            DateTimeOffset.UtcNow);

        Assert.Equal(secondUserId, conversation.GetOtherParticipantId(firstUserId));
        Assert.Equal(firstUserId, conversation.GetOtherParticipantId(secondUserId));
    }

    [Fact]
    public void GetOtherParticipantId_ForOutsider_ThrowsDomainException()
    {
        var conversation = new RoommateConversation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => conversation.GetOtherParticipantId(Guid.NewGuid()));
    }

    [Fact]
    public void RoommateMessage_TrimsBody()
    {
        var message = new RoommateMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "  Xin chào  ",
            DateTimeOffset.UtcNow);

        Assert.Equal("Xin chào", message.Body);
    }
}
