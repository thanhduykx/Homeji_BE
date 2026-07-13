using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class RoommateConversation
{
    private RoommateConversation()
    {
    }

    public RoommateConversation(
        Guid invitationId,
        Guid rentalPostId,
        Guid firstParticipantId,
        Guid secondParticipantId,
        DateTimeOffset createdAt)
    {
        if (invitationId == Guid.Empty || rentalPostId == Guid.Empty)
        {
            throw new DomainException("Invitation id and rental post id must not be empty.");
        }

        if (firstParticipantId == Guid.Empty || secondParticipantId == Guid.Empty)
        {
            throw new DomainException("Conversation participant ids must not be empty.");
        }

        if (firstParticipantId == secondParticipantId)
        {
            throw new DomainException("A roommate conversation requires two different participants.");
        }

        Id = Guid.NewGuid();
        InvitationId = invitationId;
        RentalPostId = rentalPostId;
        FirstParticipantId = firstParticipantId;
        SecondParticipantId = secondParticipantId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid InvitationId { get; private set; }

    public Guid RentalPostId { get; private set; }

    public Guid FirstParticipantId { get; private set; }

    public Guid SecondParticipantId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool Includes(Guid userId)
    {
        return FirstParticipantId == userId || SecondParticipantId == userId;
    }

    public Guid GetOtherParticipantId(Guid userId)
    {
        if (FirstParticipantId == userId)
        {
            return SecondParticipantId;
        }

        if (SecondParticipantId == userId)
        {
            return FirstParticipantId;
        }

        throw new DomainException("User is not a participant in this roommate conversation.");
    }

    public void Touch(DateTimeOffset updatedAt)
    {
        UpdatedAt = updatedAt;
    }
}
