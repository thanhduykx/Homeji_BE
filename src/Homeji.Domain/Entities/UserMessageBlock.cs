namespace Homeji.Domain.Entities;

public sealed class UserMessageBlock
{
    private UserMessageBlock() { }
    public UserMessageBlock(Guid blockerId, Guid blockedId, DateTimeOffset createdAt)
    {
        if (blockerId == Guid.Empty || blockedId == Guid.Empty || blockerId == blockedId)
            throw new Homeji.Domain.Exceptions.DomainException("Không thể chặn chính mình hoặc tài khoản không hợp lệ.");
        BlockerId = blockerId;
        BlockedId = blockedId;
        CreatedAt = createdAt.ToUniversalTime();
    }
    public Guid BlockerId { get; private set; }
    public Guid BlockedId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
