using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Web_Admin_Booking_App.Models;

namespace Web_Admin_Booking_App.Services;

public sealed class PayOsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly HttpClient _httpClient;
    private readonly PayOsSettings _settings;
    private readonly ILogger<PayOsService> _logger;

    public PayOsService(
        HttpClient httpClient,
        IOptions<PayOsSettings> options,
        ILogger<PayOsService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<PayOsPaymentLinkResult> CreatePaymentLinkAsync(
        PaymentRecord payment,
        long orderCode,
        CancellationToken cancellationToken = default)
    {
        ValidateSettings();

        var amount = Convert.ToInt64(decimal.Round(payment.Amount, 0, MidpointRounding.AwayFromZero));
        var description = BuildDescription(payment);
        var signatureData = $"amount={amount}&cancelUrl={_settings.CancelUrl}&description={description}&orderCode={orderCode}&returnUrl={_settings.ReturnUrl}";
        var signature = CreateSignature(signatureData);

        var request = new
        {
            orderCode,
            amount,
            description,
            cancelUrl = _settings.CancelUrl,
            returnUrl = _settings.ReturnUrl,
            signature,
            items = new[]
            {
                new
                {
                    name = description,
                    quantity = 1,
                    price = amount
                }
            }
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api-merchant.payos.vn/v2/payment-requests")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        message.Headers.Add("x-client-id", _settings.ClientId);
        message.Headers.Add("x-api-key", _settings.ApiKey);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("payOS create link failed with HTTP {StatusCode}: {Body}", response.StatusCode, responseText);
            throw new InvalidOperationException("Không thể tạo liên kết thanh toán payOS.");
        }

        var payOsResponse = JsonSerializer.Deserialize<PayOsCreatePaymentResponse>(responseText, JsonOptions);
        var responseData = payOsResponse?.Data;
        var checkoutUrl = responseData?.CheckoutUrl;
        if (!string.Equals(payOsResponse?.Code, "00", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(checkoutUrl))
        {
            _logger.LogWarning("payOS create link returned invalid payload: {Body}", responseText);
            throw new InvalidOperationException(payOsResponse?.Desc ?? "payOS không trả checkoutUrl.");
        }

        return new PayOsPaymentLinkResult(
            checkoutUrl,
            responseData?.PaymentLinkId,
            responseData?.Status);
    }

    public PayOsConfigCheck GetConfigCheck()
    {
        return new PayOsConfigCheck(
            HasClientId: !string.IsNullOrWhiteSpace(_settings.ClientId),
            HasApiKey: !string.IsNullOrWhiteSpace(_settings.ApiKey),
            HasChecksumKey: !string.IsNullOrWhiteSpace(_settings.ChecksumKey),
            HasReturnUrl: !string.IsNullOrWhiteSpace(_settings.ReturnUrl),
            HasCancelUrl: !string.IsNullOrWhiteSpace(_settings.CancelUrl),
            ReturnUrl: _settings.ReturnUrl ?? string.Empty,
            CancelUrl: _settings.CancelUrl ?? string.Empty,
            WebhookPath: "/api/payments/payos/webhook");
    }

    public bool HasCreateLinkConfiguration()
    {
        var check = GetConfigCheck();
        return check.HasClientId &&
               check.HasApiKey &&
               check.HasChecksumKey &&
               check.HasReturnUrl &&
               check.HasCancelUrl;
    }

    public bool VerifyWebhook(PayOsWebhookRequest webhook)
    {
        ValidateChecksumKey();

        if (webhook.Data is null || string.IsNullOrWhiteSpace(webhook.Signature))
        {
            return false;
        }

        var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["accountNumber"] = webhook.Data.AccountNumber ?? string.Empty,
            ["amount"] = FormatDecimal(webhook.Data.Amount),
            ["code"] = webhook.Data.Code ?? string.Empty,
            ["counterAccountBankId"] = webhook.Data.CounterAccountBankId ?? string.Empty,
            ["counterAccountBankName"] = webhook.Data.CounterAccountBankName ?? string.Empty,
            ["counterAccountName"] = webhook.Data.CounterAccountName ?? string.Empty,
            ["counterAccountNumber"] = webhook.Data.CounterAccountNumber ?? string.Empty,
            ["currency"] = webhook.Data.Currency ?? string.Empty,
            ["desc"] = webhook.Data.Desc ?? string.Empty,
            ["description"] = webhook.Data.Description ?? string.Empty,
            ["orderCode"] = webhook.Data.OrderCode?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            ["paymentLinkId"] = webhook.Data.PaymentLinkId ?? string.Empty,
            ["reference"] = webhook.Data.Reference ?? string.Empty,
            ["transactionDateTime"] = webhook.Data.TransactionDateTime ?? string.Empty,
            ["virtualAccountName"] = webhook.Data.VirtualAccountName ?? string.Empty,
            ["virtualAccountNumber"] = webhook.Data.VirtualAccountNumber ?? string.Empty
        };

        var data = string.Join("&", values.Select(x => $"{x.Key}={x.Value}"));
        var expected = CreateSignature(data);
        return string.Equals(expected, webhook.Signature, StringComparison.OrdinalIgnoreCase);
    }

    private string CreateSignature(string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_settings.ChecksumKey);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var hash = HMACSHA256.HashData(keyBytes, dataBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private void ValidateSettings()
    {
        ValidateChecksumKey();
        if (string.IsNullOrWhiteSpace(_settings.ClientId) ||
            string.IsNullOrWhiteSpace(_settings.ApiKey) ||
            string.IsNullOrWhiteSpace(_settings.ReturnUrl) ||
            string.IsNullOrWhiteSpace(_settings.CancelUrl))
        {
            throw new InvalidOperationException("Thiếu cấu hình PayOS. Hãy cấu hình ClientId, ApiKey, ReturnUrl và CancelUrl.");
        }
    }

    private void ValidateChecksumKey()
    {
        if (string.IsNullOrWhiteSpace(_settings.ChecksumKey))
        {
            throw new InvalidOperationException("Thiếu cấu hình PayOS:ChecksumKey.");
        }
    }

    private static string BuildDescription(PaymentRecord payment)
    {
        var code = FirstNonEmpty(
            payment.PaymentCode,
            payment.InvoiceCode,
            payment.AppointmentCode,
            payment.Id);
        var cleaned = new string(code.Where(char.IsLetterOrDigit).Take(18).ToArray());
        return $"PAY {cleaned}".Trim();
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "PAYMENT";
    }

    private static string FormatDecimal(decimal? value)
    {
        if (!value.HasValue) return string.Empty;
        var normalized = decimal.Round(value.Value, 0, MidpointRounding.AwayFromZero);
        return normalized.ToString("0", CultureInfo.InvariantCulture);
    }
}

public sealed record PayOsPaymentLinkResult(
    string CheckoutUrl,
    string? PaymentLinkId,
    string? Status);

public sealed record PayOsConfigCheck(
    bool HasClientId,
    bool HasApiKey,
    bool HasChecksumKey,
    bool HasReturnUrl,
    bool HasCancelUrl,
    string ReturnUrl,
    string CancelUrl,
    string WebhookPath);

internal sealed class PayOsCreatePaymentResponse
{
    public string? Code { get; set; }
    public string? Desc { get; set; }
    public PayOsCreatePaymentData? Data { get; set; }
}

internal sealed class PayOsCreatePaymentData
{
    public string? PaymentLinkId { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? Status { get; set; }
}
