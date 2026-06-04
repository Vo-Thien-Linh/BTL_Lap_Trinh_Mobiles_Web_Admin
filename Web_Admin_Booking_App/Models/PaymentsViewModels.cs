namespace Web_Admin_Booking_App.Models;

public enum TransactionStatus
{
    Paid = 1,
    Pending = 2,
    Failed = 3,
}

public enum PaymentMethod
{
    CreditCard = 1,
    AtmCard = 2,
    Cash = 3,
    BankTransfer = 4,
    MoMo = 5,
}

public class TransactionListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string SourceCollection { get; set; } = string.Empty;
    public string InvoiceCode { get; set; } = string.Empty;
    public string AppointmentCode { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientPhone { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal OriginalAmountVnd { get; set; }
    public decimal InsuranceSupportVnd { get; set; }
    public decimal AmountVnd { get; set; }
    public DateTime PaidAt { get; set; }
    public PaymentMethod Method { get; set; }
    public TransactionStatus Status { get; set; }
}

public class PaymentsIndexViewModel
{
    public string? Search { get; set; }
    public string? SourceFilter { get; set; }
    public string? StatusFilter { get; set; }
    public string? MethodFilter { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? FilterError { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalRevenueChangePercent { get; set; }
    public int SuccessfulToday { get; set; }
    public int PendingCount { get; set; }
    public int PendingNeedsApprovalCount { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;

    public IReadOnlyList<TransactionListItemViewModel> Transactions { get; set; } = Array.Empty<TransactionListItemViewModel>();
}

public class PaymentReceiptViewModel
{
    public string Id { get; set; } = string.Empty;
    public string SourceCollection { get; set; } = string.Empty;
    public string InvoiceCode { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public DateTime? PatientDob { get; set; }
    public string PatientGender { get; set; } = string.Empty;
    public string PatientAddress { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public DateTime PaidAt { get; set; }
    public PaymentMethod Method { get; set; }
    public TransactionStatus Status { get; set; }
    public decimal TotalAmountVnd { get; set; }
    public decimal DiscountVnd { get; set; }
    public decimal AmountDueVnd { get; set; }
    public string AmountInWords { get; set; } = string.Empty;
    public IReadOnlyList<PaymentReceiptLineItemViewModel> Items { get; set; } = Array.Empty<PaymentReceiptLineItemViewModel>();
}

public class PaymentReceiptLineItemViewModel
{
    public int Index { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitPriceVnd { get; set; }
    public decimal AmountVnd { get; set; }
}
