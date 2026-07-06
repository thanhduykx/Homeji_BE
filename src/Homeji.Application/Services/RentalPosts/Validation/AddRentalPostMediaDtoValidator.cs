using FluentValidation;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Domain.Entities;

namespace Homeji.Application.Services.RentalPosts.Validation;

public sealed class AddRentalPostMediaDtoValidator : AbstractValidator<AddRentalPostMediaDto>
{
    public AddRentalPostMediaDtoValidator()
    {
        RuleFor(request => request.Bucket)
            .Equal("homeji-media");

        RuleFor(request => request.Path)
            .NotEmpty()
            .MaximumLength(RentalPostMedia.MaxPathLength);

        RuleFor(request => request.SortOrder)
            .GreaterThanOrEqualTo(0);
    }
}
