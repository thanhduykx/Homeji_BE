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

    [Fact]
    public void RoomTransfer_SubmitAndApprove_VerifiesOwnerConsent()
    {
        var now = new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero);
        var post = CreateCompleteTransfer(now, ownerConsentConfirmed: true);

        post.Submit(now.AddMinutes(1));
        var reviewerId = Guid.NewGuid();
        post.Approve(now.AddMinutes(2), reviewerId, "Đã gọi chủ nhà xác nhận số điện thoại và thời hạn hợp đồng.");

        Assert.Equal(RentalPostStatus.Active, post.Status);
        Assert.Equal(now.AddMinutes(2), post.OwnerConsentVerifiedAt);
        Assert.Equal(reviewerId, post.OwnerConsentVerifiedBy);
        Assert.NotNull(post.OwnerConsentVerificationNote);
        Assert.Equal(RoomTransferKind.LeaseAssignment, post.TransferKind);
    }

    [Fact]
    public void RoomTransfer_ApprovalWithoutVerificationAudit_IsRejected()
    {
        var now = new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero);
        var post = CreateCompleteTransfer(now, ownerConsentConfirmed: true);
        post.Submit(now.AddMinutes(1));

        Assert.Throws<DomainException>(() => post.Approve(now.AddMinutes(2)));
        Assert.Equal(RentalPostStatus.Pending, post.Status);
    }

    [Fact]
    public void RoomTransfer_WithoutOwnerConsent_CannotBeSubmitted()
    {
        var now = new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero);
        var post = CreateCompleteTransfer(now, ownerConsentConfirmed: false);

        var exception = Assert.Throws<DomainException>(() => post.Submit(now.AddMinutes(1)));

        Assert.Contains("owner consent", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RentalPostStatus.Draft, post.Status);
    }

    private static RentalPost CreateCompleteTransfer(DateTimeOffset now, bool ownerConsentConfirmed)
    {
        var post = RentalPost.CreateDraft(Guid.NewGuid(), RentalPostType.RoomTransfer, now);
        post.UpdateDetails(
            RentalPostType.RoomTransfer,
            "Pass phòng gần trường",
            "Phòng còn hợp đồng và cần chuyển cho sinh viên khác.",
            3_000_000,
            3_000_000,
            24,
            "Linh Trung, Thủ Đức",
            10.85m,
            106.77m,
            [],
            now,
            availableFrom: new DateOnly(2026, 8, 1),
            transferKind: RoomTransferKind.LeaseAssignment,
            originalLeaseEndsOn: new DateOnly(2027, 2, 1),
            passFee: 0,
            transferReason: "Chuyển nơi học.",
            ownerConsentConfirmed: ownerConsentConfirmed,
            ownerConsentContact: "owner@example.com");
        for (var index = 0; index < RentalPost.MinimumImageCountForSubmit; index++)
        {
            post.AddMedia(MediaType.Image, "rental", $"image-{index}.jpg", index == 0, index, now);
        }

        return post;
    }
}
