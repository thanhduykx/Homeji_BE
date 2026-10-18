using System.ComponentModel.DataAnnotations;

namespace Homeji.Application.DTOs.Accounts;

public sealed record RequestPasswordOtpDto([Required, EmailAddress, MaxLength(254)] string Email);
public sealed record ResetPasswordOtpDto(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, RegularExpression("^[0-9]{6}$")] string Code,
    [Required, MinLength(6), MaxLength(128)] string NewPassword);
