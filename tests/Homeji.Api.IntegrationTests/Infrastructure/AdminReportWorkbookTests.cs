using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Homeji.Application.DTOs.Admin;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.External;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class AdminReportWorkbookTests
{
    private static readonly string[] SheetNames = ["Tổng quan", "Tài khoản", "Gói thành viên", "Hoạt động", "Khu vực"];
    internal static AdminReportingSnapshot Sample()
    {
        var now = new DateTimeOffset(2026, 11, 1, 7, 0, 0, TimeSpan.Zero);
        var user = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var analytics = new AdminProductAnalyticsDto(now, 30, new AdminProductKpisDto(1, 1, 0, 0, 0, 0, 2, 3, 1, 0, 0, 0), [], []);
        return new([new AdminRegisteredAccountDto(user, "=HYPERLINK(\"https://example.invalid\")\u0001 Nguyễn An", "fixture@example.invalid", 1, now, true, false, true, now.AddDays(30))],
            [new AdminMemberDto(Guid.NewGuid(), user, "Nguyễn An", "fixture@example.invalid", "premium-30", "Premium 30 ngày", now, now.AddDays(30), now,
                "active", Guid.NewGuid(), 100000m, PaymentStatus.Paid, PaymentMethod.Wallet)], analytics);
    }
    [Fact]
    public void WorkbookHasReadableSheetsNumericAmountsAndNoUserSuppliedFormulas()
    {
        var snapshot = Sample();
        var bytes = new AdminReportWorkbookWriter().Write(snapshot);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var workbook = Load(zip, "xl/workbook.xml");
        Assert.Equal(SheetNames, workbook.Descendants(ns + "sheet").Select(sheet => (string?)sheet.Attribute("name")));
        var accounts = Load(zip, "xl/worksheets/sheet2.xml");
        var name = accounts.Descendants(ns + "c").Single(cell => (string?)cell.Attribute("r") == "B2");
        Assert.Equal("inlineStr", (string?)name.Attribute("t"));
        Assert.Contains("=HYPERLINK", name.Value);
        Assert.DoesNotContain('\u0001', name.Value);
        Assert.Contains("Nguyễn An", name.Value);
        var members = Load(zip, "xl/worksheets/sheet3.xml");
        var amount = members.Descendants(ns + "c").Single(cell => (string?)cell.Attribute("r") == "L2");
        Assert.Equal("100000", amount.Element(ns + "v")!.Value);
        Assert.Equal("2", (string?)amount.Attribute("s"));
        var date = accounts.Descendants(ns + "c").Single(cell => (string?)cell.Attribute("r") == "E2");
        Assert.Equal(snapshot.Accounts[0].RegisteredAt.ToOffset(TimeSpan.FromHours(7)).DateTime.ToOADate(), double.Parse(date.Element(ns + "v")!.Value, CultureInfo.InvariantCulture));
        foreach (var entry in zip.Entries.Where(entry => entry.FullName.EndsWith(".xml", StringComparison.Ordinal)))
        {
            var document = Load(zip, entry.FullName);
            Assert.Empty(document.Descendants(ns + "f"));
        }
        var fixture = Environment.GetEnvironmentVariable("HOMEJI_ADMIN_REPORT_FIXTURE");
        if (!string.IsNullOrWhiteSpace(fixture)) { Directory.CreateDirectory(Path.GetDirectoryName(fixture)!); File.WriteAllBytes(fixture, bytes); }
    }
    [Fact]
    public void MemberAndAccountQueriesCompileWithoutMaterializingAllRows()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql("Host=localhost;Database=query;Username=query;Password=unused").Options);
        var repository = new AdminReportingRepository(db, TimeProvider.System);
        Assert.Contains("auth.users", repository.AccountsQuery().Take(25).ToQueryString());
        Assert.Contains("LIMIT", repository.MembersQuery().Where(member => member.State == "active").Take(25).ToQueryString());
    }
    private static XDocument Load(ZipArchive zip, string name)
    {
        using var reader = new StreamReader(zip.GetEntry(name)!.Open(), Encoding.UTF8);
        return XDocument.Parse(reader.ReadToEnd());
    }
}
