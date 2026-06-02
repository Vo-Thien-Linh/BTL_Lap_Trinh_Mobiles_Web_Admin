using SkiaSharp;
using System.Globalization;
using Web_Admin_Booking_App.Models;

namespace Web_Admin_Booking_App.Services;

public static class ReceiptPdfRenderer
{
    private const float PageWidth = 595f;
    private const float PageHeight = 842f;
    private const float TextSizeBoost = 3f;
    private static readonly SKColor Ink = new(22, 28, 45);
    private static readonly SKColor Muted = new(85, 95, 112);
    private static readonly SKColor Border = new(198, 206, 218);
    private static readonly SKColor Primary = new(61, 86, 191);

    public static byte[] Render(PaymentReceiptViewModel receipt)
    {
        using var stream = new MemoryStream();
        using var document = SKDocument.CreatePdf(stream);
        using var canvas = document.BeginPage(PageWidth, PageHeight);

        canvas.Clear(SKColors.White);

        using var regular = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal)
            ?? SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
            ?? SKTypeface.Default;
        using var bold = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
            ?? SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
            ?? SKTypeface.Default;
        using var italic = SKTypeface.FromFamilyName("Arial", SKFontStyle.Italic)
            ?? SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Italic)
            ?? SKTypeface.Default;

        DrawHeader(canvas, regular, bold);
        DrawTitle(canvas, receipt, regular, bold);
        DrawPatientInfo(canvas, receipt, regular, bold);

        var y = 352f;
        DrawText(canvas, "II. Chi phí khám, chữa bệnh", 42, y, 11, bold, Ink);
        y += 16;
        DrawTable(canvas, receipt, regular, bold, ref y);
        y += 28;

        DrawMoneyRow(canvas, "Tổng chi phí", receipt.TotalAmountVnd, y, regular);
        y += 18;
        DrawMoneyRow(canvas, "Số tiền được miễn giảm", receipt.DiscountVnd, y, regular);
        y += 12;
        DrawLine(canvas, 42, y, 553, y);
        y += 18;
        DrawMoneyRow(canvas, "Số tiền khách phải thanh toán", receipt.AmountDueVnd, y, bold);
        y += 26;
        DrawText(canvas, $"Bằng chữ (In written): {Value(receipt.AmountInWords)}", 42, y, 10, italic, Ink);

        DrawSignatures(canvas, receipt, regular, bold, italic);

        document.EndPage();
        document.Close();
        return stream.ToArray();
    }

    private static void DrawHeader(SKCanvas canvas, SKTypeface regular, SKTypeface bold)
    {
        DrawLogo(canvas, 42, 52, 44);
        DrawText(canvas, "BỆNH VIỆN TAI MŨI HỌNG SÀI GÒN", 104, 58, 12, bold, Ink);
        DrawText(canvas, "1-3 Trịnh Văn Cấn, P. Bến Thành, Q. 1, TPHCM", 104, 75, 9, regular, Muted);
        DrawText(canvas, "SĐT: (028) 38.213.456", 104, 89, 9, regular, Muted);
        DrawText(canvas, "Website: https://taimuihongsg.com", 104, 103, 9, regular, Muted);
    }

    private static void DrawLogo(SKCanvas canvas, float x, float y, float size)
    {
        using var fill = new SKPaint { Color = Primary, IsAntialias = true };
        using var white = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawRoundRect(new SKRect(x, y, x + size, y + size), 4, 4, fill);
        canvas.DrawRect(new SKRect(x + 18, y + 7, x + 26, y + size - 7), white);
        canvas.DrawRect(new SKRect(x + 7, y + 18, x + size - 7, y + 26), white);
    }

    private static void DrawTitle(SKCanvas canvas, PaymentReceiptViewModel receipt, SKTypeface regular, SKTypeface bold)
    {
        DrawCenteredText(canvas, "PHIẾU THU TIỀN", 154, 18, bold, Ink);
        DrawCenteredText(canvas, $"Mã BN: {receipt.InvoiceCode}", 174, 9, regular, Muted);
    }

    private static void DrawPatientInfo(SKCanvas canvas, PaymentReceiptViewModel receipt, SKTypeface regular, SKTypeface bold)
    {
        var y = 226f;
        DrawLabelValue(canvas, "Khách hàng (Client):", Value(receipt.PatientName), 42, y, 10, regular, bold);
        y += 24;
        DrawLabelValue(canvas, "Ngày sinh (Date of birth):", FormatDate(receipt.PatientDob), 42, y, 10, regular, bold);
        y += 24;
        DrawLabelValue(canvas, "Giới tính (Gender):", Value(receipt.PatientGender), 42, y, 10, regular, bold);
        y += 24;
        DrawLabelValue(canvas, "Địa chỉ (Address):", Value(receipt.PatientAddress), 42, y, 10, regular, bold);
        y += 24;
        DrawLabelValue(canvas, "Bác sĩ (Doctor):", Value(receipt.DoctorName), 42, y, 10, regular, bold);
    }

    private static void DrawTable(SKCanvas canvas, PaymentReceiptViewModel receipt, SKTypeface regular, SKTypeface bold, ref float y)
    {
        var left = 42f;
        var widths = new[] { 28f, 218f, 48f, 58f, 78f, 81f };
        var tableWidth = widths.Sum();
        var headerHeight = 30f;
        var rowHeight = 28f;
        var rows = receipt.Items.Take(8).ToList();
        var tableHeight = headerHeight + rows.Count * rowHeight;

        using var borderPaint = new SKPaint { Color = Border, StrokeWidth = 0.8f, IsAntialias = true, Style = SKPaintStyle.Stroke };
        canvas.DrawRect(new SKRect(left, y, left + tableWidth, y + tableHeight), borderPaint);
        DrawLine(canvas, left, y + headerHeight, left + tableWidth, y + headerHeight);

        var x = left;
        foreach (var width in widths.Take(widths.Length - 1))
        {
            x += width;
            DrawLine(canvas, x, y, x, y + tableHeight);
        }

        DrawText(canvas, "TT", left + 9, y + 18, 7.5f, bold, Ink);
        DrawText(canvas, "Mục", left + 36, y + 12, 7.5f, bold, Ink);
        DrawText(canvas, "(Description)", left + 36, y + 24, 6.5f, regular, Muted);
        DrawText(canvas, "SL", left + 254, y + 12, 7.5f, bold, Ink);
        DrawText(canvas, "(Qt)", left + 254, y + 24, 6.5f, regular, Muted);
        DrawText(canvas, "ĐVT", left + 302, y + 12, 7.5f, bold, Ink);
        DrawText(canvas, "(Unit)", left + 302, y + 24, 6.5f, regular, Muted);
        DrawText(canvas, "Đơn giá", left + 361, y + 12, 7.5f, bold, Ink);
        DrawText(canvas, "(Price)", left + 361, y + 24, 6.5f, regular, Muted);
        DrawText(canvas, "Thành tiền", left + 438, y + 12, 7.5f, bold, Ink);
        DrawText(canvas, "(Amount)", left + 438, y + 24, 6.5f, regular, Muted);

        var rowY = y + headerHeight + 18;
        foreach (var item in rows)
        {
            DrawText(canvas, item.Index.ToString(CultureInfo.InvariantCulture), left + 10, rowY, 8, regular, Ink);
            DrawText(canvas, Truncate(item.Description, 32), left + 36, rowY, 8, regular, Ink);
            DrawText(canvas, FormatQuantity(item.Quantity), left + 254, rowY, 8, regular, Ink);
            DrawText(canvas, Value(item.Unit), left + 302, rowY, 8, regular, Ink);
            DrawText(canvas, FormatMoney(item.UnitPriceVnd), left + 361, rowY, 8, regular, Ink);
            DrawText(canvas, FormatMoney(item.AmountVnd), left + 438, rowY, 8, regular, Ink);
            rowY += rowHeight;
        }

        y += tableHeight;
    }

    private static void DrawMoneyRow(SKCanvas canvas, string label, decimal amount, float y, SKTypeface typeface)
    {
        DrawText(canvas, label, 42, y, 10, typeface, Ink);
        DrawRightText(canvas, FormatMoney(amount), 553, y, 10, typeface, Ink);
    }

    private static void DrawSignatures(SKCanvas canvas, PaymentReceiptViewModel receipt, SKTypeface regular, SKTypeface bold, SKTypeface italic)
    {
        var y = 654f;
        DrawText(canvas, $"TP. Hồ Chí Minh, Ngày {receipt.PaidAt:dd} tháng {receipt.PaidAt:MM} năm {receipt.PaidAt:yyyy}", 318, y, 8.5f, italic, Muted);
        y += 22;
        DrawText(canvas, "Người trả tiền (Payer)", 42, y, 9.5f, bold, Ink);
        DrawText(canvas, "Bác sĩ điều trị (Physician)", 336, y, 9.5f, bold, Ink);
        y += 66;
        DrawText(canvas, "(Ký, họ tên)", 76, y, 8.5f, regular, Ink);
        DrawCenteredText(canvas, Value(receipt.DoctorName), y, 9.5f, bold, Ink, 336, 186);
    }

    private static void DrawText(SKCanvas canvas, string text, float x, float y, float size, SKTypeface typeface, SKColor color)
    {
        using var paint = new SKPaint
        {
            Typeface = typeface,
            TextSize = EffectiveSize(size),
            Color = color,
            IsAntialias = true,
            SubpixelText = true
        };
        canvas.DrawText(text, x, y, paint);
    }

    private static void DrawLabelValue(SKCanvas canvas, string label, string value, float x, float y, float size, SKTypeface regular, SKTypeface bold)
    {
        using var labelPaint = new SKPaint
        {
            Typeface = regular,
            TextSize = EffectiveSize(size),
            Color = Ink,
            IsAntialias = true,
            SubpixelText = true
        };
        using var valuePaint = new SKPaint
        {
            Typeface = bold,
            TextSize = EffectiveSize(size),
            Color = Ink,
            IsAntialias = true,
            SubpixelText = true
        };

        canvas.DrawText(label, x, y, labelPaint);
        canvas.DrawText($" {value}", x + labelPaint.MeasureText(label), y, valuePaint);
    }

    private static void DrawCenteredText(SKCanvas canvas, string text, float y, float size, SKTypeface typeface, SKColor color, float left = 42, float width = 511)
    {
        using var paint = new SKPaint
        {
            Typeface = typeface,
            TextSize = EffectiveSize(size),
            Color = color,
            IsAntialias = true,
            SubpixelText = true
        };
        var x = left + (width - paint.MeasureText(text)) / 2;
        canvas.DrawText(text, x, y, paint);
    }

    private static void DrawRightText(SKCanvas canvas, string text, float right, float y, float size, SKTypeface typeface, SKColor color)
    {
        using var paint = new SKPaint
        {
            Typeface = typeface,
            TextSize = EffectiveSize(size),
            Color = color,
            IsAntialias = true,
            SubpixelText = true
        };
        canvas.DrawText(text, right - paint.MeasureText(text), y, paint);
    }

    private static void DrawLine(SKCanvas canvas, float x1, float y1, float x2, float y2)
    {
        using var paint = new SKPaint { Color = Border, StrokeWidth = 0.8f, IsAntialias = true };
        canvas.DrawLine(x1, y1, x2, y2, paint);
    }

    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static string FormatDate(DateTime? value) => value.HasValue ? value.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "-";

    private static string FormatQuantity(decimal value) => value % 1 == 0 ? value.ToString("0", CultureInfo.InvariantCulture) : value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FormatMoney(decimal value) => string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:N0}", value);

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..(maxLength - 1)] + ".";

    private static float EffectiveSize(float size) => size + TextSizeBoost;
}
