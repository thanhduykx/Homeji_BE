using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.Verifications;

public sealed class LandlordVerificationTests
{
    [Fact]
    public void Submit_ForRenter_ThrowsDomainException()
    {
        var profile = UserProfile.Create(Guid.NewGuid(), "Renter", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => profile.SubmitLandlordVerification(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Review_Approved_UpdatesRequestStatus()
    {
        var request = new LandlordVerificationRequest(
            Guid.NewGuid(),
            "https://cdn.example.com/document.jpg",
            null,
            DateTimeOffset.UtcNow);

        request.Review(true, Guid.NewGuid(), "Valid", DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(LandlordVerificationStatus.Verified, request.Status);
    }
}
