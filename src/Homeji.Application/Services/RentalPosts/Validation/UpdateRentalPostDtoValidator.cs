using FluentValidation;
using Homeji.Application.Common;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Domain.Entities;

namespace Homeji.Application.Services.RentalPosts.Validation;

public sealed class UpdateRentalPostDtoValidator : AbstractValidator<UpdateRentalPostDto>
{
    public UpdateRentalPostDtoValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty()
            .MaximumLength(RentalPost.MaxTitleLength);

        RuleFor(request => request.Description)
            .NotEmpty()
            .MaximumLength(RentalPost.MaxDescriptionLength);

        RuleFor(request => request.Price)
            .GreaterThan(0);

        RuleFor(request => request.Deposit)
            .GreaterThanOrEqualTo(0);

        RuleFor(request => request.Area)
            .GreaterThan(0);

        RuleFor(request => request.Address)
            .NotEmpty()
            .MaximumLength(RentalPost.MaxAddressLength);

        RuleFor(request => request.Latitude)
            .InclusiveBetween(HomejiServiceArea.MinLatitude, HomejiServiceArea.MaxLatitude)
            .WithMessage("Vị trí phải nằm trong khu vực Thủ Đức và Quận 9 cũ.");

        RuleFor(request => request.Longitude)
            .InclusiveBetween(HomejiServiceArea.MinLongitude, HomejiServiceArea.MaxLongitude)
            .WithMessage("Vị trí phải nằm trong khu vực Thủ Đức và Quận 9 cũ.");

        RuleFor(request => request.ElectricityPrice).GreaterThanOrEqualTo(0);
        RuleFor(request => request.WaterPrice).GreaterThanOrEqualTo(0);
        RuleFor(request => request.InternetPrice).GreaterThanOrEqualTo(0);
        RuleFor(request => request.MaxOccupants).GreaterThan(0);
        RuleFor(request => request.AvailableSlots)
            .GreaterThan(0)
            .LessThanOrEqualTo(request => request.MaxOccupants);
        RuleFor(request => request.HouseRules)
            .MaximumLength(RentalPost.MaxHouseRulesLength);
    }
}
