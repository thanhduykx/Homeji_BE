using FluentValidation;
using Homeji.Application.DTOs.Profiles;
using Homeji.Domain.Entities;
using System.Text.RegularExpressions;

namespace Homeji.Application.Services.Profiles.Validation;

public sealed class UpdateMyProfileDtoValidator : AbstractValidator<UpdateMyProfileDto>
{
    private static readonly Regex FullNameRegex = new(
        "^[\\p{L}\\p{M}' -]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public UpdateMyProfileDtoValidator()
    {
        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .Must(BeValidFullName)
            .WithMessage("Full name must contain 2 to 60 characters, include family and given name, and use letters only.");

        RuleFor(request => request.Phone)
            .Matches("^0[0-9]{9}$")
            .When(request => !string.IsNullOrWhiteSpace(request.Phone))
            .WithMessage("Phone number must contain exactly 10 digits and start with 0.");

        RuleFor(request => request.AvatarPath)
            .MaximumLength(UserProfile.MaxAvatarPathLength);

        RuleFor(request => request.School)
            .MaximumLength(UserProfile.MaxSchoolLength);

        RuleFor(request => request.PreferredArea)
            .MaximumLength(UserProfile.MaxAreaLength);

        RuleFor(request => request.ContactAddress)
            .MaximumLength(UserProfile.MaxContactAddressLength);

        RuleFor(request => request.RentalNeed)
            .MaximumLength(UserProfile.MaxRentalNeedLength);
    }

    private static bool BeValidFullName(string? value)
    {
        var normalized = Regex.Replace(value?.Trim() ?? string.Empty, "\\s+", " ");
        return normalized.Length is >= 2 and <= 60
            && FullNameRegex.IsMatch(normalized)
            && normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2;
    }
}
