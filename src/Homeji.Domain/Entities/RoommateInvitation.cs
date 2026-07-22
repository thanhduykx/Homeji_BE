using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class RoommateInvitation
{
    private RoommateInvitation()
    {
    }

    public RoommateInvitation(Guid rentalPostId, Guid senderId, Guid receiverId, DateTimeOffset createdAt)
    {
        if (senderId == receiverId)
        {
            throw new DomainException("Không thể gửi lời mời ở ghép cho chính mình.");
        }

        Id = Guid.NewGuid();
        RentalPostId = rentalPostId;
        SenderId = senderId;
        ReceiverId = receiverId;
        Status = RoommateInvitationStatus.Pending;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid RentalPostId { get; private set; }

    public Guid SenderId { get; private set; }

    public Guid ReceiverId { get; private set; }

    public RoommateInvitationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Accept(DateTimeOffset updatedAt)
    {
        EnsurePending();
        Status = RoommateInvitationStatus.Accepted;
        UpdatedAt = updatedAt;
    }

    public void Reject(DateTimeOffset updatedAt)
    {
        EnsurePending();
        Status = RoommateInvitationStatus.Rejected;
        UpdatedAt = updatedAt;
    }

    public void Cancel(DateTimeOffset updatedAt)
    {
        EnsurePending();
        Status = RoommateInvitationStatus.Cancelled;
        UpdatedAt = updatedAt;
    }

    private void EnsurePending()
    {
        if (Status != RoommateInvitationStatus.Pending)
        {
            throw new DomainException("Chỉ lời mời ở ghép đang chờ mới có thể cập nhật.");
        }
    }
}
