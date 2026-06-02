using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

public class PaymentsController : Controller
{
    private readonly FirestoreAdminDataService _dataService;

    public PaymentsController(FirestoreAdminDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<IActionResult> Index(
        string? search,
        string? sourceFilter,
        string? statusFilter,
        string? methodFilter,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        var filterError = ValidateFilters(search, sourceFilter, statusFilter, methodFilter, fromDate, toDate);
        var vm = await _dataService.GetPaymentsAsync(search, sourceFilter, statusFilter, methodFilter, fromDate, toDate, cancellationToken);
        if (filterError != null)
        {
            vm.FilterError = filterError;
            vm.Transactions = Array.Empty<TransactionListItemViewModel>();
        }
        return View(vm);
    }

    public async Task<IActionResult> ReceiptPdf(
        string id,
        string? sourceCollection,
        CancellationToken cancellationToken)
    {
        var receipt = await _dataService.GetPaymentReceiptAsync(id, sourceCollection, cancellationToken);
        if (receipt is null)
        {
            return NotFound();
        }

        var pdf = ReceiptPdfRenderer.Render(receipt);
        var fileName = $"phieu-thu-{SafeFileName(receipt.InvoiceCode)}.pdf";
        Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
        return File(pdf, "application/pdf");
    }

    private static string? ValidateFilters(
        string? search,
        string? sourceFilter,
        string? statusFilter,
        string? methodFilter,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        if (!string.IsNullOrWhiteSpace(search) && search.Trim().Length > 100)
        {
            return "Từ khóa tìm kiếm không được vượt quá 100 ký tự.";
        }

        var allowedSources = new[] { "Payments", "Invoices" };
        if (!string.IsNullOrWhiteSpace(sourceFilter) &&
            !allowedSources.Contains(sourceFilter, StringComparer.OrdinalIgnoreCase))
        {
            return "Nguồn dữ liệu thanh toán không hợp lệ.";
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) && !Enum.TryParse<TransactionStatus>(statusFilter, true, out _))
        {
            return "Bộ lọc trạng thái thanh toán không hợp lệ.";
        }

        if (!string.IsNullOrWhiteSpace(methodFilter) && !Enum.TryParse<PaymentMethod>(methodFilter, true, out _))
        {
            return "Phương thức thanh toán không hợp lệ.";
        }

        if (fromDate.HasValue && toDate.HasValue && toDate.Value < fromDate.Value)
        {
            return "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.";
        }

        return null;
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "hoa-don" : cleaned;
    }
}
