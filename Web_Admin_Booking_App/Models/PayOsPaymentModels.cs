using System.Text.Json.Serialization;

namespace Web_Admin_Booking_App.Models;

public sealed class PaymentRecord
{
    public string Id { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
    public string? AppointmentId { get; set; }
    public string? PatientId { get; set; }
    public string? UserId { get; set; }
    public string? PatientUid { get; set; }
    public decimal Amount { get; set; }
    public string PaymentCode { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string? GatewayProvider { get; set; }
    public long? GatewayOrderCode { get; set; }
    public string? CheckoutUrl { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? PaidAt { get; set; }

    public bool BelongsToPatient(string uid)
    {
        return Matches(PatientId, uid) || Matches(UserId, uid) || Matches(PatientUid, uid);
    }

    private static bool Matches(string? value, string uid)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               string.Equals(value.Trim(), uid, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class PayOsCreateLinkResponse
{
    public string CheckoutUrl { get; set; } = string.Empty;
    public string PaymentId { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = "payos_pending";
    public long GatewayOrderCode { get; set; }
}

public sealed class PayOsWebhookRequest
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("desc")]
    public string? Desc { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public PayOsWebhookData? Data { get; set; }

    [JsonPropertyName("signature")]
    public string? Signature { get; set; }
}

public sealed class PayOsWebhookData
{
    [JsonPropertyName("orderCode")]
    public long? OrderCode { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; set; }

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("transactionDateTime")]
    public string? TransactionDateTime { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("paymentLinkId")]
    public string? PaymentLinkId { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("desc")]
    public string? Desc { get; set; }

    [JsonPropertyName("counterAccountBankId")]
    public string? CounterAccountBankId { get; set; }

    [JsonPropertyName("counterAccountBankName")]
    public string? CounterAccountBankName { get; set; }

    [JsonPropertyName("counterAccountName")]
    public string? CounterAccountName { get; set; }

    [JsonPropertyName("counterAccountNumber")]
    public string? CounterAccountNumber { get; set; }

    [JsonPropertyName("virtualAccountName")]
    public string? VirtualAccountName { get; set; }

    [JsonPropertyName("virtualAccountNumber")]
    public string? VirtualAccountNumber { get; set; }
}
