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
    public string InvoiceCode { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal AmountVnd { get; set; }
    public DateTime PaidAt { get; set; }
    public PaymentMethod Method { get; set; }
    public TransactionStatus Status { get; set; }
}

public class PaymentsIndexViewModel
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalRevenueChangePercent { get; set; }
    public int SuccessfulToday { get; set; }
    public int PendingCount { get; set; }
    public int PendingNeedsApprovalCount { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;

    public IReadOnlyList<TransactionListItemViewModel> Transactions { get; set; } = Array.Empty<TransactionListItemViewModel>();
}
