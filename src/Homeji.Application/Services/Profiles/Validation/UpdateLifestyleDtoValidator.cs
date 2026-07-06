using FluentValidation;
using Homeji.Application.DTOs.Profiles;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.Profiles.Validation;

public sealed class UpdateLifestyleDtoValidator : AbstractValidator<UpdateLifestyleDto>
{
    public UpdateLifestyleDtoValidator()
    {
        RuleFor(request => request.Role)
            .Must(role => role is UserRole.Renter or UserRole.Landlord);

        RuleFor(request => request.MaxBudget)
            .GreaterThan(0)
            .When(request => request.MaxBudget.HasValue);

        RuleFor(request => request.PreferredArea)
            .MaximumLength(UserProfile.MaxAreaLength);
    }
}
