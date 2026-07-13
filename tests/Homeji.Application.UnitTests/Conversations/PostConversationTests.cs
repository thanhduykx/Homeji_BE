using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.Conversations;

public sealed class PostConversationTests
{
    [Fact]
    public void GetOtherParticipantId_ReturnsCounterparty()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var conversation = new PostConversation(
            ConversationSubjectType.RentalPost, Guid.NewGuid(), first, second, DateTimeOffset.UtcNow);

        Assert.Equal(second, conversation.GetOtherParticipantId(first));
        Assert.Equal(first, conversation.GetOtherParticipantId(second));
    }

    [Fact]
    public void Constructor_WithSameParticipant_ThrowsDomainException()
    {
        var participant = Guid.NewGuid();

        Assert.Throws<DomainException>(() => new PostConversation(
            ConversationSubjectType.MarketplacePost,
            Guid.NewGuid(),
            participant,
            participant,
            DateTimeOffset.UtcNow));
    }
}
