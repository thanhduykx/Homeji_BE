using FluentValidation;
using Homeji.Application.DTOs.Profiles;
using Homeji.Domain.Entities;

namespace Homeji.Application.Services.Profiles.Validation;

public sealed class UpdateMyProfileDtoValidator : AbstractValidator<UpdateMyProfileDto>
{
    public UpdateMyProfileDtoValidator()
    {
        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .MaximumLength(UserProfile.MaxDisplayNameLength);
    }
}
