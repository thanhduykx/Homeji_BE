using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class PostConversation
{
    private PostConversation()
    {
    }

    public PostConversation(
        ConversationSubjectType subjectType,
        Guid subjectId,
        Guid participantAId,
        Guid participantBId,
        DateTimeOffset createdAt)
    {
        if (!Enum.IsDefined(subjectType))
        {
            throw new DomainException("Conversation subject type is invalid.");
        }

        if (participantAId == Guid.Empty || participantBId == Guid.Empty || participantAId == participantBId)
        {
            throw new DomainException("A direct conversation requires two different participants.");
        }

        Id = Guid.NewGuid();
        SubjectType = subjectType;
        SubjectId = subjectId;
        ParticipantAId = participantAId;
        ParticipantBId = participantBId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        ParticipantALastReadAt = createdAt;
        ParticipantBLastReadAt = createdAt;
    }

    public Guid Id { get; private set; }
    public ConversationSubjectType SubjectType { get; private set; }
    public Guid SubjectId { get; private set; }
    public Guid ParticipantAId { get; private set; }
    public Guid ParticipantBId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ParticipantALastReadAt { get; private set; }
    public DateTimeOffset? ParticipantBLastReadAt { get; private set; }

    public bool Includes(Guid userId) => ParticipantAId == userId || ParticipantBId == userId;

    public Guid GetOtherParticipantId(Guid userId)
    {
        return ParticipantAId == userId
            ? ParticipantBId
            : ParticipantBId == userId
                ? ParticipantAId
                : throw new DomainException("User is not a participant in this conversation.");
    }

    public DateTimeOffset? GetLastReadAt(Guid userId)
    {
        if (userId == ParticipantAId) return ParticipantALastReadAt;
        if (userId == ParticipantBId) return ParticipantBLastReadAt;
        throw new DomainException("User is not a participant in this conversation.");
    }

    public void MarkRead(Guid userId, DateTimeOffset readAt)
    {
        if (userId == ParticipantAId)
        {
            ParticipantALastReadAt = readAt;
            return;
        }

        if (userId == ParticipantBId)
        {
            ParticipantBLastReadAt = readAt;
            return;
        }

        throw new DomainException("User is not a participant in this conversation.");
    }

    public void Touch(DateTimeOffset updatedAt)
    {
        UpdatedAt = updatedAt;
    }
}
