using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.RentalPosts;

public sealed class RentalPostLifecycleTests
{
    [Fact]
    public void MarkRented_WhenNotActive_ThrowsDomainException()
    {
        var post = RentalPost.CreateDraft(Guid.NewGuid(), RentalPostType.VacantRoom, DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => post.MarkRented(DateTimeOffset.UtcNow.AddMinutes(1)));
        Assert.Equal(RentalPostStatus.Draft, post.Status);
    }
}
