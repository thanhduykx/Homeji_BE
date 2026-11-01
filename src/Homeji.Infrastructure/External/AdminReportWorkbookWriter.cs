using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Homeji.Application.DTOs.Admin;
using Homeji.Application.IRepositories.Admin;
using Homeji.Domain.Enums;

namespace Homeji.Infrastructure.External;

// Minimal SpreadsheetML package: text is always inlineStr, never a formula or external link.
public sealed class AdminReportWorkbookWriter : IAdminReportWorkbookWriter
{
    private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private sealed record Sheet(string Name, string[] Headers, IEnumerable<object?[]> Rows);
    private sealed record Money(decimal Value);
    public byte[] Write(AdminReportingSnapshot snapshot)
    {
        var analytics = snapshot.Analytics;
        var kpis = analytics.Kpis;
        Sheet[] sheets = [
            new("Tổng quan", ["Chỉ tiêu", "Giá trị"], new object?[][] {
                ["Báo cáo Homeji", "Tài khoản, gói thành viên và hoạt động sản phẩm"],
                ["Thời điểm lấy dữ liệu (UTC+7)", analytics.GeneratedAt], ["Khoảng hoạt động (ngày)", analytics.PeriodDays],
                ["Tài khoản đã đăng ký (không gồm tài khoản đã xóa)", snapshot.Accounts.Count],
                ["Tài khoản Premium đang có hiệu lực", snapshot.Accounts.Count(account => account.IsPremium)],
                ["Lượt đăng ký gói (gồm lịch sử và gia hạn)", snapshot.Members.Count],
                ["Hồ sơ mới trong kỳ", kpis.NewUsers], ["Tin đang hoạt động", kpis.ActiveListings],
                ["Tin mới trong kỳ", kpis.NewListings], ["Lượt tìm trong kỳ", kpis.Searches],
                ["Lượt xem tin trong kỳ", kpis.ListingViews], ["Lượt lưu trong kỳ", kpis.Saves], ["Yêu cầu xem phòng trong kỳ", kpis.ViewingRequests],
                ["Phạm vi danh sách", "Tài khoản và gói: toàn bộ dữ liệu hiện có; hoạt động: theo khoảng ngày đã chọn."],
                ["Lưu ý thanh toán", "Gói được cấp trực tiếp có thể không có giao dịch. Trạng thái gói khác với trạng thái thanh toán."],
                ["Nguồn", "Homeji: auth.users, hồ sơ, user_subscriptions, payment_transactions và số liệu hoạt động hệ thống."] }),
            new("Tài khoản", ["Mã tài khoản", "Họ tên", "Email", "Vai trò", "Đăng ký lúc (UTC+7)", "Xác nhận email", "Hoàn tất hồ sơ", "Premium hiện tại", "Hết hạn (UTC+7)"],
                snapshot.Accounts.Select(account => new object?[] { account.UserId, account.DisplayName, account.Email, Role(account.Role), account.RegisteredAt,
                    account.EmailConfirmed ? "Đã xác nhận" : "Chưa xác nhận", account.ProfileCompleted ? "Đã hoàn tất" : "Chưa hoàn tất", account.IsPremium ? "Có" : "Không", account.PremiumExpiresAt })),
            new("Gói thành viên", ["Mã đăng ký", "Mã tài khoản", "Họ tên", "Email", "Mã gói", "Tên gói", "Trạng thái gói", "Đăng ký lúc (UTC+7)", "Bắt đầu (UTC+7)", "Hết hạn (UTC+7)", "Mã thanh toán", "Số tiền giao dịch (VND)", "Trạng thái thanh toán", "Phương thức"],
                snapshot.Members.Select(member => new object?[] { member.Id, member.UserId, member.DisplayName, member.Email, member.PackageCode, member.PackageName, State(member.State),
                    member.RegisteredAt, member.StartedAt, member.ExpiresAt, member.PaymentId, member.Amount is { } amount ? new Money(amount) : null,
                    member.PaymentId is null ? "Cấp trực tiếp / không có giao dịch" : Payment(member.PaymentStatus), Method(member.PaymentMethod) })),
            new("Hoạt động", ["Ngày", "Hồ sơ mới", "Tin mới", "Lượt tìm", "Lượt xem tin", "Lượt lưu", "Yêu cầu xem phòng"],
                analytics.Trend.Select(point => new object?[] { point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), point.NewUsers, point.NewListings, point.Searches, point.ListingViews, point.Saves, point.ViewingRequests })),
            new("Khu vực", ["Khu vực", "Tin hoạt động", "Giá cả phòng trung vị (VND/tháng)", "Mẫu giá hợp lệ", "Mẫu loại trừ", "Lượt xem", "Lượt lưu", "Yêu cầu xem", "Gợi ý"],
                analytics.Areas.Select(area => new object?[] { area.AreaName, area.ActiveListings, area.PriceSampleCount > 0 ? new Money(area.MedianMonthlyPrice) : null,
                    area.PriceSampleCount, area.ExcludedPriceCount, area.TotalViews, area.TotalSaves, area.ViewingRequests, area.Recommendation }))
        ];
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            XNamespace content = "http://schemas.openxmlformats.org/package/2006/content-types";
            AddXml(zip, "[Content_Types].xml", new XElement(content + "Types",
                new XElement(content + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(content + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                Override(content, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"),
                Override(content, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"),
                sheets.Select((_, index) => Override(content, $"/xl/worksheets/sheet{index + 1}.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"))));
            XNamespace relations = "http://schemas.openxmlformats.org/package/2006/relationships";
            AddXml(zip, "_rels/.rels", new XElement(relations + "Relationships", Relation(relations, "rId1", "officeDocument", "xl/workbook.xml")));
            XNamespace ns = Main;
            AddXml(zip, "xl/workbook.xml", new XElement(ns + "workbook", new XAttribute(XNamespace.Xmlns + "r", Relationships),
                new XElement(ns + "sheets", sheets.Select((sheet, index) => new XElement(ns + "sheet", new XAttribute("name", sheet.Name), new XAttribute("sheetId", index + 1), new XAttribute(XName.Get("id", Relationships), $"rId{index + 1}"))))));
            AddXml(zip, "xl/_rels/workbook.xml.rels", new XElement(relations + "Relationships",
                sheets.Select((_, index) => Relation(relations, $"rId{index + 1}", "worksheet", $"worksheets/sheet{index + 1}.xml")),
                Relation(relations, "styles", "styles", "styles.xml")));
            WriteStyles(zip);
            for (var index = 0; index < sheets.Length; index++) WriteSheet(zip, index + 1, sheets[index]);
        }
        return output.ToArray();
    }
    private static XElement Override(XNamespace ns, string name, string type) => new(ns + "Override", new XAttribute("PartName", name), new XAttribute("ContentType", type));
    private static XElement Relation(XNamespace ns, string id, string type, string target) => new(ns + "Relationship", new XAttribute("Id", id), new XAttribute("Type", Relationships + "/" + type), new XAttribute("Target", target));
    private static void AddXml(ZipArchive zip, string path, XElement root)
    {
        using var stream = zip.CreateEntry(path, CompressionLevel.Fastest).Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false });
        root.Save(writer);
    }
    private static void WriteStyles(ZipArchive zip)
    {
        const string styles = """
            <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <numFmts count="2"><numFmt numFmtId="164" formatCode="#,##0&quot; ₫&quot;"/><numFmt numFmtId="165" formatCode="dd/mm/yyyy hh:mm"/></numFmts>
              <fonts count="2"><font><sz val="11"/><name val="Arial"/></font><font><b/><color rgb="FFFFFFFF"/><sz val="11"/><name val="Arial"/></font></fonts>
              <fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF155E63"/><bgColor indexed="64"/></patternFill></fill></fills>
              <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
              <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
              <cellXfs count="4"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"><alignment vertical="top" wrapText="1"/></xf><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"><alignment vertical="center" wrapText="1"/></xf><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs>
              <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
            </styleSheet>
            """;
        AddXml(zip, "xl/styles.xml", XElement.Parse(styles));
    }
    private static void WriteSheet(ZipArchive zip, int index, Sheet sheet)
    {
        using var stream = zip.CreateEntry($"xl/worksheets/sheet{index}.xml", CompressionLevel.Fastest).Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
        writer.WriteStartElement("worksheet", Main);
        writer.WriteStartElement("sheetViews"); writer.WriteStartElement("sheetView"); writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane"); writer.WriteAttributeString("ySplit", "1"); writer.WriteAttributeString("topLeftCell", "A2"); writer.WriteAttributeString("activePane", "bottomLeft"); writer.WriteAttributeString("state", "frozen"); writer.WriteEndElement(); writer.WriteEndElement(); writer.WriteEndElement();
        writer.WriteStartElement("cols");
        for (var column = 0; column < sheet.Headers.Length; column++)
        {
            writer.WriteStartElement("col"); writer.WriteAttributeString("min", (column + 1).ToString(CultureInfo.InvariantCulture)); writer.WriteAttributeString("max", (column + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("width", index == 1 ? column == 0 ? "55" : "95" : column < 4 ? "34" : "25"); writer.WriteAttributeString("customWidth", "1"); writer.WriteEndElement();
        }
        writer.WriteEndElement(); writer.WriteStartElement("sheetData");
        var row = 1;
        WriteRow(writer, row++, sheet.Headers, true);
        foreach (var values in sheet.Rows) WriteRow(writer, row++, values, false);
        writer.WriteEndElement();
        writer.WriteStartElement("autoFilter"); writer.WriteAttributeString("ref", $"A1:{Column(sheet.Headers.Length - 1)}{row - 1}"); writer.WriteEndElement();
        writer.WriteEndElement();
    }
    private static void WriteRow(XmlWriter writer, int row, IEnumerable<object?> values, bool header)
    {
        writer.WriteStartElement("row"); writer.WriteAttributeString("r", row.ToString(CultureInfo.InvariantCulture));
        if (header) { writer.WriteAttributeString("ht", "32"); writer.WriteAttributeString("customHeight", "1"); }
        var column = 0;
        foreach (var value in values)
        {
            writer.WriteStartElement("c"); writer.WriteAttributeString("r", $"{Column(column++)}{row}");
            writer.WriteAttributeString("s", header ? "1" : value is Money ? "2" : value is DateTimeOffset ? "3" : "0");
            if (value is Money money) writer.WriteElementString("v", money.Value.ToString(CultureInfo.InvariantCulture));
            else if (value is DateTimeOffset date) writer.WriteElementString("v", date.ToOffset(TimeSpan.FromHours(7)).DateTime.ToOADate().ToString(CultureInfo.InvariantCulture));
            else if (value is int or long or decimal or double) writer.WriteElementString("v", Convert.ToString(value, CultureInfo.InvariantCulture));
            else
            {
                writer.WriteAttributeString("t", "inlineStr"); writer.WriteStartElement("is"); writer.WriteStartElement("t"); writer.WriteAttributeString("xml", "space", XNamespace.Xml.NamespaceName, "preserve");
                writer.WriteString(Clean(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)); writer.WriteEndElement(); writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }
    private static string Clean(string text)
    {
        var cleaned = new string(text.Where(character => !char.IsControl(character) || character is '\n' or '\r' or '\t').Take(32767).ToArray());
        return cleaned.Length > 0 && char.IsHighSurrogate(cleaned[^1]) ? cleaned[..^1] : cleaned;
    }
    private static string Column(int column)
    {
        var result = string.Empty;
        do { result = (char)('A' + column % 26) + result; column = column / 26 - 1; } while (column >= 0);
        return result;
    }
    private static string Role(int? role) => role switch { 1 => "Người thuê", 2 => "Chủ nhà", 3 => "Quản trị viên", _ => "Chưa cập nhật" };
    private static string State(string state) => state switch { "active" => "Đang có hiệu lực", "scheduled" => "Chưa đến ngày bắt đầu", "expired" => "Đã hết hạn", "cancelled" => "Đã hủy", _ => "Chưa rõ" };
    private static string Payment(PaymentStatus? state) => state switch { PaymentStatus.Paid => "Đã hoàn tất", PaymentStatus.Pending => "Chờ thanh toán", PaymentStatus.Cancelled => "Đã hủy", PaymentStatus.Failed => "Thất bại", PaymentStatus.Expired => "Đã hết hạn", _ => "Chưa rõ" };
    private static string Method(PaymentMethod? method) => method switch { PaymentMethod.Momo => "MoMo", PaymentMethod.PayOs => "PayOS", PaymentMethod.Wallet => "Ví Homeji", _ => "Không có" };
}
