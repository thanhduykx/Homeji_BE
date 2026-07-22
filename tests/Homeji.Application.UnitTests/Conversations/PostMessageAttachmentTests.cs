using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.Conversations;

public sealed class PostMessageAttachmentTests
{
    [Fact]
    public void Delete_ByUploader_RemovesPrivateContent()
    {
        var uploaderId = Guid.NewGuid();
        var message = new PostMessage(Guid.NewGuid(), uploaderId, "Ảnh phòng", DateTimeOffset.UtcNow);
        var attachment = message.AddImage(
            uploaderId,
            MessageAttachmentContext.CurrentRoom,
            "image/jpeg",
            [1, 2, 3],
            1,
            1,
            new string('a', 64),
            DateTimeOffset.UtcNow);

        attachment.Delete(uploaderId, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(MessageAttachmentStatus.Deleted, attachment.Status);
        Assert.Empty(attachment.Content);
        Assert.Equal(0, attachment.Bytes);
    }

    [Fact]
    public void Delete_ByOtherParticipant_IsRejected()
    {
        var uploaderId = Guid.NewGuid();
        var message = new PostMessage(Guid.NewGuid(), uploaderId, "Ảnh phòng", DateTimeOffset.UtcNow);
        var attachment = message.AddImage(
            uploaderId,
            MessageAttachmentContext.CurrentRoom,
            "image/jpeg",
            [1],
            1,
            1,
            new string('b', 64),
            DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => attachment.Delete(Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.Equal(MessageAttachmentStatus.Ready, attachment.Status);
    }
}
