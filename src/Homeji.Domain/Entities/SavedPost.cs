namespace Homeji.Domain.Entities;

public sealed class SavedPost
{
    private SavedPost()
    {
    }

    public SavedPost(Guid userId, Guid rentalPostId, DateTimeOffset createdAt)
    {
        UserId = userId;
        RentalPostId = rentalPostId;
        CreatedAt = createdAt;
    }

    public Guid UserId { get; private set; }

    public Guid RentalPostId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
