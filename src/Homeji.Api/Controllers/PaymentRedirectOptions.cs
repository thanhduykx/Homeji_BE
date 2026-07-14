using System.ComponentModel.DataAnnotations;

namespace Homeji.Api.Controllers;

public sealed class PaymentRedirectOptions
{
    public const string SectionName = "Payments:Redirects";

    [Required]
    [Url]
    public string FrontendPaymentUrl { get; set; } = string.Empty;
}
