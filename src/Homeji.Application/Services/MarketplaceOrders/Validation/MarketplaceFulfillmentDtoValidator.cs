using FluentValidation;
using Homeji.Application.Common;
using Homeji.Application.DTOs.MarketplaceOrders;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.MarketplaceOrders.Validation;

public sealed class MarketplaceFulfillmentDtoValidator : AbstractValidator<MarketplaceFulfillmentDto>
{
    public MarketplaceFulfillmentDtoValidator()
    {
        RuleFor(request => request.Mode).IsInEnum();
        When(request => request.Mode == MarketplaceFulfillmentType.Pickup,
            () => RuleFor(request => request.Delivery).Null());
        When(request => request.Mode == MarketplaceFulfillmentType.SellerDelivery, () =>
        {
            RuleFor(request => request.Delivery).NotNull();
            When(request => request.Delivery is not null, () =>
            {
                RuleFor(request => request.Delivery!.RecipientName).NotEmpty().MaximumLength(100);
                RuleFor(request => request.Delivery!.RecipientPhone).NotEmpty().Matches(@"^\+?[0-9]{9,15}$");
                RuleFor(request => request.Delivery!.Address).NotEmpty().MaximumLength(500);
                RuleFor(request => request.Delivery!).Must(delivery => HomejiServiceArea.Contains(delivery.Latitude, delivery.Longitude))
                    .WithMessage("Địa chỉ giao hàng phải nằm trong khu vực Thủ Đức và Quận 9 cũ.");
            });
        });
    }
}
