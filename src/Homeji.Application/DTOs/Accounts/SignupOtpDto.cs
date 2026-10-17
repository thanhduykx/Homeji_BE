namespace Homeji.Application.DTOs.Accounts;

public sealed record SignupOtpDto(string Email, string Code);
public sealed record ResendSignupOtpDto(string Email);
