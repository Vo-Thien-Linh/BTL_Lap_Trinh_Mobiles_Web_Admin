using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;

namespace Web_Admin_Booking_App.Controllers;

public class PaymentsController : Controller
{
    public IActionResult Index()
    {
        var model = new PaymentsIndexViewModel
        {
            TotalRevenue = 1248500000m,
            TotalRevenueChangePercent = 12.5m,
            SuccessfulToday = 142,
            PendingCount = 28,
            PendingNeedsApprovalCount = 8,
            PeriodLabel = "7 ngày qua",
            Transactions = new List<TransactionListItemViewModel>
            {
                new()
                {
                    Id = 1,
                    InvoiceCode = "INV-2023-001",
                    PatientName = "Nguyễn Thanh Tùng",
                    ServiceName = "Khám tổng quát",
                    AmountVnd = 500000,
                    PaidAt = new DateTime(2023,10,24,8,30,0),
                    Method = PaymentMethod.CreditCard,
                    Status = TransactionStatus.Paid
                },
                new()
                {
                    Id = 2,
                    InvoiceCode = "INV-2023-002",
                    PatientName = "Lê Hồng Hạnh",
                    ServiceName = "Xét nghiệm máu",
                    AmountVnd = 1200000,
                    PaidAt = new DateTime(2023,10,24,9,15,0),
                    Method = PaymentMethod.MoMo,
                    Status = TransactionStatus.Pending
                },
                new()
                {
                    Id = 3,
                    InvoiceCode = "INV-2023-003",
                    PatientName = "Phạm Quốc Cường",
                    ServiceName = "Siêu âm bụng",
                    AmountVnd = 850000,
                    PaidAt = new DateTime(2023,10,23,15,45,0),
                    Method = PaymentMethod.Cash,
                    Status = TransactionStatus.Paid
                },
                new()
                {
                    Id = 4,
                    InvoiceCode = "INV-2023-004",
                    PatientName = "Trần Đức Lộc",
                    ServiceName = "Chụp X-Quang",
                    AmountVnd = 450000,
                    PaidAt = new DateTime(2023,10,23,10,20,0),
                    Method = PaymentMethod.AtmCard,
                    Status = TransactionStatus.Failed
                },
                new()
                {
                    Id = 5,
                    InvoiceCode = "INV-2023-005",
                    PatientName = "Mai Bích Thùy",
                    ServiceName = "Khám nội soi",
                    AmountVnd = 2100000,
                    PaidAt = new DateTime(2023,10,22,14,0,0),
                    Method = PaymentMethod.BankTransfer,
                    Status = TransactionStatus.Paid
                },
            }
        };

        return View(model);
    }
}
