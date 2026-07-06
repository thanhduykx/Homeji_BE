namespace Homeji.Domain.Entities;

public sealed class RentalPostAmenity
{
    private RentalPostAmenity()
    {
        Code = null!;
    }

    internal RentalPostAmenity(Guid rentalPostId, string code)
    {
        RentalPostId = rentalPostId;
        Code = code;
    }

    public Guid RentalPostId { get; private set; }

    public string Code { get; private set; }
}
