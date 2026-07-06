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

        RuleFor(request => request.Phone)
            .MaximumLength(UserProfile.MaxPhoneLength);

        RuleFor(request => request.AvatarPath)
            .MaximumLength(UserProfile.MaxAvatarPathLength);

        RuleFor(request => request.School)
            .MaximumLength(UserProfile.MaxSchoolLength);

        RuleFor(request => request.PreferredArea)
            .MaximumLength(UserProfile.MaxAreaLength);
    }
}
