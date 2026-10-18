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
            Rect(new SKRect(0, 0, Width, 5), Teal);
            _canvas.DrawImage(_logo, new SKRect(Margin, 28, Margin + 48, 76), new SKSamplingOptions(SKFilterMode.Linear));
            Text("Homeji", Margin + 60, 55, _brand, Teal);
            Text("Một nơi để thuộc về", Margin + 60, 74, _small, Muted);
            Rect(new SKRect(Margin, 98, Width - Margin, 99), SKColor.Parse("#dbe8e5"));
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
        private void Row(string label, string value)
        {
            var first = true;
            foreach (var line in Lines(value, _body, ContentWidth - 132))
            {
                Ensure(20);
                if (first) Text(label, Margin, _y, _small, Muted);
                Text(line, Margin + 132, _y, _body, Ink); _y += 18; first = false;
            }
            _y += 11;
            Rect(new SKRect(Margin, _y - 5, Width - Margin, _y - 4), SKColor.Parse("#e2ece9"));
            _y += 10;
        }
        public void Write(TransactionReceiptDto receipt)
        {
            Text("BIÊN NHẬN GIAO DỊCH", Margin, _y, _bodyBold, Teal); _y += 31;
            Paragraph(TransactionReceiptHtml.Title(receipt.Event), _heading, Ink, 26); _y += 14;
            Ensure(123);
            Rect(new SKRect(Margin, _y - 4, Width - Margin, _y + 105), SKColor.Parse("#eef8f5"), 10);
            Text("SỐ TIỀN GIAO DỊCH", Margin + 20, _y + 19, _small, Muted);
            var amount = ReceiptPresentation.Amount(receipt);
            while (_amount.MeasureText(amount) > ContentWidth - 40 && _amount.Size > 18) _amount.Size--;
            Text(amount, Margin + 20, _y + 60, _amount, Teal);
            Text(ReceiptPresentation.Status(receipt.Event), Margin + 20, _y + 87, _bodyBold, SKColor.Parse("#245b48"));
            _y += 139;
            Row("Mã biên nhận", ReceiptPresentation.Id(receipt));
            Row("Mã giao dịch", receipt.ReferenceCode);
            Row("Thời gian", ReceiptPresentation.Time(receipt));
            Ensure(48); _y += 4;
            Text("NỘI DUNG GIAO DỊCH", Margin, _y, _bodyBold, Muted); _y += 25;
            Paragraph(receipt.Description, _body, Ink); _y += 22;
            Paragraph(ReceiptPresentation.Note(receipt.Event), _small, Muted, 15);
            _document.EndPage();
        }
        public void Dispose()
        {
            _body.Dispose(); _small.Dispose(); _heading.Dispose(); _amount.Dispose(); _brand.Dispose(); _bodyBold.Dispose(); _paint.Dispose();
        }
    }
}
