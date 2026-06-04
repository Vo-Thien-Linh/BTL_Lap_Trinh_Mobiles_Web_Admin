using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/payments")]
public sealed class PaymentsApiController : ControllerBase
{
    private readonly FirebaseAuthRestService _firebaseAuth;
    private readonly FirestoreAdminDataService _dataService;
    private readonly PayOsService _payOsService;
    private readonly ILogger<PaymentsApiController> _logger;

    public PaymentsApiController(
        FirebaseAuthRestService firebaseAuth,
        FirestoreAdminDataService dataService,
        PayOsService payOsService,
        ILogger<PaymentsApiController> logger)
    {
        _firebaseAuth = firebaseAuth;
        _dataService = dataService;
        _payOsService = payOsService;
        _logger = logger;
    }

    [HttpPost("{paymentId}/payos/create-link")]
    public async Task<IActionResult> CreatePayOsLink(
        string paymentId,
        CancellationToken cancellationToken)
    {
        var idToken = GetBearerToken();
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return Unauthorized(new { message = "Thiếu Firebase ID token." });
        }

        var token = await _firebaseAuth.VerifyIdTokenAsync(idToken, cancellationToken);
        if (token is null || string.IsNullOrWhiteSpace(token.Uid))
        {
            return Unauthorized(new { message = "Firebase ID token không hợp lệ." });
        }

        var payment = await _dataService.FindPaymentRecordByIdAsync(paymentId, cancellationToken);
        if (payment is null)
        {
            return NotFound(new { message = "Không tìm thấy thanh toán." });
        }

