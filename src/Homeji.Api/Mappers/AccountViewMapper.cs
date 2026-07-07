using Homeji.Api.Views.Accounts;
using Homeji.Application.DTOs.Accounts;

namespace Homeji.Api.Mappers;

public static class AccountViewMapper
{
    public static RegisterAccountDto ToDto(RegisterAccountViewModel viewModel)
    {
        return new RegisterAccountDto(
            viewModel.Email,
            viewModel.Password,
            viewModel.DisplayName,
            viewModel.RedirectTo);
    }

    public static LoginAccountDto ToDto(LoginAccountViewModel viewModel)
    {
        return new LoginAccountDto(viewModel.Email, viewModel.Password);
    }

    public static ForgotPasswordDto ToDto(ForgotPasswordViewModel viewModel)
    {
        return new ForgotPasswordDto(viewModel.Email, viewModel.RedirectTo);
    }

    public static ResetPasswordDto ToDto(ResetPasswordViewModel viewModel)
    {
        return new ResetPasswordDto(viewModel.AccessToken, viewModel.NewPassword);
    }
}
