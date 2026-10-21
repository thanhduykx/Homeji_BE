using Homeji.Application.DTOs.Emails;

namespace Homeji.Infrastructure.External;

internal static class TransactionReceiptFiles
{
    public static IReadOnlyList<EmailAttachmentDto> Create(TransactionReceiptDto receipt) =>
    [
        new($"Homeji-{ReceiptPresentation.Id(receipt)}.pdf", Convert.ToBase64String(TransactionReceiptPdf.Render(receipt))),
        new("Homeji-logo.png", Convert.ToBase64String(LogoBytes())),
    ];

    private static byte[] LogoBytes()
    {
        using var stream = typeof(TransactionReceiptFiles).Assembly
            .GetManifestResourceStream("Homeji.Infrastructure.External.EmailAssets.homeji-logo.png")
            ?? throw new InvalidOperationException("The Homeji email logo could not be loaded.");
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}
