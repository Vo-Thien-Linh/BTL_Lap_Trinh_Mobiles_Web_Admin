using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;
using System.Globalization;
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
        var addedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 1;

        foreach (var doc in docs.Documents)
        {
            var role = GetString(doc, "role", "Role")?.ToLowerInvariant();
            if (role != "patient") continue;

            result.Add(new PatientListItemViewModel
            {
                Id = doc.Id,
                FullName = GetString(doc, "fullName", "FullName", "username", "Username") ?? "Chưa có tên",
                Phone = GetString(doc, "phone", "phoneNumber", "Phone") ?? string.Empty,
                Gender = ParseGender(GetString(doc, "gender", "Gender")),
                Dob = ParseDateOnly(doc, "dateOfBirth", "dob", "Dob") ?? new DateOnly(2000, 1, 1),
                Status = ParsePatientStatus(GetString(doc, "status", "Status")),
                HealthInsuranceNumber = GetString(doc, "healthInsuranceNumber", "insuranceNumber", "bhyt", "BHYT", "bhytCode", "maBHYT") ?? string.Empty,
                HealthInsuranceStatus = NormalizeInsuranceStatus(GetString(doc, "status_bhyt", "healthInsuranceStatus", "insuranceStatus", "bhytStatus")),
                CreatedAt = GetDateTime(doc, "createdAt", "CreatedAt")
            });
            addedIds.Add(GetString(doc, "uid", "Uid") ?? doc.Id);
        }

        var patientDocs = await GetFirstAvailableCollectionAsync(_settings.PatientCollections, cancellationToken);
        foreach (var doc in patientDocs.Documents)
        {
            var linkedId = GetString(doc, "uid", "Uid", "userId", "UserId", "patientId", "PatientId") ?? doc.Id;
            if (!addedIds.Add(linkedId)) continue;

            result.Add(new PatientListItemViewModel
            {
                Id = doc.Id,
                FullName = GetString(doc, "fullName", "FullName", "patientName", "name", "username", "Username") ?? "Chưa có tên",
                Phone = GetString(doc, "phone", "phoneNumber", "Phone") ?? string.Empty,
                Gender = ParseGender(GetString(doc, "gender", "Gender")),
                Dob = ParseDateOnly(doc, "dateOfBirth", "dob", "Dob", "birthDate") ?? new DateOnly(2000, 1, 1),
                Status = ParsePatientStatus(GetString(doc, "status", "Status")),
                HealthInsuranceNumber = GetString(doc, "healthInsuranceNumber", "insuranceNumber", "bhyt", "BHYT", "bhytCode", "maBHYT") ?? string.Empty,
                HealthInsuranceStatus = NormalizeInsuranceStatus(GetString(doc, "status_bhyt", "healthInsuranceStatus", "insuranceStatus", "bhytStatus")),
                CreatedAt = GetDateTime(doc, "createdAt", "CreatedAt")
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<DoctorListItemViewModel>> GetDoctorsAsync(CancellationToken cancellationToken = default)
    {
        var docs = await GetFirstAvailableCollectionAsync(_settings.DoctorCollections, cancellationToken);
        var users = await GetFirstAvailableCollectionAsync(_settings.UserCollections, cancellationToken);
        var departments = await LoadDepartmentNamesAsync(cancellationToken);
        var result = new List<DoctorListItemViewModel>();
        var index = 1;

        foreach (var doc in docs.Documents)
        {
            var user = FindLinkedDoctorUser(doc, users.Documents);
            var sources = new DocumentSnapshot?[] { doc, user };
            var departmentId = GetStringFromSources(sources, "departmentId", "DepartmentId");
            var yearsOfExperience = GetString(doc, "yearsOfExperience", "experienceYears", "experience", "workExperience");
            result.Add(new DoctorListItemViewModel
            {
                Id = doc.Id,
                DocumentId = doc.Id,
                UserId = GetString(doc, "userId", "UserId"),
                FullName = GetStringFromSources(sources, "fullName", "FullName", "doctorName", "name", "username", "Username") ?? $"Bác sĩ {index}",
                AvatarUrl = GetStringFromSources(sources, "avatarUrl", "AvatarUrl", "photoUrl", "profileImage", "imageUrl"),
                Department = departmentId != null && departments.TryGetValue(departmentId, out var deptName)
                    ? deptName
                    : GetStringFromSources(sources, "department", "departmentName", "specialization", "Specialization") ?? string.Empty,
                DepartmentId = departmentId,
                Specialization = GetString(doc, "specialization", "Specialization"),
                LicenseNumber = GetString(doc, "licenseNumber", "medicalLicense", "license", "certificateNumber"),
                Degree = GetString(doc, "degree", "Degree", "academicDegree"),
                YearsOfExperience = FormatExperience(yearsOfExperience),
                ConsultationFee = FormatConsultationFee(GetString(doc, "consultationFee", "fee", "price", "examinationFee")),
                VerificationStatus = FormatVerificationStatus(GetString(doc, "verificationStatus", "VerificationStatus")),
                IsActive = GetBool(doc, "isActive", "IsActive"),
                Phone = GetStringFromSources(sources, "phone", "phoneNumber", "phone_number", "mobile", "mobileNumber", "contactPhone", "sdt", "soDienThoai", "Phone") ?? string.Empty,
                Email = GetStringFromSources(sources, "email", "Email"),
                UserStatus = FormatUserStatus(GetStringFromSources(new DocumentSnapshot?[] { user }, "status", "Status")),
                EmailVerified = user is null ? null : GetBool(user, "emailVerified", "EmailVerified")
            });
        }

        return result;
    }

    public async Task<DoctorDetailsViewModel?> GetDoctorDetailsAsync(string doctorDocumentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(doctorDocumentId)) return null;

        var snapshot = await GetFirstExistingDocumentAsync(_settings.DoctorCollections, doctorDocumentId, cancellationToken);
        if (snapshot is null || !snapshot.Exists) return null;

        var users = await GetFirstAvailableCollectionAsync(_settings.UserCollections, cancellationToken);
        var user = FindLinkedDoctorUser(snapshot, users.Documents);
        var sources = new DocumentSnapshot?[] { snapshot, user };
        var departments = await LoadDepartmentNamesAsync(cancellationToken);
        var departmentId = GetStringFromSources(sources, "departmentId", "DepartmentId");
        var yearsOfExperience = GetString(snapshot, "yearsOfExperience", "experienceYears", "experience", "workExperience");

        var doctor = new DoctorDetailsViewModel
        {
            DocumentId = snapshot.Id,
            UserId = GetString(snapshot, "userId", "UserId"),
            FullName = GetStringFromSources(sources, "fullName", "FullName", "doctorName", "name", "username", "Username") ?? "Chưa có tên",
            AvatarUrl = GetStringFromSources(sources, "avatarUrl", "AvatarUrl", "photoUrl", "profileImage", "imageUrl"),
            Department = departmentId != null && departments.TryGetValue(departmentId, out var deptName)
                ? deptName
                : GetStringFromSources(sources, "department", "departmentName", "specialization", "Specialization") ?? string.Empty,
            DepartmentId = departmentId,
            Specialization = GetString(snapshot, "specialization", "Specialization"),
            Phone = GetStringFromSources(sources, "phone", "phoneNumber", "phone_number", "mobile", "mobileNumber", "contactPhone", "sdt", "soDienThoai", "Phone") ?? string.Empty,
            Email = GetStringFromSources(sources, "email", "Email"),
            Gender = GetStringFromSources(sources, "gender", "Gender", "sex"),
            DateOfBirth = GetDateTimeFromSources(sources, "dateOfBirth", "dob", "birthDate", "birthday"),
            Role = GetStringFromSources(new DocumentSnapshot?[] { user }, "role", "Role"),
            UserStatus = FormatUserStatus(GetStringFromSources(new DocumentSnapshot?[] { user }, "status", "Status")),
            EmailVerified = user is null ? null : GetBool(user, "emailVerified", "EmailVerified"),
            LicenseNumber = GetStringFromSources(sources, "licenseNumber", "medicalLicense", "license", "certificateNumber"),
            Degree = GetStringFromSources(sources, "degree", "Degree", "academicDegree", "qualification", "qualifications", "education"),
            YearsOfExperience = FormatExperience(yearsOfExperience),
            ConsultationFee = FormatConsultationFee(GetString(snapshot, "consultationFee", "fee", "price", "examinationFee")),
            Biography = GetStringFromSources(sources, "bio", "biography", "description", "about", "introduction"),
            IsActive = GetBool(snapshot, "isActive", "IsActive"),
            VerificationStatus = FormatVerificationStatus(GetString(snapshot, "verificationStatus", "VerificationStatus")),
            CreatedAt = GetDateTimeFromSources(sources, "createdAt", "CreatedAt"),
            UpdatedAt = GetDateTimeFromSources(sources, "updatedAt", "UpdatedAt", "modifiedAt")
        };

        doctor.ProfileFields = BuildDoctorProfileFields(doctor);
        doctor.ContactFields = BuildDoctorContactFields(doctor);
        doctor.ProfessionalFields = BuildDoctorProfessionalFields(doctor);
        doctor.AdminFields = BuildDoctorAdminFields(doctor);

        return doctor;
    }

    public async Task<IReadOnlyList<AppointmentListItemViewModel>> GetAppointmentsAsync(CancellationToken cancellationToken = default)
    {
        var docs = await GetFirstAvailableCollectionAsync(_settings.AppointmentCollections, cancellationToken);
        var users = await LoadAppointmentUsersAsync(cancellationToken);
        var departments = await LoadDepartmentNamesAsync(cancellationToken);
        var doctors = await LoadAppointmentDoctorsAsync(cancellationToken);
        var result = new List<AppointmentListItemViewModel>();
        var index = 1;

        foreach (var doc in docs.Documents.OrderByDescending(GetAppointmentDateTime))
        {
            var patientId = GetString(doc, "patientId", "PatientId", "userId", "UserId", "customerId");
            var doctorId = GetString(doc, "doctorId", "DoctorId");
            var doctorUserId = GetString(doc, "doctorUserId", "DoctorUserId");
            var departmentId = GetString(doc, "departmentId", "DepartmentId", "specialtyId", "SpecialtyId");
            var patient = patientId != null && users.TryGetValue(patientId, out var foundPatient)
                ? foundPatient
                : null;
            var doctor = ResolveAppointmentDoctor(doctors, doctorId, doctorUserId);
            var fee = GetString(doc, "consultationFee", "fee", "price", "amount", "totalAmount");

            result.Add(new AppointmentListItemViewModel
            {
                Id = doc.Id,
                DocumentId = doc.Id,
                AppointmentCode = GetString(doc, "appointmentCode", "code", "bookingCode", "BookingCode") ?? doc.Id,
                PatientId = patientId,
                PatientPhone = patient?.Phone
                    ?? GetString(doc, "patientPhone", "phone", "phoneNumber", "PatientPhone") ?? string.Empty,
                PatientEmail = patient?.Email
                    ?? GetString(doc, "patientEmail", "email", "PatientEmail") ?? string.Empty,
                DoctorId = doctorId,
                DoctorPhone = doctor?.Phone ?? string.Empty,
                DoctorEmail = doctor?.Email ?? string.Empty,
                DepartmentId = departmentId ?? string.Empty,
                RoomName = GetString(doc, "roomName", "room", "clinicRoom", "location") ?? string.Empty,
                AppointmentType = FormatAppointmentType(GetString(doc, "type", "appointmentType", "consultationType")),
                Duration = FormatDuration(GetString(doc, "duration", "durationMinutes", "slotDuration")),
                ConsultationFee = FormatConsultationFee(fee) ?? string.Empty,
                PaymentStatus = FormatPaymentStatus(GetString(doc, "paymentStatus", "payment_state", "PaymentStatus")),
                CancelReason = GetString(doc, "cancelReason", "cancellationReason", "cancelledReason") ?? string.Empty,
                CreatedAt = GetDateTime(doc, "createdAt", "CreatedAt"),
                UpdatedAt = GetDateTime(doc, "updatedAt", "UpdatedAt", "modifiedAt"),
                PatientName = patient?.FullName
                    ?? GetString(doc, "patientName", "PatientName", "customerName") ?? "Bệnh nhân",
                PatientNote = GetString(doc, "reason", "symptoms", "note", "Note", "description") ?? string.Empty,
                DoctorName = doctor?.FullName
                    ?? GetString(doc, "doctorName", "DoctorName") ?? "Bác sĩ",
                SpecialtyName = departmentId != null && departments.TryGetValue(departmentId, out var deptName)
                    ? deptName
                    : GetString(doc, "departmentName", "SpecialtyName", "specialtyName", "department") ?? doctor?.Specialization ?? string.Empty,
                ScheduledAt = GetAppointmentDateTime(doc),
                Status = ParseAppointmentStatus(GetString(doc, "status", "Status"))
            });
        }

        return result;
    }

    public async Task<DashboardViewModel> GetDashboardAsync(string revenueRange = "this-week", CancellationToken cancellationToken = default)
    {
        var patients = await GetPatientsAsync(cancellationToken);
        var appointments = await GetAppointmentsAsync(cancellationToken);
        var payments = await GetPaymentsAsync(cancellationToken: cancellationToken);
        var doctors = await GetDoctorsAsync(cancellationToken);
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var previousMonthStart = monthStart.AddMonths(-1);
        var currentMonthPatients = patients.Count(x => x.CreatedAt.HasValue && x.CreatedAt.Value >= monthStart);
        var previousMonthPatients = patients.Count(x => x.CreatedAt.HasValue && x.CreatedAt.Value >= previousMonthStart && x.CreatedAt.Value < monthStart);

        return new DashboardViewModel
        {
            PatientCount = patients.Count,
            PatientGrowthPercent = previousMonthPatients == 0
                ? (currentMonthPatients > 0 ? 100 : 0)
                : Math.Round((currentMonthPatients - previousMonthPatients) * 100m / previousMonthPatients, 1),
            TodayAppointmentCount = appointments.Count(x => x.ScheduledAt.Date == today),
            WaitingAppointmentCount = appointments.Count(x => x.ScheduledAt.Date == today && x.Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed or AppointmentStatus.Upcoming),
            MonthlyRevenue = payments.Transactions.Where(x => x.Status == TransactionStatus.Paid && x.PaidAt >= monthStart).Sum(x => x.AmountVnd),
            OnDutyDoctorCount = doctors.Count(x => string.Equals(x.UserStatus, "Đang hoạt động", StringComparison.OrdinalIgnoreCase) || x.IsActive == true),
            TotalDoctorCount = doctors.Count,
            ThisWeekRevenue = BuildWeeklyRevenue(payments.Transactions, StartOfWeek(today)),
            LastWeekRevenue = BuildWeeklyRevenue(payments.Transactions, StartOfWeek(today).AddDays(-7)),
            RecentAppointments = appointments
                .OrderByDescending(x => x.ScheduledAt)
                .Take(5)
                .Select(x => new DashboardAppointmentViewModel
                {
                    Id = x.DocumentId,
                    PatientName = x.PatientName,
                    DoctorName = x.DoctorName,
                    ScheduledAt = x.ScheduledAt,
                    Status = x.Status
                })
                .ToList(),
            Reminders = BuildDashboardReminders(patients, appointments, payments)
        };
    }

    public async Task<AppointmentCreateViewModel> BuildAppointmentCreateModelAsync(AppointmentCreateViewModel? model = null, CancellationToken cancellationToken = default)
    {
        model ??= new AppointmentCreateViewModel();
        var departments = await GetFirstAvailableCollectionAsync(_settings.DepartmentCollections, cancellationToken);
        var doctors = await GetDoctorsAsync(cancellationToken);

        model.Specialties = departments.Documents
            .Select(x => new SelectOption
            {
                Value = x.Id,
                Text = GetString(x, "name", "Name", "departmentName", "specialtyName") ?? x.Id
            })
            .OrderBy(x => x.Text)
            .ToList();

        model.Doctors = doctors
            .Where(x => string.IsNullOrWhiteSpace(model.SpecialtyId) || string.Equals(x.DepartmentId, model.SpecialtyId, StringComparison.OrdinalIgnoreCase))
            .Select(x => new SelectOption
            {
                Value = x.DocumentId,
                Text = string.IsNullOrWhiteSpace(x.Department) ? x.FullName : $"{x.FullName} - {x.Department}"
            })
            .OrderBy(x => x.Text)
            .ToList();

        return model;
    }

    public async Task<IReadOnlyList<SelectOption>> GetDoctorOptionsByDepartmentAsync(string departmentId, CancellationToken cancellationToken = default)
    {
        var doctors = await GetDoctorsAsync(cancellationToken);
        return doctors
            .Where(x => string.IsNullOrWhiteSpace(departmentId) || string.Equals(x.DepartmentId, departmentId, StringComparison.OrdinalIgnoreCase))
            .Select(x => new SelectOption { Value = x.DocumentId, Text = x.FullName })
            .OrderBy(x => x.Text)
            .ToList();
    }

    public async Task CreateAppointmentAsync(AppointmentCreateViewModel model, CancellationToken cancellationToken = default)
    {
        var doctors = await GetDoctorsAsync(cancellationToken);
        var departments = await LoadDepartmentNamesAsync(cancellationToken);
        var doctor = doctors.FirstOrDefault(x => string.Equals(x.DocumentId, model.DoctorId, StringComparison.OrdinalIgnoreCase));
        var scheduledAt = model.Date.ToDateTime(model.Time);
        var collectionName = _settings.AppointmentCollections.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "Appointments";
        var payload = new Dictionary<string, object?>
        {
            ["patientName"] = model.PatientName.Trim(),
            ["patientPhone"] = model.PatientPhone?.Trim(),
            ["doctorId"] = model.DoctorId,
            ["doctorName"] = doctor?.FullName ?? model.DoctorId,
            ["departmentId"] = model.SpecialtyId,
            ["departmentName"] = departments.TryGetValue(model.SpecialtyId, out var departmentName) ? departmentName : model.SpecialtyId,
            ["scheduledAt"] = Timestamp.FromDateTime(DateTime.SpecifyKind(scheduledAt, DateTimeKind.Utc)),
            ["appointmentDate"] = Timestamp.FromDateTime(DateTime.SpecifyKind(model.Date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)),
            ["appointmentTime"] = model.Time.ToString("HH:mm"),
            ["status"] = "pending",
            ["note"] = model.Note?.Trim(),
            ["createdAt"] = Timestamp.GetCurrentTimestamp(),
            ["updatedAt"] = Timestamp.GetCurrentTimestamp()
        };

        await _firestore.Collection(collectionName).AddAsync(payload, cancellationToken);
    }

    public async Task<PaymentsIndexViewModel> GetPaymentsAsync(
        string? search = null,
        string? sourceFilter = null,
        string? statusFilter = null,
        string? methodFilter = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var paymentCollections = sourceFilter?.Trim().ToLowerInvariant() switch
        {
            "payments" => _settings.PaymentCollections,
            "invoices" => _settings.InvoiceCollections,
            _ => _settings.PaymentCollections.Concat(_settings.InvoiceCollections).ToArray()
        };
        var docs = await GetFirstAvailableCollectionAsync(paymentCollections, cancellationToken);
        var transactions = new List<TransactionListItemViewModel>();
        var index = 1;

        foreach (var doc in docs.Documents)
        {
            var amount = GetDecimal(doc, "amount", "totalAmount", "AmountVnd");
            var status = ParseTransactionStatus(GetString(doc, "status", "Status"));
            var paidAt = GetDateTime(doc, "paidAt", "createdAt", "CreatedAt") ?? DateTime.Now;

            transactions.Add(new TransactionListItemViewModel
            {
                Id = doc.Id,
                SourceCollection = doc.Reference.Parent.Id,
                InvoiceCode = GetString(doc, "invoiceCode", "code", "id") ?? doc.Id,
                PatientName = GetString(doc, "patientName", "PatientName") ?? string.Empty,
                ServiceName = GetString(doc, "serviceName", "description") ?? "Dịch vụ khám",
                AmountVnd = amount,
                PaidAt = paidAt,
                Method = ParsePaymentMethod(GetString(doc, "method", "paymentMethod")),
                Status = status
            });
        }

        var filtered = transactions.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim().ToLowerInvariant();
            filtered = filtered.Where(x =>
                x.InvoiceCode.ToLowerInvariant().Contains(key) ||
                x.PatientName.ToLowerInvariant().Contains(key) ||
                x.ServiceName.ToLowerInvariant().Contains(key) ||
                x.Id.ToLowerInvariant().Contains(key));
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<TransactionStatus>(statusFilter, true, out var parsedStatus))
        {
            filtered = filtered.Where(x => x.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(methodFilter) && Enum.TryParse<PaymentMethod>(methodFilter, true, out var parsedMethod))
        {
            filtered = filtered.Where(x => x.Method == parsedMethod);
        }

        if (fromDate.HasValue)
        {
            filtered = filtered.Where(x => x.PaidAt.Date >= fromDate.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (toDate.HasValue)
        {
            filtered = filtered.Where(x => x.PaidAt.Date <= toDate.Value.ToDateTime(TimeOnly.MinValue));
        }

        return new PaymentsIndexViewModel
        {
            Search = search,
            SourceFilter = sourceFilter,
            StatusFilter = statusFilter,
            MethodFilter = methodFilter,
            FromDate = fromDate,
            ToDate = toDate,
            TotalRevenue = transactions.Where(x => x.Status == TransactionStatus.Paid).Sum(x => x.AmountVnd),
            TotalRevenueChangePercent = 0,
            SuccessfulToday = transactions.Count(x => x.Status == TransactionStatus.Paid && x.PaidAt.Date == DateTime.Today),
            PendingCount = transactions.Count(x => x.Status == TransactionStatus.Pending),
            PendingNeedsApprovalCount = transactions.Count(x => x.Status == TransactionStatus.Pending),
            PeriodLabel = "Dữ liệu từ Firebase",
            Transactions = filtered
                .OrderByDescending(x => x.PaidAt)
                .ThenByDescending(x => x.InvoiceCode)
                .ToList()
        };
    }

    public async Task<PaymentReceiptViewModel?> GetPaymentReceiptAsync(
        string id,
        string? sourceCollection = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var doc = await FindPaymentDocumentAsync(id.Trim(), sourceCollection, cancellationToken);
        if (doc is null || !doc.Exists)
        {
            return null;
        }

        var total = GetDecimal(doc, "amount", "totalAmount", "AmountVnd", "total", "grandTotal");
        var discount = GetDecimal(doc, "discount", "discountAmount", "discountVnd", "discountValue");
        var due = GetDecimal(doc, "amountDue", "payableAmount", "finalAmount", "paidAmount");
        if (due <= 0)
        {
            due = Math.Max(0, total - discount);
        }

        var items = GetReceiptItems(doc, total);
        if (total <= 0)
        {
            total = items.Sum(x => x.AmountVnd);
            due = Math.Max(0, total - discount);
        }

        return new PaymentReceiptViewModel
        {
            Id = doc.Id,
            SourceCollection = doc.Reference.Parent.Id,
            InvoiceCode = GetString(doc, "invoiceCode", "code", "id", "receiptCode") ?? doc.Id,
            PatientName = GetString(doc, "patientName", "PatientName", "customerName", "clientName") ?? "Benh nhan",
            PatientDob = GetDateTime(doc, "patientDob", "dateOfBirth", "dob", "birthDate"),
            PatientGender = GetString(doc, "gender", "patientGender", "Gender") ?? string.Empty,
            PatientAddress = GetString(doc, "address", "patientAddress", "Address") ?? string.Empty,
            DoctorName = GetString(doc, "doctorName", "DoctorName", "physicianName") ?? string.Empty,
            PaidAt = GetDateTime(doc, "paidAt", "paymentDate", "createdAt", "CreatedAt") ?? DateTime.Now,
            Method = ParsePaymentMethod(GetString(doc, "method", "paymentMethod")),
            Status = ParseTransactionStatus(GetString(doc, "status", "Status")),
            TotalAmountVnd = total,
            DiscountVnd = discount,
            AmountDueVnd = due,
            AmountInWords = ToVietnameseMoneyWords(due),
            Items = items
        };
    }

    private async Task<DocumentSnapshot?> FindPaymentDocumentAsync(
        string id,
        string? sourceCollection,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(sourceCollection))
        {
            var directDoc = await _firestore.Collection(sourceCollection.Trim()).Document(id).GetSnapshotAsync(cancellationToken);
            if (directDoc.Exists)
            {
                return directDoc;
            }
        }

        var collections = _settings.PaymentCollections
            .Concat(_settings.InvoiceCollections)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var collectionName in collections)
        {
            var snapshot = await _firestore.Collection(collectionName).Document(id).GetSnapshotAsync(cancellationToken);
            if (snapshot.Exists)
            {
                return snapshot;
            }
        }

        return null;
    }

    public async Task<MedicalRecordsIndexViewModel> GetMedicalRecordsAsync(
        string? search,
        string? statusFilter,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var records = new List<MedicalRecordListItemViewModel>();
        foreach (var collectionName in new[] { "MedicalRecords", "medicalRecords", "medical_records" })
        {
            var snapshot = await _firestore.Collection(collectionName).Limit(500).GetSnapshotAsync(cancellationToken);
            foreach (var doc in snapshot.Documents)
            {
                records.Add(new MedicalRecordListItemViewModel
                {
                    Id = doc.Id,
                    RecordCode = GetString(doc, "recordCode", "code", "medicalRecordCode") ?? $"BA-{doc.Id}",
                    PatientId = GetString(doc, "patientId", "PatientId", "userId", "UserId") ?? string.Empty,
                    PatientName = GetString(doc, "patientName", "PatientName") ?? "Chưa có tên",
                    DoctorName = GetString(doc, "doctorName", "DoctorName") ?? "Chưa phân bác sĩ",
                    SpecialtyName = GetString(doc, "specialtyName", "departmentName", "department") ?? "Chưa cập nhật",
                    CreatedAt = GetDateTime(doc, "createdAt", "CreatedAt") ?? DateTime.MinValue,
                    Status = ParseMedicalRecordStatus(GetString(doc, "status", "Status"))
                });
            }
            if (records.Count > 0) break;
        }

        var query = records.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.RecordCode.ToLowerInvariant().Contains(key) ||
                x.PatientId.ToLowerInvariant().Contains(key) ||
                x.PatientName.ToLowerInvariant().Contains(key) ||
                x.DoctorName.ToLowerInvariant().Contains(key));
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<MedicalRecordStatus>(statusFilter, true, out var parsedStatus))
        {
            query = query.Where(x => x.Status == parsedStatus);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt.Date >= fromDate.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt.Date <= toDate.Value.ToDateTime(TimeOnly.MinValue));
        }

        var filtered = query.OrderByDescending(x => x.CreatedAt).ToList();
        var safePageSize = Math.Clamp(pageSize, 5, 50);
        var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)safePageSize));
        var safePage = Math.Min(Math.Max(1, page), totalPages);

        return new MedicalRecordsIndexViewModel
        {
            Search = search,
            StatusFilter = statusFilter,
            FromDate = fromDate,
            ToDate = toDate,
            Page = safePage,
            PageSize = safePageSize,
            TotalCount = filtered.Count,
            Items = filtered.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToList()
        };
    }

    public async Task<WorkShiftsIndexViewModel> GetWorkShiftsAsync(string? mode = null, CancellationToken cancellationToken = default)
    {
        mode = string.Equals(mode, "list", StringComparison.OrdinalIgnoreCase) ? "list" : "calendar";

        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var calendar = CalendarBuilder.BuildMonth(monthStart);

        var scheduleDocs = await GetFirstAvailableCollectionAsync(_settings.DoctorScheduleCollections, cancellationToken);
        var shiftDocs = await GetFirstAvailableCollectionAsync(_settings.ShiftCollections, cancellationToken);
        var shifts = LoadShiftInfo(shiftDocs.Documents);
        var doctors = await LoadAppointmentDoctorsAsync(cancellationToken);
        var departments = await LoadDepartmentNamesAsync(cancellationToken);
        var departmentRooms = await LoadDepartmentRoomsAsync(cancellationToken);

        var monthAssignments = new List<TodayAssignmentViewModel>();
        var todayAssignments = new List<TodayAssignmentViewModel>();
        var scheduleRows = new List<DoctorScheduleRowViewModel>();
        var unassignedCount = 0;

        foreach (var doc in scheduleDocs.Documents)
        {
            var date = GetScheduleDate(doc);
            if (date is null) continue;

            var doctorId = GetString(doc, "doctorId", "DoctorId");
            var doctorUserId = GetString(doc, "doctorUserId", "DoctorUserId", "userId", "UserId");
            var doctor = ResolveAppointmentDoctor(doctors, doctorId, doctorUserId);
            if (doctor is null) unassignedCount++;

            var shiftId = GetString(doc, "shiftId", "ShiftId");
            var shift = !string.IsNullOrWhiteSpace(shiftId) && shifts.TryGetValue(shiftId, out var foundShift)
                ? foundShift
                : ShiftInfo.FromSchedule(doc);
            var shiftType = ParseShiftType(GetString(doc, "shiftType", "type", "ShiftType") ?? shift.Type);
            var departmentId = GetString(doc, "departmentId", "DepartmentId", "specialtyId", "SpecialtyId");
            var department = departmentId != null && departments.TryGetValue(departmentId, out var deptName)
                ? deptName
                : GetString(doc, "departmentName", "specialtyName", "department") ?? doctor?.Specialization ?? string.Empty;
            var room = GetString(doc, "room", "roomName", "clinicRoom", "location") ?? shift.Room;
            var status = ParseAssignmentStatus(GetString(doc, "status", "Status"));
            var isActive = GetBool(doc, "isActive", "IsActive") ?? !string.Equals(GetString(doc, "status", "Status"), "unavailable", StringComparison.OrdinalIgnoreCase);
            var shiftLabel = BuildShiftLabel(shift, shiftType);
            var doctorName = doctor?.FullName ?? GetString(doc, "doctorName", "DoctorName") ?? "Chưa phân bác sĩ";

            if (date.Value >= monthStart && date.Value <= monthEnd)
            {
                CalendarBuilder.AddShift(
                    calendar,
                    date.Value,
                    shiftType,
                    $"{ShiftPrefix(shiftType)}: {doctorName}",
                    BuildScheduleSubtitle(room, department));
            }

            var assignment = new TodayAssignmentViewModel
            {
                Initial = Initials(doctorName),
                DoctorName = doctorName,
                DoctorTitle = GetString(doc, "doctorTitle", "degree", "title"),
                Specialty = department,
                ShiftLabel = shiftLabel,
                Room = room,
                Status = status
            };

            if (date.Value >= monthStart && date.Value <= monthEnd)
            {
                monthAssignments.Add(assignment);
                scheduleRows.Add(new DoctorScheduleRowViewModel
                {
                    DocumentId = doc.Id,
                    Date = date.Value,
                    DoctorName = doctorName,
                    DepartmentName = department,
                    Room = room,
                    ShiftName = shift.Name,
                    ShiftTime = FormatShiftTime(shift),
                    AvailableSlots = GetInt(doc, "availableSlots", "AvailableSlots", "slots"),
                    IsActive = isActive,
                    Status = isActive ? "available" : "unavailable"
                });
            }

            if (date.Value == today)
            {
                todayAssignments.Add(assignment);
            }
        }

        var assignedCount = monthAssignments.Count(x => x.Status == AssignmentStatus.Assigned);
        var fillRate = monthAssignments.Count == 0
            ? 0
            : Math.Round(assignedCount * 100m / monthAssignments.Count, 1);

        return new WorkShiftsIndexViewModel
        {
            Mode = mode,
            RangeLabel = $"{monthStart:dd/MM} - {monthEnd:dd/MM/yyyy}",
            TotalDoctorsLabel = $"{doctors.Count} Bác sĩ",
            UnassignedCount = unassignedCount,
            FillRatePercent = fillRate,
            ConflictCount = CountScheduleConflicts(scheduleDocs.Documents),
            MonthLabel = $"Tháng {monthStart.Month}, {monthStart.Year}",
            Calendar = calendar,
            TodayLabel = $"Phân bổ nhân sự hôm nay ({today:dd/MM})",
            TodayAssignments = todayAssignments,
            Schedules = scheduleRows.OrderBy(x => x.Date).ThenBy(x => x.DoctorName).ThenBy(x => x.ShiftName).ToList(),
            Doctors = doctors
                .OrderBy(x => x.FullName)
                .Select(x => new SelectOption { Value = x.DocumentId, Text = x.FullName })
                .ToList(),
            Departments = departments
                .OrderBy(x => x.Value)
                .Select(x => new SelectOption { Value = x.Key, Text = x.Value })
                .ToList(),
            DepartmentRooms = departmentRooms,
            Shifts = shifts
                .OrderBy(x => x.Key)
                .Select(x => new SelectOption { Value = x.Key, Text = BuildShiftLabel(x.Value, ParseShiftType(x.Value.Type ?? x.Key)) })
                .ToList(),
            GenerateForm = new WorkScheduleGenerateViewModel
            {
                StartDate = today,
                WeeksAhead = 4,
                AvailableSlots = 10
            }
        };
    }

    public async Task<int> GenerateDoctorSchedulesAsync(WorkScheduleGenerateViewModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.DoctorId) ||
            string.IsNullOrWhiteSpace(model.DepartmentId) ||
            model.ShiftIds.Count == 0 ||
            model.DaysOfWeek.Count == 0)
        {
            return 0;
        }

        var collection = _firestore.Collection(_settings.DoctorScheduleCollections.First());
        var existingSchedules = await GetFirstAvailableCollectionAsync(_settings.DoctorScheduleCollections, cancellationToken);
        var weeksAhead = Math.Clamp(model.WeeksAhead, 1, 8);
        var availableSlots = Math.Max(model.AvailableSlots, 0);
        var startDate = model.StartDate;
        var endDate = startDate.AddDays(weeksAhead * 7 - 1);
        var createdCount = 0;

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (!model.DaysOfWeek.Contains(date.DayOfWeek)) continue;

            foreach (var shiftId in model.ShiftIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (HasDoctorScheduleConflict(existingSchedules.Documents, model.DoctorId, model.Room, date, shiftId))
                {
                    continue;
                }

                var documentId = BuildScheduleDocumentId(model.DoctorId, date, shiftId);
                var docRef = collection.Document(documentId);
                var existing = await docRef.GetSnapshotAsync(cancellationToken);
                if (existing.Exists) continue;

                var payload = new Dictionary<string, object?>
                {
                    ["doctorId"] = model.DoctorId,
                    ["departmentId"] = model.DepartmentId,
                    ["scheduleDate"] = Timestamp.FromDateTime(date.ToDateTime(TimeOnly.MinValue).ToUniversalTime()),
                    ["shiftId"] = shiftId,
                    ["room"] = model.Room.Trim(),
                    ["availableSlots"] = availableSlots,
                    ["isActive"] = true,
                    ["status"] = "available",
                    ["source"] = "template",
                    ["createdAt"] = Timestamp.GetCurrentTimestamp(),
                    ["updatedAt"] = Timestamp.GetCurrentTimestamp()
                };

                await docRef.SetAsync(payload, cancellationToken: cancellationToken);
                createdCount++;
            }
        }

        return createdCount;
    }

    public async Task UpdateDoctorScheduleAsync(string documentId, bool isActive, int availableSlots, string? room, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId)) return;

        var doc = _firestore.Collection(_settings.DoctorScheduleCollections.First()).Document(documentId);
        var snapshot = await doc.GetSnapshotAsync(cancellationToken);
        if (!snapshot.Exists) return;

        var date = GetScheduleDate(snapshot);
        var shiftId = GetString(snapshot, "shiftId", "ShiftId", "shiftType", "type", "ShiftType");
        var doctorId = GetString(snapshot, "doctorId", "DoctorId", "doctorUserId", "DoctorUserId", "userId", "UserId");

        if (date.HasValue && !string.IsNullOrWhiteSpace(shiftId) && !string.IsNullOrWhiteSpace(doctorId))
        {
            var allSchedules = await GetFirstAvailableCollectionAsync(_settings.DoctorScheduleCollections, cancellationToken);
            if (HasDoctorScheduleConflict(allSchedules.Documents, doctorId, room, date.Value, shiftId, documentId))
            {
                throw new InvalidOperationException("Lịch bị trùng bác sĩ hoặc trùng phòng trong cùng ngày, cùng ca.");
            }
        }

        await doc.UpdateAsync(new Dictionary<string, object>
        {
            ["isActive"] = isActive,
            ["status"] = isActive ? "available" : "unavailable",
            ["availableSlots"] = Math.Max(availableSlots, 0),
            ["room"] = room?.Trim() ?? string.Empty,
            ["updatedAt"] = Timestamp.GetCurrentTimestamp()
        }, cancellationToken: cancellationToken);
    }

    public async Task<int> BackfillScheduleRoomsAsync(WorkScheduleBackfillRoomViewModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Room)) return 0;

        var docs = await GetFirstAvailableCollectionAsync(_settings.DoctorScheduleCollections, cancellationToken);
        var collection = _firestore.Collection(_settings.DoctorScheduleCollections.First());
        var updatedCount = 0;

        foreach (var doc in docs.Documents)
        {
            var currentRoom = GetString(doc, "room", "roomId", "roomName");
            if (!string.IsNullOrWhiteSpace(currentRoom)) continue;

            var doctorId = GetString(doc, "doctorId", "DoctorId");
            var departmentId = GetString(doc, "departmentId", "DepartmentId");

            if (!string.IsNullOrWhiteSpace(model.DoctorId) &&
                !string.Equals(doctorId, model.DoctorId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(model.DepartmentId) &&
                !string.Equals(departmentId, model.DepartmentId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            await collection.Document(doc.Id).UpdateAsync(new Dictionary<string, object>
            {
                ["room"] = model.Room.Trim(),
                ["updatedAt"] = Timestamp.GetCurrentTimestamp()
            }, cancellationToken: cancellationToken);
            updatedCount++;
        }

        return updatedCount;
    }

    private static Dictionary<string, ShiftInfo> LoadShiftInfo(IEnumerable<DocumentSnapshot> docs)
    {
        return docs.ToDictionary(
            x => x.Id,
            x => new ShiftInfo(
                GetString(x, "name", "shiftName", "title") ?? x.Id,
                GetString(x, "type", "shiftType", "ShiftType"),
                GetString(x, "startTime", "start", "from"),
                GetString(x, "endTime", "end", "to"),
                GetString(x, "room", "roomName", "location") ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> LoadDepartmentRoomsAsync(CancellationToken cancellationToken)
    {
        var docs = await GetFirstAvailableCollectionAsync(_settings.DepartmentCollections, cancellationToken);
        return docs.Documents.ToDictionary(
            x => x.Id,
            x => GetStringList(x, "rooms", "Rooms", "roomNames", "RoomNames"),
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> GetStringList(DocumentSnapshot snapshot, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (!snapshot.ContainsField(fieldName)) continue;

            try
            {
                var value = snapshot.GetValue<object?>(fieldName);
                return value switch
                {
                    IEnumerable<object> list => list
                        .Select(x => x?.ToString())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)
                        .ToList(),
                    string s when !string.IsNullOrWhiteSpace(s) => s
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)
                        .ToList(),
                    _ => Array.Empty<string>()
                };
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        return Array.Empty<string>();
    }

    private static string FormatShiftTime(ShiftInfo shift)
    {
        if (string.IsNullOrWhiteSpace(shift.StartTime) && string.IsNullOrWhiteSpace(shift.EndTime)) return string.Empty;
        return $"{shift.StartTime} - {shift.EndTime}";
    }

    private static string BuildScheduleDocumentId(string doctorId, DateOnly date, string shiftId)
    {
        var safeDoctor = NormalizeDocumentIdPart(doctorId);
        var safeShift = NormalizeDocumentIdPart(shiftId);
        return $"{safeDoctor}_{date:yyyyMMdd}_{safeShift}";
    }

    private static string NormalizeDocumentIdPart(string value)
    {
        return value.Trim()
            .Replace("/", "_", StringComparison.Ordinal)
            .Replace("\\", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal);
    }

    private static DateOnly? GetScheduleDate(DocumentSnapshot doc)
    {
        var value = GetDateTime(doc, "date", "workDate", "shiftDate", "scheduleDate", "startAt", "startTime", "createdAt");
        return value.HasValue ? DateOnly.FromDateTime(value.Value) : null;
    }

    private static ShiftType ParseShiftType(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "morning" or "sang" or "sáng" or "ca_sang" or "ca_sáng" or "1" => ShiftType.Morning,
            "afternoon" or "chieu" or "chiều" or "ca_chieu" or "ca_chiều" or "2" => ShiftType.Afternoon,
            "night" or "toi" or "tối" or "dem" or "đêm" or "ca_toi" or "ca_tối" or "3" => ShiftType.Night,
            _ => ShiftType.Morning
        };
    }

    private static AssignmentStatus ParseAssignmentStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "pending" or "waiting" or "unassigned" => AssignmentStatus.Pending,
            _ => AssignmentStatus.Assigned
        };
    }

    private static string BuildShiftLabel(ShiftInfo shift, ShiftType type)
    {
        var name = !string.IsNullOrWhiteSpace(shift.Name)
            ? shift.Name
            : ShiftText(type);
        var time = !string.IsNullOrWhiteSpace(shift.StartTime) || !string.IsNullOrWhiteSpace(shift.EndTime)
            ? $" ({shift.StartTime} - {shift.EndTime})"
            : string.Empty;

        return $"{name}{time}";
    }

    private static string BuildScheduleSubtitle(string room, string department)
    {
        if (!string.IsNullOrWhiteSpace(room) && !string.IsNullOrWhiteSpace(department)) return $"{room} - {department}";
        if (!string.IsNullOrWhiteSpace(room)) return room;
        return department;
    }

    private static string ShiftText(ShiftType type) => type switch
    {
        ShiftType.Morning => "Ca sáng",
        ShiftType.Afternoon => "Ca chiều",
        ShiftType.Night => "Ca tối",
        _ => "Ca làm việc"
    };

    private static string ShiftPrefix(ShiftType type) => type switch
    {
        ShiftType.Morning => "S",
        ShiftType.Afternoon => "C",
        ShiftType.Night => "T",
        _ => "Ca"
    };

    private static string Initials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return string.Empty;
        return parts[^1].Substring(0, 1).ToUpperInvariant();
    }

    private static int CountScheduleConflicts(IEnumerable<DocumentSnapshot> docs)
    {
        return docs
            .Select(x => new
            {
                DoctorId = GetString(x, "doctorId", "DoctorId", "doctorUserId", "DoctorUserId", "userId", "UserId"),
                Date = GetScheduleDate(x),
                Shift = GetString(x, "shiftId", "ShiftId", "shiftType", "type", "ShiftType"),
                Room = NormalizeRoom(GetString(x, "room", "roomId", "roomName"))
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.DoctorId) && x.Date.HasValue && !string.IsNullOrWhiteSpace(x.Shift))
            .GroupBy(x => $"{x.DoctorId}|{x.Date}|{x.Shift}", StringComparer.OrdinalIgnoreCase)
            .Count(x => x.Count() > 1) +
            docs
                .Select(x => new
                {
                    Date = GetScheduleDate(x),
                    Shift = GetString(x, "shiftId", "ShiftId", "shiftType", "type", "ShiftType"),
                    Room = NormalizeRoom(GetString(x, "room", "roomId", "roomName"))
                })
                .Where(x => x.Date.HasValue && !string.IsNullOrWhiteSpace(x.Shift) && !string.IsNullOrWhiteSpace(x.Room))
                .GroupBy(x => $"{x.Room}|{x.Date}|{x.Shift}", StringComparer.OrdinalIgnoreCase)
                .Count(x => x.Count() > 1);
    }

    private static bool HasDoctorScheduleConflict(
        IEnumerable<DocumentSnapshot> docs,
        string doctorId,
        string? room,
        DateOnly date,
        string shiftId,
        string? ignoredDocumentId = null)
    {
        var normalizedRoom = NormalizeRoom(room);

        foreach (var doc in docs)
        {
            if (!string.IsNullOrWhiteSpace(ignoredDocumentId) &&
                string.Equals(doc.Id, ignoredDocumentId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var existingDate = GetScheduleDate(doc);
            if (existingDate != date) continue;

            var existingShift = GetString(doc, "shiftId", "ShiftId", "shiftType", "type", "ShiftType");
            if (!string.Equals(existingShift, shiftId, StringComparison.OrdinalIgnoreCase)) continue;

            var existingDoctor = GetString(doc, "doctorId", "DoctorId", "doctorUserId", "DoctorUserId", "userId", "UserId");
            if (string.Equals(existingDoctor, doctorId, StringComparison.OrdinalIgnoreCase)) return true;

            var existingRoom = NormalizeRoom(GetString(doc, "room", "roomId", "roomName"));
            if (!string.IsNullOrWhiteSpace(normalizedRoom) &&
                string.Equals(existingRoom, normalizedRoom, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeRoom(string? room)
    {
        return room?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private sealed record ShiftInfo(
        string Name,
        string? Type,
        string? StartTime,
        string? EndTime,
        string Room)
    {
        public static ShiftInfo FromSchedule(DocumentSnapshot doc)
        {
            return new ShiftInfo(
                GetString(doc, "shiftName", "name", "title") ?? string.Empty,
                GetString(doc, "shiftType", "type", "ShiftType"),
                GetString(doc, "startTime", "start", "from"),
                GetString(doc, "endTime", "end", "to"),
                GetString(doc, "room", "roomName", "location") ?? string.Empty);
        }
    }

    private static IReadOnlyList<PaymentReceiptLineItemViewModel> GetReceiptItems(DocumentSnapshot doc, decimal fallbackAmount)
    {
        foreach (var fieldName in new[] { "items", "lineItems", "services", "details" })
        {
            if (!doc.ContainsField(fieldName)) continue;

            try
            {
                var value = doc.GetValue<object?>(fieldName);
                var rows = value switch
                {
                    IEnumerable<object> list => list.Select((item, index) => MapReceiptItem(item, index + 1)).Where(x => x != null).Cast<PaymentReceiptLineItemViewModel>().ToList(),
                    _ => new List<PaymentReceiptLineItemViewModel>()
                };

                if (rows.Count > 0)
                {
                    return rows;
                }
            }
            catch
            {
                return Array.Empty<PaymentReceiptLineItemViewModel>();
            }
        }

        var description = GetString(doc, "serviceName", "description", "service", "serviceTitle") ?? "Dịch vụ khám, chữa bệnh";
        return new[]
        {
            new PaymentReceiptLineItemViewModel
            {
                Index = 1,
                Description = description,
                Quantity = 1,
                Unit = "Lần",
                UnitPriceVnd = fallbackAmount,
                AmountVnd = fallbackAmount
            }
        };
    }

    private static PaymentReceiptLineItemViewModel? MapReceiptItem(object item, int index)
    {
        if (item is not IDictionary<string, object> data)
        {
            return null;
        }

        var quantity = GetMapDecimal(data, "quantity", "qty", "count");
        if (quantity <= 0)
        {
            quantity = 1;
        }

        var amount = GetMapDecimal(data, "amount", "totalAmount", "lineTotal", "price");
        var unitPrice = GetMapDecimal(data, "unitPrice", "price", "fee");
        if (unitPrice <= 0 && amount > 0)
        {
            unitPrice = amount / quantity;
        }

        if (amount <= 0)
        {
            amount = unitPrice * quantity;
        }

        return new PaymentReceiptLineItemViewModel
        {
            Index = index,
            Description = GetMapString(data, "description", "serviceName", "name", "title") ?? "Dịch vụ khám, chữa bệnh",
            Quantity = quantity,
            Unit = GetMapString(data, "unit", "dvt", "unitName") ?? "Lần",
            UnitPriceVnd = unitPrice,
            AmountVnd = amount
        };
    }

    private static string? GetMapString(IDictionary<string, object> data, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (data.TryGetValue(key, out var value) && value is not null)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    private static decimal GetMapDecimal(IDictionary<string, object> data, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!data.TryGetValue(key, out var value) || value is null) continue;
            try
            {
                return value switch
                {
                    decimal d => d,
                    double d => Convert.ToDecimal(d),
                    float f => Convert.ToDecimal(f),
                    long l => l,
                    int i => i,
                    string s when decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
                    string s when decimal.TryParse(s, NumberStyles.Any, CultureInfo.GetCultureInfo("vi-VN"), out var parsed) => parsed,
                    _ => 0
                };
            }
            catch
            {
                return 0;
            }
        }

        return 0;
    }

    private static string ToVietnameseMoneyWords(decimal amount)
    {
        var rounded = Math.Max(0, decimal.Round(amount, 0));
        return string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:N0} đồng", rounded);
    }

    private async Task<QuerySnapshot> GetFirstAvailableCollectionAsync(string[] collectionNames, CancellationToken cancellationToken)
    {
        foreach (var collectionName in collectionNames.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal))
        {
            var snapshot = await _firestore.Collection(collectionName).Limit(500).GetSnapshotAsync(cancellationToken);
            if (snapshot.Count > 0)
            {
                return snapshot;
            }
        }

        var fallbackCollection = collectionNames.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "_missing_collection";
        return await _firestore.Collection(fallbackCollection).Limit(0).GetSnapshotAsync(cancellationToken);
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-offset);
    }

    private static IReadOnlyList<DashboardRevenuePointViewModel> BuildWeeklyRevenue(
        IEnumerable<TransactionListItemViewModel> transactions,
        DateTime weekStart)
    {
        var labels = new[] { "T2", "T3", "T4", "T5", "T6", "T7", "CN" };
        return Enumerable.Range(0, 7)
            .Select(i =>
            {
                var date = weekStart.AddDays(i).Date;
                return new DashboardRevenuePointViewModel
                {
                    Label = labels[i],
                    Amount = transactions
                        .Where(x => x.Status == TransactionStatus.Paid && x.PaidAt.Date == date)
                        .Sum(x => x.AmountVnd)
                };
            })
            .ToList();
    }

    private static IReadOnlyList<DashboardReminderViewModel> BuildDashboardReminders(
        IReadOnlyList<PatientListItemViewModel> patients,
        IReadOnlyList<AppointmentListItemViewModel> appointments,
        PaymentsIndexViewModel payments)
    {
        var result = new List<DashboardReminderViewModel>();
        var pendingInsurance = patients.Count(x => string.Equals(x.HealthInsuranceStatus, "pending", StringComparison.OrdinalIgnoreCase));
        if (pendingInsurance > 0)
        {
            result.Add(new DashboardReminderViewModel
            {
                Title = "Duyệt hồ sơ BHYT",
                Description = $"{pendingInsurance} hồ sơ đang chờ xét duyệt",
                Tone = "warning"
            });
        }

        if (payments.PendingCount > 0)
        {
            result.Add(new DashboardReminderViewModel
            {
                Title = "Xử lý thanh toán",
                Description = $"{payments.PendingCount} giao dịch đang chờ xử lý",
                Tone = "danger"
            });
        }

        var waitingToday = appointments.Count(x => x.ScheduledAt.Date == DateTime.Today && x.Status == AppointmentStatus.Pending);
        if (waitingToday > 0)
        {
            result.Add(new DashboardReminderViewModel
            {
                Title = "Xác nhận lịch hẹn hôm nay",
                Description = $"{waitingToday} ca chưa xác nhận",
                Tone = "primary"
            });
        }

        if (result.Count == 0)
        {
            result.Add(new DashboardReminderViewModel
            {
                Title = "Không có việc cần xử lý",
                Description = "Các luồng chính đang ổn định",
                Tone = "secondary"
            });
        }

        return result;
    }

    private static MedicalRecordStatus ParseMedicalRecordStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "approved" or "done" or "completed" => MedicalRecordStatus.Approved,
            "cancelled" or "canceled" or "rejected" => MedicalRecordStatus.Cancelled,
            _ => MedicalRecordStatus.Pending
        };
    }

    private static string NormalizeInsuranceStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "approved" or "da_duyet" or "đã duyệt" => "approved",
            "pending" or "cho_duyet" or "chờ duyệt" => "pending",
            "rejected" or "tu_choi" or "từ chối" => "rejected",
            "none" or "missing" => "none",
            _ => string.Empty
        };
    }

    private async Task<DocumentSnapshot?> GetFirstExistingDocumentAsync(
        string[] collectionNames,
        string documentId,
        CancellationToken cancellationToken)
    {
        foreach (var collectionName in collectionNames.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal))
        {
            var doc = await _firestore.Collection(collectionName).Document(documentId).GetSnapshotAsync(cancellationToken);
            if (doc.Exists) return doc;
        }

        return null;
    }

    private async Task<Dictionary<string, string>> LoadUserNamesAsync(CancellationToken cancellationToken)
    {
        var docs = await GetFirstAvailableCollectionAsync(_settings.UserCollections, cancellationToken);
        return docs.Documents.ToDictionary(
            x => x.Id,
            x => GetString(x, "fullName", "FullName", "username", "Username", "email") ?? x.Id);
    }

    private async Task<Dictionary<string, AppointmentUserInfo>> LoadAppointmentUsersAsync(CancellationToken cancellationToken)
    {
        var docs = await GetFirstAvailableCollectionAsync(_settings.UserCollections, cancellationToken);
        return docs.Documents.ToDictionary(
            x => x.Id,
            x => new AppointmentUserInfo(
                GetString(x, "uid", "Uid") ?? x.Id,
                GetString(x, "fullName", "FullName", "username", "Username", "email") ?? x.Id,
                GetString(x, "phone", "phoneNumber", "Phone") ?? string.Empty,
                GetString(x, "email", "Email") ?? string.Empty));
    }

    private async Task<Dictionary<string, string>> LoadDoctorNamesAsync(CancellationToken cancellationToken)
    {
        var docs = await GetFirstAvailableCollectionAsync(_settings.DoctorCollections, cancellationToken);
        return docs.Documents.ToDictionary(
            x => x.Id,
            x => GetString(x, "fullName", "FullName", "doctorName", "name") ?? x.Id);
    }

    private async Task<IReadOnlyList<AppointmentDoctorInfo>> LoadAppointmentDoctorsAsync(CancellationToken cancellationToken)
    {
        var docs = await GetFirstAvailableCollectionAsync(_settings.DoctorCollections, cancellationToken);
        var users = await GetFirstAvailableCollectionAsync(_settings.UserCollections, cancellationToken);
        var result = new List<AppointmentDoctorInfo>();

        foreach (var doctor in docs.Documents)
        {
            var user = FindLinkedDoctorUser(doctor, users.Documents);
            var sources = new DocumentSnapshot?[] { doctor, user };

            result.Add(new AppointmentDoctorInfo(
                doctor.Id,
                GetString(doctor, "userId", "UserId"),
                GetStringFromSources(sources, "fullName", "FullName", "doctorName", "name", "username", "Username") ?? doctor.Id,
                GetStringFromSources(sources, "phone", "phoneNumber", "Phone") ?? string.Empty,
                GetStringFromSources(sources, "email", "Email") ?? string.Empty,
                GetString(doctor, "specialization", "Specialization")));
        }

        return result;
    }

    private async Task<Dictionary<string, string>> LoadDepartmentNamesAsync(CancellationToken cancellationToken)
    {
        var docs = await GetFirstAvailableCollectionAsync(_settings.DepartmentCollections, cancellationToken);
        return docs.Documents.ToDictionary(
            x => x.Id,
            x => GetString(x, "departmentName", "name", "DepartmentName") ?? x.Id);
    }

    private static AppointmentDoctorInfo? ResolveAppointmentDoctor(
        IReadOnlyList<AppointmentDoctorInfo> doctors,
        string? doctorId,
        string? doctorUserId)
    {
        if (!string.IsNullOrWhiteSpace(doctorId))
        {
            var byDocumentId = doctors.FirstOrDefault(x =>
                string.Equals(x.DocumentId, doctorId, StringComparison.OrdinalIgnoreCase));
            if (byDocumentId is not null) return byDocumentId;

            var byUserId = doctors.FirstOrDefault(x =>
                string.Equals(x.UserId, doctorId, StringComparison.OrdinalIgnoreCase));
            if (byUserId is not null) return byUserId;
        }

        if (!string.IsNullOrWhiteSpace(doctorUserId))
        {
            return doctors.FirstOrDefault(x =>
                string.Equals(x.UserId, doctorUserId, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private sealed record AppointmentUserInfo(
        string Uid,
        string FullName,
        string Phone,
        string Email);

    private sealed record AppointmentDoctorInfo(
        string DocumentId,
        string? UserId,
        string FullName,
        string Phone,
        string Email,
        string? Specialization);

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

    private static bool? GetBool(DocumentSnapshot snapshot, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (!snapshot.ContainsField(fieldName)) continue;

            try
            {
                var value = snapshot.GetValue<object?>(fieldName);
                return value switch
                {
                    bool b => b,
                    string s when bool.TryParse(s, out var parsed) => parsed,
                    string s when s.Equals("yes", StringComparison.OrdinalIgnoreCase) => true,
                    string s when s.Equals("no", StringComparison.OrdinalIgnoreCase) => false,
                    int i => i != 0,
                    long l => l != 0,
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

    private static int GetInt(DocumentSnapshot snapshot, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (!snapshot.ContainsField(fieldName)) continue;

            try
            {
                var value = snapshot.GetValue<object?>(fieldName);
                return value switch
                {
                    int i => i,
                    long l => Convert.ToInt32(l),
                    double d => Convert.ToInt32(d),
                    decimal d => Convert.ToInt32(d),
                    string s when int.TryParse(s, out var parsed) => parsed,
                    _ => 0
                };
            }
            catch
            {
                return 0;
            }
        }

        return 0;
    }

    private static string? GetStringFromSources(IEnumerable<DocumentSnapshot?> snapshots, params string[] fieldNames)
    {
        foreach (var snapshot in snapshots)
        {
            if (snapshot is null) continue;

            var value = GetString(snapshot, fieldNames);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return null;
    }

    private static DateTime? GetDateTimeFromSources(IEnumerable<DocumentSnapshot?> snapshots, params string[] fieldNames)
    {
        foreach (var snapshot in snapshots)
        {
            if (snapshot is null) continue;

            var value = GetDateTime(snapshot, fieldNames);
            if (value.HasValue) return value;
        }

        return null;
    }

    private static DocumentSnapshot? FindLinkedDoctorUser(DocumentSnapshot doctor, IEnumerable<DocumentSnapshot> users)
    {
        var userList = users.ToList();
        var linkedIds = new[]
        {
            doctor.Id,
            GetString(doctor, "userId", "UserId", "uid", "Uid", "accountId", "authUid")
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var userById = userList.FirstOrDefault(x => linkedIds.Contains(x.Id));
        if (userById is not null) return userById;

        return userList.FirstOrDefault(x =>
            string.Equals(GetString(x, "doctorId", "DoctorId"), doctor.Id, StringComparison.OrdinalIgnoreCase) ||
            linkedIds.Contains(GetString(x, "uid", "Uid", "userId", "UserId") ?? string.Empty));
    }

    private static IReadOnlyList<DoctorDetailFieldViewModel> BuildDoctorProfileFields(DoctorDetailsViewModel doctor)
    {
        return BuildDoctorFields(
            ("Mã hồ sơ bác sĩ", doctor.DocumentId),
            ("UID tài khoản", doctor.UserId),
            ("Họ và tên", doctor.FullName),
            ("Khoa", doctor.Department),
            ("Mã khoa", doctor.DepartmentId),
            ("Giới tính", FormatGenderText(doctor.Gender)),
            ("Ngày sinh", FormatDoctorDate(doctor.DateOfBirth)),
            ("Ngày tạo hồ sơ", FormatDoctorDateTime(doctor.CreatedAt)),
            ("Cập nhật gần nhất", FormatDoctorDateTime(doctor.UpdatedAt)));
    }

    private static IReadOnlyList<DoctorDetailFieldViewModel> BuildDoctorContactFields(DoctorDetailsViewModel doctor)
    {
        return BuildDoctorFields(
            ("Số điện thoại", doctor.Phone),
            ("Email", doctor.Email),
            ("Ảnh đại diện", doctor.AvatarUrl),
            ("Email đã xác minh", FormatBooleanText(doctor.EmailVerified)));
    }

    private static IReadOnlyList<DoctorDetailFieldViewModel> BuildDoctorProfessionalFields(DoctorDetailsViewModel doctor)
    {
        return BuildDoctorFields(
            ("Chuyên khoa", doctor.Specialization),
            ("Chứng chỉ hành nghề", doctor.LicenseNumber),
            ("Học vị", doctor.Degree),
            ("Số năm kinh nghiệm", doctor.YearsOfExperience),
            ("Phí khám", doctor.ConsultationFee),
            ("Giới thiệu", doctor.Biography));
    }

    private static IReadOnlyList<DoctorDetailFieldViewModel> BuildDoctorAdminFields(DoctorDetailsViewModel doctor)
    {
        return BuildDoctorFields(
            ("Vai trò", FormatRoleText(doctor.Role)),
            ("Trạng thái tài khoản", doctor.UserStatus),
            ("Đang hoạt động", FormatBooleanText(doctor.IsActive)),
            ("Trạng thái duyệt hồ sơ", doctor.VerificationStatus));
    }

    private static IReadOnlyList<DoctorDetailFieldViewModel> BuildDoctorFields(params (string Label, string? Value)[] fields)
    {
        return fields
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => new DoctorDetailFieldViewModel
            {
                Label = x.Label,
                Value = x.Value!.Trim()
            })
            .ToList();
    }

    private static string? FormatGenderText(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "male" or "nam" => "Nam",
            "female" or "nu" or "nữ" => "Nữ",
            _ => value
        };
    }

    private static string? FormatRoleText(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "doctor" => "Bác sĩ",
            "patient" => "Bệnh nhân",
            "admin" => "Quản trị viên",
            _ => value
        };
    }

    private static string? FormatUserStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "active" => "Đang hoạt động",
            "pending" => "Chờ kích hoạt",
            "blocked" => "Đã khóa",
            _ => value
        };
    }

    private static string? FormatVerificationStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "approved" => "Đã duyệt",
            "pending" or "pending_verification" => "Chờ duyệt",
            "rejected" => "Từ chối",
            "draft" => "Bản nháp",
            _ => value
        };
    }

    private static string? FormatBooleanText(bool? value)
    {
        return value switch
        {
            true => "Có",
            false => "Không",
            _ => null
        };
    }

    private static string? FormatExperience(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        return decimal.TryParse(value, out _) && !value.Contains("năm", StringComparison.OrdinalIgnoreCase)
            ? $"{value.Trim()} năm"
            : value;
    }

    private static string? FormatConsultationFee(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        return decimal.TryParse(value, out var amount)
            ? $"{amount:N0} VND"
            : value;
    }

    private static string? FormatDoctorDate(DateTime? value)
    {
        return value?.ToString("dd/MM/yyyy");
    }

    private static string? FormatDoctorDateTime(DateTime? value)
    {
        return value?.ToString("dd/MM/yyyy HH:mm");
    }

    private static DateTime GetAppointmentDateTime(DocumentSnapshot doc)
    {
        var scheduledAt = GetDateTime(doc, "scheduledAt", "appointmentDateTime", "startAt", "startTime");
        if (scheduledAt.HasValue) return scheduledAt.Value;

        var date = GetDateTime(doc, "appointmentDate", "date", "bookingDate");
        var time = GetTimeSpan(doc, "appointmentTime", "time", "slotTime", "startHour");
        if (date.HasValue && time.HasValue) return date.Value.Date.Add(time.Value);

        return date ?? GetDateTime(doc, "CreatedAt", "createdAt") ?? DateTime.Now;
    }

    private static TimeSpan? GetTimeSpan(DocumentSnapshot snapshot, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (!snapshot.ContainsField(fieldName)) continue;

            try
            {
                var value = snapshot.GetValue<object?>(fieldName);
                return value switch
                {
                    Timestamp ts => ts.ToDateTime().ToLocalTime().TimeOfDay,
                    DateTime dt => dt.TimeOfDay,
                    string s when TimeSpan.TryParse(s, out var parsed) => parsed,
                    string s when DateTime.TryParse(s, out var parsedDate) => parsedDate.TimeOfDay,
                    int i => TimeSpan.FromMinutes(i),
                    long l => TimeSpan.FromMinutes(l),
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

        return FormatDoctorStatus(GetString(doc, "status", "Status"));
    }

    private static string ParseDoctorStatus(DocumentSnapshot doctor, DocumentSnapshot? user)
    {
        if (doctor.ContainsField("isActive") || doctor.ContainsField("status") || doctor.ContainsField("Status"))
        {
            return ParseDoctorStatus(doctor);
        }

        return user is null ? ParseDoctorStatus(doctor) : ParseDoctorStatus(user);
    }

    private static string FormatDoctorStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "active" or "available" or "working" => "Đang hoạt động",
            "busy" or "examining" or "inprogress" or "in_progress" => "Đang khám",
            "inactive" or "disabled" or "paused" or "off" => "Tạm nghỉ",
            _ when !string.IsNullOrWhiteSpace(value) => value,
            _ => "Đang hoạt động"
        };
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

    private static string FormatAppointmentType(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "online" or "telehealth" => "Khám trực tuyến",
            "offline" or "clinic" or "in_person" => "Khám tại phòng khám",
            "followup" or "follow_up" => "Tái khám",
            _ when !string.IsNullOrWhiteSpace(value) => value,
            _ => string.Empty
        };
    }

    private static string FormatPaymentStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "paid" or "success" or "successful" => "Đã thanh toán",
            "failed" or "cancelled" or "canceled" => "Thanh toán lỗi",
            "refunded" => "Đã hoàn tiền",
            "unpaid" => "Chưa thanh toán",
            "pending" => "Chờ thanh toán",
            _ when !string.IsNullOrWhiteSpace(value) => value,
            _ => string.Empty
        };
    }

    private static string FormatDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var text = value.Trim();
        return decimal.TryParse(text, out _) && !text.Contains("phút", StringComparison.OrdinalIgnoreCase)
            ? $"{text} phút"
            : text;
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

