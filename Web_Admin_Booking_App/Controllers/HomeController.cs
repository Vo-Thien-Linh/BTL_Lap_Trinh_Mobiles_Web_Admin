using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Grpc.Core;
using System.Diagnostics;
using Web_Admin_Booking_App.Models;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers
{
    public class HomeController : Controller
    {
        private readonly FirestoreAdminDataService _dataService;
        private readonly PatientProfileResolver _patientResolver;
        private readonly IMemoryCache _cache;

        public HomeController(
            FirestoreAdminDataService dataService,
            PatientProfileResolver patientResolver,
            IMemoryCache cache)
        {
            _dataService = dataService;
            _patientResolver = patientResolver;
            _cache = cache;
        }

        public async Task<IActionResult> Index(string range = "this-week", CancellationToken cancellationToken = default)
        {
            var cacheKey = $"dashboard:{range}";
            try
            {
                var model = await _cache.GetOrCreateAsync(cacheKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
                    return await _dataService.GetDashboardAsync(range, cancellationToken);
                });

                return View(model ?? new DashboardViewModel());
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.ResourceExhausted)
            {
                TempData["ErrorMessage"] = "Firestore đang vượt quota đọc dữ liệu. Vui lòng thử lại sau.";
                return View(new DashboardViewModel());
            }
        }

        [Authorize(Policy = "StaffOnly")]
        public async Task<IActionResult> StaffDashboard(CancellationToken cancellationToken = default)
        {
            try
            {
                var appointments = await _dataService.GetAppointmentsAsync(cancellationToken);
                var patients = await _patientResolver.GetPatientsAsync(cancellationToken);
                var payments = await _dataService.GetPaymentsAsync(cancellationToken: cancellationToken);
                var today = DateTime.Today;

                var model = new StaffDashboardViewModel
                {
                    TodayAppointmentCount = appointments.Count(x => x.ScheduledAt.Date == today),
                    WaitingCheckInCount = appointments.Count(x => x.ScheduledAt.Date == today && x.Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed or AppointmentStatus.Upcoming),
                    CancelRequestCount = appointments.Count(x => x.Status == AppointmentStatus.CancelRequested),
                    PendingInsuranceCount = patients.Count(x => string.Equals(x.HealthInsuranceStatus, "pending", StringComparison.OrdinalIgnoreCase)),
                    CashPaymentCount = payments.Transactions.Count(x => x.Status == TransactionStatus.Pending && x.Method == PaymentMethod.Cash),
                    UnpaidInvoiceCount = payments.Transactions.Count(x => x.Status == TransactionStatus.Pending),
                    TodayAppointments = appointments
                        .Where(x => x.ScheduledAt.Date == today)
                        .OrderBy(x => x.ScheduledAt)
                        .Take(8)
                        .Select(x => new DashboardAppointmentViewModel
                        {
                            Id = x.Id,
                            PatientName = x.PatientName,
                            DoctorName = x.DoctorName,
                            ScheduledAt = x.ScheduledAt,
                            Status = x.Status
                        })
                        .ToList()
                };

                return View(model);
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.ResourceExhausted)
            {
                TempData["ErrorMessage"] = "Firestore đang vượt quota đọc dữ liệu. Vui lòng thử lại sau.";
                return View(new StaffDashboardViewModel());
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [AllowAnonymous]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
