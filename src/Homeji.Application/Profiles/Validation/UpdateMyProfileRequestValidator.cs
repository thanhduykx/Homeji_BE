using FluentValidation;
using Homeji.Application.Profiles.Models;
using Homeji.Domain.Profiles;

namespace Homeji.Application.Profiles.Validation;

public sealed class UpdateMyProfileRequestValidator : AbstractValidator<UpdateMyProfileRequest>
{
    public UpdateMyProfileRequestValidator()
    {
        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .MaximumLength(UserProfile.MaxDisplayNameLength);
    }
}
