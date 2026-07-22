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

    /// <summary>
    /// Vietnamese mobile prefixes (10 digits starting with 0):
    /// Viettel / Vinaphone / Mobifone / Vietnamobile / Gmobile / Itelecom.
    /// </summary>
    private static readonly Regex VietnamMobilePhoneRegex = new(
        "^0(3[2-9]|5[2689]|7[06-9]|8[1-9]|9[0-4]|9[6-9])[0-9]{7}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public UpdateMyProfileDtoValidator()
    {
        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .Must(BeValidFullName)
            .WithMessage("Full name must contain 2 to 60 characters, include family and given name, and use letters only.");

        RuleFor(request => request.Phone)
            .Must(BeValidVietnamMobilePhone)
            .When(request => !string.IsNullOrWhiteSpace(request.Phone))
            .WithMessage("Phone number must be a valid 10-digit Vietnam mobile number.");

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

    private static bool BeValidVietnamMobilePhone(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && VietnamMobilePhoneRegex.IsMatch(value);
    }
}
