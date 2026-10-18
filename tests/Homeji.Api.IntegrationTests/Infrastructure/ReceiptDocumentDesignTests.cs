using System.Text;
using Homeji.Application.DTOs.Emails;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.External;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class ReceiptDocumentDesignTests
{
    [Theory]
    [InlineData(ReceiptEvent.DepositSucceeded)]
    [InlineData(ReceiptEvent.PaymentSucceeded)]
    [InlineData(ReceiptEvent.WithdrawalRequested)]
    [InlineData(ReceiptEvent.WithdrawalCompleted)]
    [InlineData(ReceiptEvent.WithdrawalRejected)]
    [InlineData(ReceiptEvent.PurchasePaid)]
    [InlineData(ReceiptEvent.PurchaseRefunded)]
    public void Receipt_contains_branded_html_body_and_both_portable_files(ReceiptEvent value)
    {
        var receipt = Example(value);
        var html = TransactionReceiptHtml.Render(receipt);
        var files = TransactionReceiptFiles.Create(receipt, html);
        var pdf = Convert.FromBase64String(files[0].Content);
        Assert.EndsWith(".pdf", files[0].Name, StringComparison.Ordinal);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf), StringComparison.Ordinal);
        // Embedded Vietnamese fonts keep files portable; budget remains well below email limits.
        Assert.InRange(pdf.Length, 5_000, 1_500_000);
        Assert.EndsWith(".html", files[1].Name, StringComparison.Ordinal);
        Assert.Equal(html, Encoding.UTF8.GetString(Convert.FromBase64String(files[1].Content)));
        Assert.Contains(HomejiEmailLayout.LogoUrl, html, StringComparison.Ordinal);
        Assert.Contains(ReceiptPresentation.Amount(receipt), html, StringComparison.Ordinal);
        Assert.Contains(ReceiptPresentation.Status(value), html, StringComparison.Ordinal);
        Assert.Contains("không phải hóa đơn thuế/VAT", html, StringComparison.Ordinal);

        var output = Environment.GetEnvironmentVariable("HOMEJI_EMAIL_PREVIEW_DIR");
        if (!string.IsNullOrEmpty(output) && value == ReceiptEvent.PurchasePaid)
        {
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, "receipt.pdf"), pdf);
            File.WriteAllText(Path.Combine(output, "receipt.html"), html);
            File.WriteAllText(Path.Combine(output, "signup-otp.html"), HomejiEmailLayout.Otp("123456", false));
            File.WriteAllText(Path.Combine(output, "recovery-otp.html"), HomejiEmailLayout.Otp("123456", true));
        }
    }

    [Fact]
    public void Long_receipt_preserves_reference_and_multiline_description_in_pdf()
    {
        var receipt = Example(ReceiptEvent.PaymentSucceeded) with
        {
            ReferenceCode = new string('R', 120),
            Description = string.Concat(Enumerable.Repeat("Tiếng Việt\n", 45)),
        };
        var pdf = TransactionReceiptPdf.Render(receipt);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf), StringComparison.Ordinal);
        var output = Environment.GetEnvironmentVariable("HOMEJI_EMAIL_PREVIEW_DIR");
        if (!string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, "receipt-long.pdf"), pdf);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Otp_email_has_brand_code_expiration_and_security_note(bool recovery)
    {
        var html = HomejiEmailLayout.Otp("12345678", recovery);
        Assert.Contains(HomejiEmailLayout.LogoUrl, html, StringComparison.Ordinal);
        Assert.Contains("12345678", html, StringComparison.Ordinal);
        Assert.Contains("10 phút", html, StringComparison.Ordinal);
        Assert.Contains("Không chuyển tiếp mã", html, StringComparison.Ordinal);
        Assert.Contains(recovery ? "Đặt lại mật khẩu" : "Xác thực đăng ký", html, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => HomejiEmailLayout.Otp("<script>", recovery));
    }

    private static TransactionReceiptDto Example(ReceiptEvent value) => new(
        Guid.Parse("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb"), Guid.Parse("cccccccc-1111-2222-3333-dddddddddddd"), value,
        125000, "VND", "KIEM-THU-HOMEJI-20261018", "KIỂM THỬ - không phải giao dịch thật. Bàn học gỗ · Số lượng 1 · Đơn giá 125000 VND · Thanh toán bằng số dư Homeji.",
        new DateTimeOffset(2026, 10, 18, 3, 30, 0, TimeSpan.Zero), null, 0, false);
}
