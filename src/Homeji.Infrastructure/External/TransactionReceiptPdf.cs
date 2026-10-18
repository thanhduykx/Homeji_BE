using System.Globalization;
using System.Text;
using Homeji.Application.DTOs.Emails;
using SkiaSharp;

namespace Homeji.Infrastructure.External;

internal static class TransactionReceiptPdf
{
    private static Stream Asset(string file) => typeof(TransactionReceiptPdf).Assembly
        .GetManifestResourceStream("Homeji.Infrastructure.External.EmailAssets." + file)
        ?? throw new InvalidOperationException("A receipt branding asset is missing.");

    public static byte[] Render(TransactionReceiptDto receipt)
    {
        using var regularStream = Asset("NotoSans-Regular.ttf");
        using var boldStream = Asset("NotoSans-Bold.ttf");
        using var logoStream = Asset("homeji-logo.png");
        using var regular = SKTypeface.FromStream(regularStream) ?? throw new InvalidOperationException("Receipt font could not be loaded.");
        using var bold = SKTypeface.FromStream(boldStream) ?? throw new InvalidOperationException("Receipt bold font could not be loaded.");
        using var logoData = SKData.Create(logoStream);
        using var logo = SKImage.FromEncodedData(logoData) ?? throw new InvalidOperationException("Receipt logo could not be loaded.");
        using var output = new MemoryStream();
        using var document = SKDocument.CreatePdf(output, new SKDocumentPdfMetadata
        {
            Title = "Biên nhận giao dịch Homeji", Author = "Homeji", Creator = "Homeji",
        }) ?? throw new InvalidOperationException("The PDF document could not be created.");
        using var writer = new ReceiptWriter(document, regular, bold, logo);
        writer.Write(receipt);
        document.Close();
        return output.ToArray();
    }

    private sealed class ReceiptWriter : IDisposable
    {
        private const float Width = 595, Height = 842, Margin = 42, ContentWidth = Width - Margin * 2;
        private readonly SKDocument _document;
        private readonly SKImage _logo;
        private readonly SKFont _body, _small, _heading, _amount, _brand, _bodyBold;
        private readonly SKPaint _paint = new() { IsAntialias = true };
        private SKCanvas _canvas = null!;
        private float _y;
        private int _page;
        private static readonly SKColor Ink = SKColor.Parse("#193737"), Teal = SKColor.Parse("#087f88"), Muted = SKColor.Parse("#627976");

        public ReceiptWriter(SKDocument document, SKTypeface regular, SKTypeface bold, SKImage logo)
        {
            _document = document; _logo = logo;
            _body = new SKFont(regular, 11); _small = new SKFont(regular, 9);
            _heading = new SKFont(bold, 19); _amount = new SKFont(bold, 30);
            _brand = new SKFont(bold, 24); _bodyBold = new SKFont(bold, 11);
            NewPage();
        }

        private void Text(string value, float x, float y, SKFont font, SKColor color)
        {
            _paint.Color = color; _canvas.DrawText(value, x, y, SKTextAlign.Left, font, _paint);
        }
        private void Rect(SKRect rect, SKColor color, float radius = 0)
        {
            _paint.Color = color;
            if (radius == 0) _canvas.DrawRect(rect, _paint); else _canvas.DrawRoundRect(rect, radius, radius, _paint);
        }
        private void NewPage()
        {
            if (_page > 0) _document.EndPage();
            _page++;
            _canvas = _document.BeginPage(Width, Height);
            _canvas.Clear(SKColors.White);
            Rect(new SKRect(0, 0, Width, 90), Teal);
            Rect(new SKRect(Margin, 24, Margin + 44, 68), SKColors.White, 6);
            _canvas.DrawImage(_logo, new SKRect(Margin + 4, 28, Margin + 40, 64), new SKSamplingOptions(SKFilterMode.Linear));
            Text("Homeji", Margin + 56, 47, _brand, SKColors.White);
            Text("Một nơi để thuộc về", Margin + 56, 66, _small, SKColor.Parse("#d9f3ec"));
            Text("BIÊN NHẬN ĐIỆN TỬ", Width - Margin - 105, 48, _small, SKColors.White);
            Rect(new SKRect(0, 90, Width, 94), SKColor.Parse("#84d9c5"));
            Rect(new SKRect(0, 98, Width, 101), Teal);
            Text("Biên nhận giao dịch Homeji - không phải hóa đơn thuế/VAT.", Margin, Height - 48, _small, Muted);
            Text("homeji.onrender.com", Margin, Height - 29, _small, Teal);
            Text("Trang " + _page.ToString(CultureInfo.InvariantCulture), Width - Margin - 38, Height - 29, _small, Muted);
            _y = 126;
        }
        private void Ensure(float height) { if (_y + height > Height - 80) NewPage(); }

