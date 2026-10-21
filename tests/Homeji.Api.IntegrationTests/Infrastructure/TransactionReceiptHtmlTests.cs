using Homeji.Application.DTOs.Emails;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.External;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class TransactionReceiptHtmlTests
{
    [Fact]
    public void Pending_withdrawal_does_not_claim_transfer_completion_and_escapes_content()
    {
        var dto = new TransactionReceiptDto(Guid.NewGuid(), Guid.NewGuid(), ReceiptEvent.WithdrawalRequested,
            100000, "VND", "<script>reference</script>", "<img src=x onerror=alert(1)>",
            DateTimeOffset.UnixEpoch, null, 0, false);
        var html = TransactionReceiptHtml.Render(dto);
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img src=x", html, StringComparison.Ordinal);
        Assert.Contains(HomejiEmailLayout.LogoUrl, html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("không xác nhận tiền đã tới ngân hàng", html, StringComparison.Ordinal);
        Assert.Contains("không phải hóa đơn thuế/VAT", html, StringComparison.Ordinal);
        Assert.Contains("07:00:00 (UTC+7)", html, StringComparison.Ordinal);
    }
    [Fact]
    public void History_link_opens_protected_wallet_history_in_new_browser_tab()
    {
        var dto = new TransactionReceiptDto(Guid.NewGuid(), Guid.NewGuid(), ReceiptEvent.DepositSucceeded,
            100000, "VND", "TEST-REFERENCE", "Kiểm thử liên kết", DateTimeOffset.UnixEpoch, null, 0, false);
        var html = TransactionReceiptHtml.Render(dto);
        Assert.Contains("href=\"https://homeji.onrender.com/marketplace?market=wallet&amp;wallet=history\" target=\"_blank\"", html, StringComparison.Ordinal);
        Assert.Contains("rel=\"noopener noreferrer\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("PDF và HTML", html, StringComparison.Ordinal);
    }
}
