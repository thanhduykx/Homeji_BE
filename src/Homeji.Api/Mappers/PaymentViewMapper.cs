using Homeji.Api.Views.Payments;
using Homeji.Application.DTOs.Payments;

namespace Homeji.Api.Mappers;

public static class PaymentViewMapper
{
    public static CreateMomoPaymentDto ToDto(CreateMomoPaymentViewModel viewModel)
    {
        return new CreateMomoPaymentDto(viewModel.Amount, viewModel.Description);
    }

    public static CreatePayOsPaymentDto ToDto(CreatePayOsPaymentViewModel viewModel)
    {
        return new CreatePayOsPaymentDto(viewModel.Amount, viewModel.Description);
    }

    public static MomoIpnDto ToDto(MomoIpnViewModel viewModel)
    {
        return new MomoIpnDto(
            viewModel.PartnerCode,
            viewModel.OrderId,
            viewModel.RequestId,
            viewModel.Amount,
            viewModel.OrderInfo,
            viewModel.OrderType,
            viewModel.TransId,
            viewModel.ResultCode,
            viewModel.Message,
            viewModel.PayType,
            viewModel.ResponseTime,
            viewModel.ExtraData,
            viewModel.Signature);
    }

    public static PayOsWebhookDto ToDto(PayOsWebhookViewModel viewModel)
    {
        return new PayOsWebhookDto(
            viewModel.Code,
            viewModel.Desc,
            viewModel.Success,
            viewModel.Data is null
                ? null
                : new PayOsWebhookDataDto(
                    viewModel.Data.OrderCode,
                    viewModel.Data.Amount,
                    viewModel.Data.Description,
                    viewModel.Data.AccountNumber,
                    viewModel.Data.Reference,
                    viewModel.Data.TransactionDateTime,
                    viewModel.Data.Currency,
                    viewModel.Data.PaymentLinkId,
                    viewModel.Data.Code,
                    viewModel.Data.Desc,
                    viewModel.Data.CounterAccountBankId,
                    viewModel.Data.CounterAccountBankName,
                    viewModel.Data.CounterAccountName,
                    viewModel.Data.CounterAccountNumber,
                    viewModel.Data.VirtualAccountName,
                    viewModel.Data.VirtualAccountNumber),
            viewModel.Signature);
    }
}
