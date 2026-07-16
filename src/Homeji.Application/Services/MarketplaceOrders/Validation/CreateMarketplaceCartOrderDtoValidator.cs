using FluentValidation;
using Homeji.Application.DTOs.MarketplaceOrders;
using Homeji.Domain.Entities;

namespace Homeji.Application.Services.MarketplaceOrders.Validation;

public sealed class CreateMarketplaceCartOrderDtoValidator : AbstractValidator<CreateMarketplaceCartOrderDto>
{
    public const int MaxCartItems = 20;

    public CreateMarketplaceCartOrderDtoValidator(TimeProvider timeProvider)
    {
        RuleFor(request => request.PickupAt)
            .GreaterThan(_ => timeProvider.GetUtcNow())
            .WithMessage("Pickup time must be in the future.");
        RuleFor(request => request.PickupAddress)
            .NotEmpty()
            .MaximumLength(MarketplaceOrder.MaxPickupAddressLength);
        RuleFor(request => request.Note)
            .MaximumLength(MarketplaceOrder.MaxNoteLength);
        RuleFor(request => request.Items)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(items => items.Count <= MaxCartItems)
            .WithMessage($"A cart can contain at most {MaxCartItems} items.")
            .Must(items => items.Select(item => item.PostId).Distinct().Count() == items.Count)
            .WithMessage("Cart items must be unique.");
        RuleForEach(request => request.Items).ChildRules(item =>
        {
            item.RuleFor(value => value.PostId).NotEmpty();
            item.RuleFor(value => value.Quantity).InclusiveBetween(1, MarketplacePost.MaxFoodStock);
        });
    }
}
