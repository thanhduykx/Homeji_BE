using System.Text;
using Homeji.Application.DTOs.Emails;

namespace Homeji.Infrastructure.External;

internal static class TransactionReceiptFiles
{
    public static IReadOnlyList<EmailAttachmentDto> Create(TransactionReceiptDto receipt, string html) =>
    [
        new($"Homeji-{ReceiptPresentation.Id(receipt)}.pdf", Convert.ToBase64String(TransactionReceiptPdf.Render(receipt))),
        new($"Homeji-{ReceiptPresentation.Id(receipt)}.html", Convert.ToBase64String(Encoding.UTF8.GetBytes(html))),
    ];
}