        // Wrap grapheme clusters as well as words, so long references cannot run off the page.
        private static IEnumerable<string> Lines(string value, SKFont font, float width)
        {
            foreach (var paragraph in value.Normalize(NormalizationForm.FormC).Replace("\r", "", StringComparison.Ordinal).Split('\n'))
            {
                var line = new StringBuilder();
                var pendingWord = new StringBuilder();
                var pieces = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in pieces)
                {
                    var candidate = line.Length == 0 ? word : line + " " + word;
                    if (font.MeasureText(candidate) <= width) { if (line.Length > 0) line.Append(' '); line.Append(word); continue; }
                    if (line.Length > 0) { yield return line.ToString(); line.Clear(); }
                    var elements = StringInfo.GetTextElementEnumerator(word);
                    while (elements.MoveNext())
                    {
                        var element = elements.GetTextElement();
                        if (pendingWord.Length > 0 && font.MeasureText(pendingWord.ToString() + element) > width)
                        { yield return pendingWord.ToString(); pendingWord.Clear(); }
                        pendingWord.Append(element);
                    }
                    line.Append(pendingWord); pendingWord.Clear();
                }
                yield return line.ToString();
            }
        }
        private void Paragraph(string value, SKFont font, SKColor color, float lineHeight = 18)
        {
            foreach (var line in Lines(value, font, ContentWidth))
            { Ensure(lineHeight); Text(line, Margin, _y, font, color); _y += lineHeight; }
        }
        private float Metadata(string label, string value, float x, float y, float width)
        {
            Text(label, x, y, _small, Muted); y += 19;
            foreach (var line in Lines(value, _body, width))
            { Text(line, x, y, _body, Ink); y += 17; }
            return y + 15;
        }
        private void DashedRule(float x, float y, float width)
        {
            for (var offset = 0f; offset < width; offset += 7)
                Rect(new SKRect(x + offset, y, x + Math.Min(offset + 4, width), y + .7f), SKColor.Parse("#b8ccc5"));
        }
        private void Total(string value, float x, float y, float width)
        {
            DashedRule(x, y, width);
            Text("TỔNG TIỀN", x, y + 26, _small, Ink);
            var size = _bodyBold.Size;
            _bodyBold.Size = 14;
            Text(value, x + width - _bodyBold.MeasureText(value), y + 27, _bodyBold, Teal);
            _bodyBold.Size = size;
        }
        public void Write(TransactionReceiptDto receipt)
        {
            Text("GIAO DỊCH CỦA BẠN TẠI HOMEJI", Margin, _y, _small, Muted); _y += 29;
            Paragraph(TransactionReceiptHtml.Title(receipt.Event), _heading, Teal, 26); _y += 13;
            Ensure(80);
            var amount = ReceiptPresentation.Amount(receipt);
            Text("Tổng tiền giao dịch", Margin, _y, _small, Muted);
            Text("Ngày · Giờ (UTC+7)", Margin + 266, _y, _small, Muted);
            while (_amount.MeasureText(amount) > 246 && _amount.Size > 18) _amount.Size--;
            Text(amount, Margin, _y + 35, _amount, Teal);
            Text(ReceiptPresentation.Time(receipt), Margin + 266, _y + 30, _small, Ink);
            _y += 70;

            const float leftWidth = 166, gap = 24, paperPadding = 18;
            var paperX = Margin + leftWidth + gap;
            var paperWidth = ContentWidth - leftWidth - gap;
            var textWidth = paperWidth - paperPadding * 2;
            var detailLines = Lines(receipt.Description, _body, textWidth).ToArray();
            var metadataHeight = 136 + 17 * (Lines(receipt.ReferenceCode, _body, leftWidth).Count()
                + Lines(ReceiptPresentation.Id(receipt), _body, leftWidth).Count());
            var panelHeight = Math.Max(metadataHeight, 145 + detailLines.Length * 18);
            if (panelHeight <= Height - 110 - _y)
            {
                var top = _y;
                Rect(new SKRect(Margin - 16, top - 16, Width - Margin + 16, top + panelHeight + 16), SKColor.Parse("#f3f6f5"));
                Text("CHI TIẾT GIAO DỊCH", Margin, top + 8, _bodyBold, Teal);
                var metaY = Metadata("Trạng thái", ReceiptPresentation.Status(receipt.Event), Margin, top + 38, leftWidth);
                metaY = Metadata("Mã giao dịch", receipt.ReferenceCode, Margin, metaY, leftWidth);
                Metadata("Mã biên nhận", ReceiptPresentation.Id(receipt), Margin, metaY, leftWidth);
                Rect(new SKRect(paperX - 1, top - 1, paperX + paperWidth + 1, top + panelHeight + 1), SKColor.Parse("#dbe5e2"));
                Rect(new SKRect(paperX, top, paperX + paperWidth, top + panelHeight), SKColors.White);
                var x = paperX + paperPadding;
                Text("BIÊN NHẬN CỦA BẠN", x, top + 26, _bodyBold, Teal);
                Text("Được ghi nhận bởi Homeji", x, top + 44, _small, Muted);
                DashedRule(x, top + 60, textWidth);
                Text("Nội dung giao dịch", x, top + 79, _small, Muted);
                var lineY = top + 103;
                foreach (var line in detailLines) { Text(line, x, lineY, _body, Ink); lineY += 18; }
                Total(amount, x, top + panelHeight - 47, textWidth);
                _y = top + panelHeight + 49;
            }
            else
            {
                // Long receipts switch to full-width flowing content; no detail is dropped.
                Paragraph("CHI TIẾT GIAO DỊCH", _bodyBold, Teal); _y += 10;
                Paragraph("Trạng thái: " + ReceiptPresentation.Status(receipt.Event), _body, Ink);
                Paragraph("Mã giao dịch: " + receipt.ReferenceCode, _body, Ink);
                Paragraph("Mã biên nhận: " + ReceiptPresentation.Id(receipt), _body, Ink); _y += 20;
                Ensure(55); Paragraph("BIÊN NHẬN CỦA BẠN", _bodyBold, Teal); _y += 10;
                Paragraph(receipt.Description, _body, Ink); _y += 15;
                Ensure(55); Total(amount, Margin, _y, ContentWidth); _y += 62;
            }
            Paragraph(ReceiptPresentation.Note(receipt.Event), _small, Muted, 15);
            _document.EndPage();
        }
        public void Dispose()
        {
            _body.Dispose(); _small.Dispose(); _heading.Dispose(); _amount.Dispose(); _brand.Dispose(); _bodyBold.Dispose(); _paint.Dispose();
        }
    }
}