        if (!payment.BelongsToPatient(token.Uid))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Bạn không có quyền thanh toán hóa đơn này." });
        }

        if (payment.Amount <= 0)
        {
            return BadRequest(new { message = "Số tiền thanh toán không hợp lệ." });
        }

        if (IsPaid(payment))
        {
            return Conflict(new { message = "Thanh toán này đã được ghi nhận.", paymentId = payment.Id, paymentStatus = "paid" });
        }

        if (IsPayOsPending(payment) &&
            !string.IsNullOrWhiteSpace(payment.CheckoutUrl) &&
            payment.GatewayOrderCode.HasValue)
        {
            return Ok(new PayOsCreateLinkResponse
            {
                CheckoutUrl = payment.CheckoutUrl,
                PaymentId = payment.Id,
                PaymentStatus = "payos_pending",
                GatewayOrderCode = payment.GatewayOrderCode.Value
            });
        }

        var orderCode = payment.GatewayOrderCode ?? GenerateOrderCode();

        try
        {
            var link = await _payOsService.CreatePaymentLinkAsync(payment, orderCode, cancellationToken);
            await _dataService.UpdatePaymentAsync(
                payment.CollectionName,
                payment.Id,
                new Dictionary<string, object?>
                {
                    ["paymentStatus"] = "payos_pending",
                    ["status"] = "payos_pending",
                    ["paymentMethod"] = "payos",
                    ["method"] = "payos",
                    ["gatewayProvider"] = "payos",
                    ["gatewayOrderCode"] = orderCode,
                    ["checkoutUrl"] = link.CheckoutUrl,
                    ["gatewayPaymentLinkId"] = link.PaymentLinkId,
                    ["updatedAt"] = FieldValue.ServerTimestamp
                },
                cancellationToken);

            return Ok(new PayOsCreateLinkResponse
            {
                CheckoutUrl = link.CheckoutUrl,
                PaymentId = payment.Id,
                PaymentStatus = "payos_pending",
                GatewayOrderCode = orderCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot create payOS link for payment {PaymentId}", payment.Id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Không thể tạo liên kết thanh toán. Vui lòng thử lại sau." });
        }
    }

    [HttpPost("payos/webhook")]
    public async Task<IActionResult> PayOsWebhook(
        [FromBody] PayOsWebhookRequest webhook,
        CancellationToken cancellationToken)
    {
        if (!_payOsService.VerifyWebhook(webhook))
        {
            return BadRequest(new { message = "Webhook payOS không hợp lệ." });
        }

        var data = webhook.Data;
        if (data?.OrderCode is null)
        {
            return BadRequest(new { message = "Webhook payOS thiếu orderCode." });
        }

        var payment = await _dataService.FindPaymentRecordByGatewayOrderCodeAsync(data.OrderCode.Value, cancellationToken);
        if (payment is null)
        {
            _logger.LogWarning(
                "payOS webhook payment not found for orderCode {OrderCode}. Webhook ignored.",
                data.OrderCode.Value);

            return Ok(new { code = "00", message = "Webhook received. Payment not found, ignored." });
        }

        if (data.Amount.HasValue && !AmountsMatch(payment.Amount, data.Amount.Value))
        {
            _logger.LogWarning(
                "payOS webhook amount mismatch for payment {PaymentId}. Firestore={ExpectedAmount}, Webhook={ActualAmount}",
                payment.Id,
                payment.Amount,
                data.Amount.Value);

            return Ok(new { message = "Webhook hợp lệ nhưng số tiền không khớp, chưa cập nhật trạng thái paid." });
        }

        if (IsPaid(payment))
        {
            return Ok(new { message = "Payment already paid." });
        }

        var nextStatus = ResolveWebhookStatus(webhook);
        var updates = new Dictionary<string, object?>
        {
            ["paymentStatus"] = nextStatus,
            ["status"] = nextStatus,
            ["paymentMethod"] = "payos",
            ["method"] = "payos",
            ["gatewayProvider"] = "payos",
            ["gatewayOrderCode"] = data.OrderCode.Value,
            ["gatewayTransactionId"] = data.Reference,
            ["gatewayPaymentLinkId"] = data.PaymentLinkId,
            ["updatedAt"] = FieldValue.ServerTimestamp
        };

        if (nextStatus == "paid")
        {
            updates["paidAt"] = FieldValue.ServerTimestamp;
        }

        await _dataService.UpdatePaymentAsync(payment.CollectionName, payment.Id, updates, cancellationToken);
        if (nextStatus == "paid")
        {
            await _dataService.CreatePaymentSuccessNotificationAsync(payment, cancellationToken);
        }
        return Ok(new { message = "OK" });
    }

    [HttpGet("payos/return")]
    public IActionResult PayOsReturn()
    {
        return Content("Đã quay lại từ payOS. Trạng thái thanh toán sẽ được xác nhận bằng webhook.", "text/plain; charset=utf-8");
    }

    [HttpGet("payos/cancel")]
    public IActionResult PayOsCancel()
    {
        return Content("Thanh toán payOS đã bị hủy hoặc chưa hoàn tất. Vui lòng quay lại ứng dụng để kiểm tra.", "text/plain; charset=utf-8");
    }

    private string? GetBearerToken()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authorization["Bearer ".Length..].Trim();
    }

    private static bool IsPaid(PaymentRecord payment)
    {
        return IsStatus(payment.PaymentStatus, "paid") || IsStatus(payment.Status, "paid");
    }

    private static bool IsPayOsPending(PaymentRecord payment)
    {
        return IsStatus(payment.PaymentStatus, "payos_pending") || IsStatus(payment.Status, "payos_pending");
    }

    private static bool IsStatus(string? value, string status)
    {
        return string.Equals(value?.Trim(), status, StringComparison.OrdinalIgnoreCase);
    }

    private static long GenerateOrderCode()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1_000_000_000_000;
        var random = Random.Shared.Next(10, 99);
        return timestamp * 100 + random;
    }

    private static bool AmountsMatch(decimal expected, decimal actual)
    {
        return Math.Abs(decimal.Round(expected, 0) - decimal.Round(actual, 0)) <= 1;
    }

    private static string ResolveWebhookStatus(PayOsWebhookRequest webhook)
    {
        var code = webhook.Data?.Code ?? webhook.Code;
        var desc = $"{webhook.Data?.Desc} {webhook.Desc}".ToLowerInvariant();

        if (webhook.Success && string.Equals(code, "00", StringComparison.OrdinalIgnoreCase))
        {
            return "paid";
        }

        if (desc.Contains("cancel") || desc.Contains("hủy") || desc.Contains("huy"))
        {
            return "cancelled";
        }

        if (desc.Contains("expire") || desc.Contains("hết hạn") || desc.Contains("het han"))
        {
            return "expired";
        }

        return "failed";
    }
}
