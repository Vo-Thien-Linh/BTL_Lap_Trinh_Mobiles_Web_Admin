using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;
using Web_Admin_Booking_App.Models;

namespace Web_Admin_Booking_App.Services;

public sealed class FirestoreAdminDataService
{
    private readonly FirestoreDb _firestore;
    private readonly FirebaseSettings _settings;

    public FirestoreAdminDataService(
        FirestoreDb firestore,
        IOptions<FirebaseSettings> options)
    {
        _firestore = firestore;
        _settings = options.Value;
    }

    public async Task<IReadOnlyList<PatientListItemViewModel>> GetPatientsAsync(CancellationToken cancellationToken = default)
    {
        var docs = await GetFirstAvailableCollectionAsync(_settings.UserCollections, cancellationToken);
        var result = new List<PatientListItemViewModel>();
        var index = 1;

        foreach (var doc in docs.Documents)
        {
            var role = GetString(doc, "role", "Role")?.ToLowerInvariant();
            if (role != "patient") continue;

            result.Add(new PatientListItemViewModel
            {
                Id = index++,
                FullName = GetString(doc, "fullName", "FullName", "username", "Username") ?? "Chưa có tên",
                Phone = GetString(doc, "phone", "phoneNumber", "Phone") ?? string.Empty,
                Gender = ParseGender(GetString(doc, "gender", "Gender")),
                Dob = ParseDateOnly(doc, "dateOfBirth", "dob", "Dob") ?? new DateOnly(2000, 1, 1),
                Status = ParsePatientStatus(GetString(doc, "status", "Status"))
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<DoctorListItemViewModel>> GetDoctorsAsync(CancellationToken cancellationToken = default)
    {
        var docs = await GetFirstAvailableCollectionAsync(_settings.DoctorCollections, cancellationToken);
        var departments = await LoadDepartmentNamesAsync(cancellationToken);
        var result = new List<DoctorListItemViewModel>();
        var index = 1;

        foreach (var doc in docs.Documents)
        {
            var departmentId = GetString(doc, "departmentId", "DepartmentId");
            result.Add(new DoctorListItemViewModel
            {
                Id = index++,
                FullName = GetString(doc, "fullName", "FullName", "doctorName", "name") ?? $"Bác sĩ {index}",
                Department = departmentId != null && departments.TryGetValue(departmentId, out var deptName)
                    ? deptName
                    : GetString(doc, "department", "specialization", "Specialization") ?? string.Empty,
                Phone = GetString(doc, "phone", "phoneNumber", "Phone") ?? string.Empty,
                Status = ParseDoctorStatus(doc)
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<AppointmentListItemViewModel>> GetAppointmentsAsync(CancellationToken cancellationToken = default)
    {
        var docs = await GetFirstAvailableCollectionAsync(_settings.AppointmentCollections, cancellationToken);
        var users = await LoadUserNamesAsync(cancellationToken);
        var departments = await LoadDepartmentNamesAsync(cancellationToken);
        var doctors = await LoadDoctorNamesAsync(cancellationToken);
        var result = new List<AppointmentListItemViewModel>();
        var index = 1;

        foreach (var doc in docs.Documents.OrderByDescending(GetAppointmentDateTime))
        {
            var patientId = GetString(doc, "patientId", "PatientId");
            var doctorId = GetString(doc, "doctorId", "DoctorId");
            var departmentId = GetString(doc, "departmentId", "DepartmentId");

            result.Add(new AppointmentListItemViewModel
            {
                Id = index++,
                PatientName = patientId != null && users.TryGetValue(patientId, out var patientName)
                    ? patientName
                    : GetString(doc, "patientName", "PatientName") ?? "Bệnh nhân",
                PatientNote = GetString(doc, "symptoms", "note", "Note") ?? string.Empty,
                DoctorName = doctorId != null && doctors.TryGetValue(doctorId, out var doctorName)
                    ? doctorName
                    : GetString(doc, "doctorName", "DoctorName") ?? "Bác sĩ",
                SpecialtyName = departmentId != null && departments.TryGetValue(departmentId, out var deptName)
                    ? deptName
                    : GetString(doc, "departmentName", "SpecialtyName") ?? string.Empty,
                ScheduledAt = GetAppointmentDateTime(doc),
                Status = ParseAppointmentStatus(GetString(doc, "status", "Status"))
            });
        }

        return result;
    }

    public async Task<PaymentsIndexViewModel> GetPaymentsAsync(CancellationToken cancellationToken = default)
    {
        var docs = await GetFirstAvailableCollectionAsync(_settings.InvoiceCollections, cancellationToken);
        var transactions = new List<TransactionListItemViewModel>();
        var index = 1;

        foreach (var doc in docs.Documents)
        {
            var amount = GetDecimal(doc, "amount", "totalAmount", "AmountVnd");
            var status = ParseTransactionStatus(GetString(doc, "status", "Status"));
            var paidAt = GetDateTime(doc, "paidAt", "createdAt", "CreatedAt") ?? DateTime.Now;

            transactions.Add(new TransactionListItemViewModel
            {
                Id = index++,
                InvoiceCode = GetString(doc, "invoiceCode", "code", "id") ?? doc.Id,
                PatientName = GetString(doc, "patientName", "PatientName") ?? string.Empty,
                ServiceName = GetString(doc, "serviceName", "description") ?? "Dịch vụ khám",
                AmountVnd = amount,
                PaidAt = paidAt,
                Method = ParsePaymentMethod(GetString(doc, "method", "paymentMethod")),
                Status = status
            });
        }

        return new PaymentsIndexViewModel
        {
            TotalRevenue = transactions.Where(x => x.Status == TransactionStatus.Paid).Sum(x => x.AmountVnd),
            TotalRevenueChangePercent = 0,
            SuccessfulToday = transactions.Count(x => x.Status == TransactionStatus.Paid && x.PaidAt.Date == DateTime.Today),
            PendingCount = transactions.Count(x => x.Status == TransactionStatus.Pending),
            PendingNeedsApprovalCount = transactions.Count(x => x.Status == TransactionStatus.Pending),
            PeriodLabel = "Dữ liệu từ Firebase",
            Transactions = transactions
        };
    }

    private async Task<QuerySnapshot> GetFirstAvailableCollectionAsync(string[] collectionNames, CancellationToken cancellationToken)
    {
        foreach (var collectionName in collectionNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var snapshot = await _firestore.Collection(collectionName).Limit(500).GetSnapshotAsync(cancellationToken);
            if (snapshot.Count > 0)
            {
                return snapshot;
            }
        }

        return await _firestore.Collection(collectionNames.First()).Limit(0).GetSnapshotAsync(cancellationToken);
    }

    private async Task<Dictionary<string, string>> LoadUserNamesAsync(CancellationToken cancellationToken)
    {
        var docs = await GetFirstAvailableCollectionAsync(_settings.UserCollections, cancellationToken);
        return docs.Documents.ToDictionary(
            x => x.Id,
            x => GetString(x, "fullName", "FullName", "username", "Username", "email") ?? x.Id);
    }

    private async Task<Dictionary<string, string>> LoadDoctorNamesAsync(CancellationToken cancellationToken)
    {
        var docs = await GetFirstAvailableCollectionAsync(_settings.DoctorCollections, cancellationToken);
        return docs.Documents.ToDictionary(
            x => x.Id,
            x => GetString(x, "fullName", "FullName", "doctorName", "name") ?? x.Id);
    }

    private async Task<Dictionary<string, string>> LoadDepartmentNamesAsync(CancellationToken cancellationToken)
    {
        var docs = await GetFirstAvailableCollectionAsync(_settings.DepartmentCollections, cancellationToken);
        return docs.Documents.ToDictionary(
            x => x.Id,
            x => GetString(x, "departmentName", "name", "DepartmentName") ?? x.Id);
    }

    private static string? GetString(DocumentSnapshot snapshot, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (!snapshot.ContainsField(fieldName)) continue;
            try { return snapshot.GetValue<object?>(fieldName)?.ToString(); }
            catch { return null; }
        }
        return null;
    }

    private static DateTime GetAppointmentDateTime(DocumentSnapshot doc)
    {
        return GetDateTime(doc, "appointmentDate", "scheduledAt", "date", "CreatedAt", "createdAt") ?? DateTime.Now;
    }

    private static DateTime? GetDateTime(DocumentSnapshot snapshot, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (!snapshot.ContainsField(fieldName)) continue;

            try
            {
                var value = snapshot.GetValue<object?>(fieldName);
                return value switch
                {
                    Timestamp ts => ts.ToDateTime().ToLocalTime(),
                    DateTime dt => dt,
                    string s when DateTime.TryParse(s, out var parsed) => parsed,
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static DateOnly? ParseDateOnly(DocumentSnapshot doc, params string[] fieldNames)
    {
        var dateTime = GetDateTime(doc, fieldNames);
        return dateTime.HasValue ? DateOnly.FromDateTime(dateTime.Value) : null;
    }

    private static decimal GetDecimal(DocumentSnapshot snapshot, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (!snapshot.ContainsField(fieldName)) continue;
            try
            {
                var value = snapshot.GetValue<object?>(fieldName);
                return value switch
                {
                    decimal d => d,
                    double d => Convert.ToDecimal(d),
                    long l => l,
                    int i => i,
                    string s when decimal.TryParse(s, out var parsed) => parsed,
                    _ => 0
                };
            }
            catch { return 0; }
        }

        return 0;
    }

    private static Gender ParseGender(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "male" or "nam" => Gender.Male,
            "female" or "nu" or "nữ" => Gender.Female,
            _ => Gender.Unknown
        };
    }

    private static PatientStatus ParsePatientStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "inactive" or "blocked" or "disabled" => PatientStatus.Inactive,
            _ => PatientStatus.Active
        };
    }

    private static string ParseDoctorStatus(DocumentSnapshot doc)
    {
        if (doc.ContainsField("isActive"))
        {
            try { return doc.GetValue<bool>("isActive") ? "Đang hoạt động" : "Tạm nghỉ"; }
            catch { return "Đang hoạt động"; }
        }

        return GetString(doc, "status", "Status") ?? "Đang hoạt động";
    }

    private static AppointmentStatus ParseAppointmentStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "confirmed" => AppointmentStatus.Confirmed,
            "upcoming" => AppointmentStatus.Upcoming,
            "inprogress" or "in_progress" or "examining" => AppointmentStatus.InProgress,
            "completed" or "done" => AppointmentStatus.Completed,
            "cancelled" or "canceled" => AppointmentStatus.Cancelled,
            _ => AppointmentStatus.Pending
        };
    }

    private static TransactionStatus ParseTransactionStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "paid" or "success" or "successful" => TransactionStatus.Paid,
            "failed" or "cancelled" => TransactionStatus.Failed,
            _ => TransactionStatus.Pending
        };
    }

    private static PaymentMethod ParsePaymentMethod(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "cash" => PaymentMethod.Cash,
            "bank" or "banktransfer" or "bank_transfer" => PaymentMethod.BankTransfer,
            "momo" => PaymentMethod.MoMo,
            "atm" or "atmcard" => PaymentMethod.AtmCard,
            _ => PaymentMethod.CreditCard
        };
    }
}
