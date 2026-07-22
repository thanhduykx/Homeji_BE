using FluentValidation;
using Homeji.Application.DTOs.MarketplaceOrders;
using Homeji.Domain.Entities;

namespace Homeji.Application.Services.MarketplaceOrders.Validation;

public sealed class CreateMarketplaceOrderDtoValidator : AbstractValidator<CreateMarketplaceOrderDto>
{
    public CreateMarketplaceOrderDtoValidator(TimeProvider timeProvider)
    {
        RuleFor(request => request.PickupAt)
            .GreaterThan(_ => timeProvider.GetUtcNow())
            .WithMessage("Thời gian nhận hàng phải ở tương lai.");
        RuleFor(request => request.PickupAddress)
            .NotEmpty()
            .MaximumLength(MarketplaceOrder.MaxPickupAddressLength);
        RuleFor(request => request.Note)
            .MaximumLength(MarketplaceOrder.MaxNoteLength);
        RuleFor(request => request.Quantity)
            .InclusiveBetween(1, MarketplacePost.MaxFoodStock);
    }
}
