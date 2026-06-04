using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;
using System.Globalization;
using Web_Admin_Booking_App.Models;

namespace Web_Admin_Booking_App.Services;

public sealed class PatientProfileResolver
{
    private readonly FirestoreDb _firestore;
    private readonly FirebaseSettings _settings;
    private readonly CodeGeneratorService _codeGenerator;

    public PatientProfileResolver(
        FirestoreDb firestore,
        IOptions<FirebaseSettings> options,
        CodeGeneratorService codeGenerator)
    {
        _firestore = firestore;
        _settings = options.Value;
        _codeGenerator = codeGenerator;
    }

    public async Task<IReadOnlyList<PatientListItemViewModel>> GetPatientsAsync(CancellationToken cancellationToken = default)
    {
        var groups = await LoadPatientGroupsAsync(cancellationToken);
        return groups.Values
            .Select(MapPatientListItem)
            .OrderBy(x => x.FullName)
            .ThenBy(x => x.Code)
            .ToList();
    }

    public async Task<PatientDetailsViewModel?> GetPatientDetailsAsync(string id, CancellationToken cancellationToken = default)
    {
        var snapshots = await LoadPatientSnapshotsAsync(id, cancellationToken);
        if (snapshots.Count == 0) return null;

        var model = MapPatientDetails(id, snapshots);
        model.RecentAppointments = await LoadRecentAppointmentsAsync(model.Id, model.Code, cancellationToken);
        return model;
    }

    public async Task<InsuranceApprovalIndexViewModel> GetInsuranceApprovalsAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var patients = await GetPatientsAsync(cancellationToken);
        var all = patients
            .Where(x => x.HasPendingInsuranceRequest)
            .Select(x => new InsuranceApprovalListItemViewModel
            {
                PatientId = x.Id,
                InsuranceCode = x.InsuranceCode,
                PatientCode = x.Code,
                PatientName = x.FullName,
                Phone = x.Phone,
                CurrentInsuranceNumber = x.HealthInsuranceNumber,
                PendingInsuranceNumber = string.IsNullOrWhiteSpace(x.PendingHealthInsuranceNumber)
                    ? x.HealthInsuranceNumber
                    : x.PendingHealthInsuranceNumber,
                SubmittedAt = x.UpdatedAt ?? x.CreatedAt,
                Status = "pending",
                RejectReason = x.HealthInsuranceRejectReason
            })
            .ToList();

        var key = search?.Trim().ToLowerInvariant();
        var filtered = string.IsNullOrWhiteSpace(key)
            ? all
            : all.Where(x =>
                x.PatientId.ToLowerInvariant().Contains(key) ||
                x.PatientCode.ToLowerInvariant().Contains(key) ||
                x.PatientName.ToLowerInvariant().Contains(key) ||
                x.Phone.ToLowerInvariant().Contains(key) ||
                x.CurrentInsuranceNumber.ToLowerInvariant().Contains(key) ||
                x.PendingInsuranceNumber.ToLowerInvariant().Contains(key))
                .ToList();

        var allPatients = await GetPatientsAsync(cancellationToken);
        return new InsuranceApprovalIndexViewModel
        {
            Search = search,
            PendingCount = all.Count,
            ApprovedCount = allPatients.Count(x => string.Equals(x.HealthInsuranceStatus, "approved", StringComparison.OrdinalIgnoreCase)),
            RejectedCount = allPatients.Count(x => string.Equals(x.HealthInsuranceStatus, "rejected", StringComparison.OrdinalIgnoreCase)),
            ExpiredCount = allPatients.Count(x => string.Equals(x.HealthInsuranceStatus, "expired", StringComparison.OrdinalIgnoreCase)),
            Items = filtered
                .OrderByDescending(x => x.SubmittedAt ?? DateTime.MinValue)
                .ThenBy(x => x.PatientName)
                .ToList()
        };
    }

    public async Task<bool> ApproveInsuranceAsync(
        string patientId,
        DateOnly expiryDate,
        string staffId,
        CancellationToken cancellationToken = default)
    {
        if (expiryDate < DateOnly.FromDateTime(DateTime.Today))
        {
            throw new InvalidOperationException("Ngày hết hạn BHYT phải lớn hơn hoặc bằng hôm nay.");
        }

        var snapshots = await LoadPatientSnapshotsAsync(patientId, cancellationToken);
        if (snapshots.Count == 0) return false;

        var pendingNumber = GetFirstString(snapshots, "pendingInsuranceNumber", "pendingHealthInsuranceNumber");
        var currentNumber = GetFirstString(snapshots, "insuranceNumber", "healthInsuranceNumber", "bhytNumber", "maBHYT");
        var insuranceNumber = string.IsNullOrWhiteSpace(pendingNumber) ? currentNumber : pendingNumber;
        if (string.IsNullOrWhiteSpace(insuranceNumber))
        {
            throw new InvalidOperationException("Không tìm thấy mã BHYT chờ duyệt.");
        }

        var now = Timestamp.GetCurrentTimestamp();
        var expiry = Timestamp.FromDateTime(DateTime.SpecifyKind(expiryDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc));
        var updates = new Dictionary<string, object?>
        {
            ["insuranceNumber"] = insuranceNumber.Trim(),
            ["healthInsuranceNumber"] = insuranceNumber.Trim(),
            ["insuranceStatus"] = "approved",
            ["healthInsuranceStatus"] = "approved",
            ["status_bhyt"] = "approved",
            ["insuranceExpiryDate"] = expiry,
            ["healthInsuranceExpiryDate"] = expiry,
            ["insuranceCoveragePercent"] = 80,
            ["insuranceAppliedEligible"] = true,
            ["pendingInsuranceNumber"] = null,
            ["pendingHealthInsuranceNumber"] = null,
            ["pendingHealthInsuranceStatus"] = null,
            ["pendingHealthInsuranceRejectReason"] = null,
            ["insuranceApprovedAt"] = now,
            ["insuranceApprovedByStaffId"] = string.IsNullOrWhiteSpace(staffId) ? "staff" : staffId.Trim(),
            ["healthInsuranceUpdatedAt"] = now,
            ["reject_reason"] = null,
            ["healthInsuranceRejectReason"] = null,
            ["insuranceRejectReason"] = null,
            ["updatedAt"] = now
        };

        await UpdateInsuranceSourcesAsync(snapshots, patientId, updates, cancellationToken);
        await CreateInsuranceNotificationAsync(patientId, "BHYT đã được duyệt", $"Mã BHYT {insuranceNumber.Trim()} đã được duyệt. Hạn thẻ: {expiryDate:dd/MM/yyyy}.", cancellationToken);
        return true;
    }

    public async Task<bool> RejectInsuranceAsync(
        string patientId,
        string rejectReason,
        string staffId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rejectReason))
        {
            throw new InvalidOperationException("Vui lòng nhập lý do từ chối BHYT.");
        }

        var snapshots = await LoadPatientSnapshotsAsync(patientId, cancellationToken);
        if (snapshots.Count == 0) return false;

        var now = Timestamp.GetCurrentTimestamp();
        var hasApprovedInsurance = IsApprovedStatus(GetFirstString(snapshots, "insuranceStatus", "healthInsuranceStatus", "status_bhyt", "status"));
        var updates = new Dictionary<string, object?>
        {
            ["pendingInsuranceNumber"] = null,
            ["pendingHealthInsuranceNumber"] = null,
            ["pendingHealthInsuranceStatus"] = "rejected",
            ["pendingHealthInsuranceRejectReason"] = rejectReason.Trim(),
            ["insuranceRejectReason"] = rejectReason.Trim(),
            ["healthInsuranceRejectReason"] = rejectReason.Trim(),
            ["reject_reason"] = rejectReason.Trim(),
            ["insuranceRejectedAt"] = now,
            ["insuranceRejectedByStaffId"] = string.IsNullOrWhiteSpace(staffId) ? "staff" : staffId.Trim(),
            ["healthInsuranceUpdatedAt"] = now,
            ["updatedAt"] = now
        };

        if (!hasApprovedInsurance)
        {
            updates["insuranceStatus"] = "rejected";
            updates["healthInsuranceStatus"] = "rejected";
            updates["status_bhyt"] = "rejected";
        }

        await UpdateInsuranceSourcesAsync(snapshots, patientId, updates, cancellationToken);
        await CreateInsuranceNotificationAsync(patientId, "BHYT bị từ chối", $"Yêu cầu BHYT bị từ chối. Lý do: {rejectReason.Trim()}", cancellationToken);
        return true;
    }

    private async Task UpdateInsuranceSourcesAsync(
        IReadOnlyList<DocumentSnapshot> snapshots,
        string patientId,
        Dictionary<string, object?> updates,
        CancellationToken cancellationToken)
    {
        var refs = snapshots
            .Where(x => IsPatientProfileCollection(x.Reference.Parent.Id) || IsInsuranceCollection(x.Reference.Parent.Id))
            .Select(x => x.Reference)
            .ToDictionary(x => x.Path, x => x, StringComparer.OrdinalIgnoreCase);

        if (!refs.Values.Any(x => IsInsuranceCollection(x.Parent.Id)))
        {
            refs[_firestore.Collection("health_insurances").Document(patientId).Path] =
                _firestore.Collection("health_insurances").Document(patientId);
        }

        var batch = _firestore.StartBatch();
        foreach (var docRef in refs.Values)
        {
            var payload = new Dictionary<string, object?>(updates);
            if (IsInsuranceCollection(docRef.Parent.Id))
            {
                if (updates.TryGetValue("insuranceStatus", out var insuranceStatus) && insuranceStatus is string status)
                {
                    payload["status"] = status;
                }

                if (updates.TryGetValue("insuranceExpiryDate", out var expiryValue))
                {
                    payload["expiryDate"] = expiryValue;
                }

                if (updates.TryGetValue("insuranceApprovedAt", out var approvedAt))
                {
                    payload["verifiedAt"] = approvedAt;
                }

                if (updates.TryGetValue("insuranceApprovedByStaffId", out var approvedBy))
                {
                    payload["verifiedBy"] = approvedBy;
                }

                if (updates.TryGetValue("insuranceRejectReason", out var rejectReason))
                {
                    payload["rejectReason"] = rejectReason;
                }
            }

            batch.Set(docRef, payload, SetOptions.MergeAll);
        }

        await batch.CommitAsync(cancellationToken);
    }

    private async Task<Dictionary<string, List<DocumentSnapshot>>> LoadPatientGroupsAsync(CancellationToken cancellationToken)
    {
        var groups = new Dictionary<string, List<DocumentSnapshot>>(StringComparer.OrdinalIgnoreCase);

        foreach (var collectionName in GetUserCollections())
        {
            var snapshot = await _firestore.Collection(collectionName).GetSnapshotAsync(cancellationToken);
            foreach (var doc in snapshot.Documents.Where(x => x.Exists))
            {
                var role = GetString(doc, "role", "Role")?.Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(role) && role != "patient") continue;
                AddToGroup(groups, ResolvePatientKey(doc), doc);
            }
        }

        foreach (var collectionName in GetPatientCollections())
        {
            var snapshot = await _firestore.Collection(collectionName).GetSnapshotAsync(cancellationToken);
            foreach (var doc in snapshot.Documents.Where(x => x.Exists))
            {
                AddToGroup(groups, ResolvePatientKey(doc), doc);
            }
        }

        foreach (var collectionName in new[] { "health_insurances" })
        {
            var snapshot = await _firestore.Collection(collectionName).GetSnapshotAsync(cancellationToken);
            foreach (var doc in snapshot.Documents.Where(x => x.Exists))
            {
                AddToGroup(groups, ResolvePatientKey(doc), doc);
            }
        }

        return groups;
    }

    private async Task<List<DocumentSnapshot>> LoadPatientSnapshotsAsync(string id, CancellationToken cancellationToken)
    {
        var snapshots = new Dictionary<string, DocumentSnapshot>(StringComparer.OrdinalIgnoreCase);

        async Task AddDocumentAsync(DocumentReference docRef)
        {
            var snap = await docRef.GetSnapshotAsync(cancellationToken);
            if (snap.Exists) snapshots[snap.Reference.Path] = snap;
        }

        foreach (var collectionName in GetUserCollections().Concat(GetPatientCollections()).Concat(new[] { "health_insurances" }))
        {
            await AddDocumentAsync(_firestore.Collection(collectionName).Document(id));
        }

        foreach (var collectionName in GetUserCollections().Concat(GetPatientCollections()).Concat(new[] { "health_insurances" }))
        {
            foreach (var field in new[] { "uid", "userId", "patientId", "documentId", "UserId", "PatientId" })
            {
                var matches = await _firestore.Collection(collectionName).WhereEqualTo(field, id).Limit(10).GetSnapshotAsync(cancellationToken);
                foreach (var doc in matches.Documents.Where(x => x.Exists))
                {
                    snapshots[doc.Reference.Path] = doc;
                }
            }
        }

        return snapshots.Values
            .OrderByDescending(x => GetUpdatedAt(x) ?? DateTime.MinValue)
            .ThenBy(x => SourceRank(x.Reference.Parent.Id))
            .ToList();
    }

    private PatientListItemViewModel MapPatientListItem(IReadOnlyList<DocumentSnapshot> snapshots)
    {
        var sources = SortSources(snapshots);
        var id = GetFirstString(sources, "uid", "userId", "patientId", "documentId") ?? sources.First().Id;
        var insurance = ResolveInsurance(sources);

        return new PatientListItemViewModel
        {
            Id = id,
            Code = GetFirstString(sources, "patientCode", "userCode", "code") ?? id,
            FullName = GetFirstString(sources, "fullName", "name", "displayName", "username", "patientName") ?? "Chưa có tên",
            Phone = GetFirstString(sources, "phone", "phoneNumber", "mobile", "sdt") ?? "Chưa có số điện thoại",
            Email = GetFirstString(sources, "email") ?? string.Empty,
            Cccd = GetFirstString(sources, "cccd", "citizenId", "identityNumber", "idCard") ?? string.Empty,
            Gender = ParseGender(GetFirstString(sources, "gender", "sex")),
            Dob = GetFirstDateOnly(sources, "dateOfBirth", "birthDate", "dob", "birthday"),
            Status = ParsePatientStatus(GetFirstString(sources, "status", "accountStatus", "isActive")),
            HealthInsuranceNumber = insurance.Number,
            PendingHealthInsuranceNumber = insurance.PendingNumber,
            InsuranceCode = insurance.Code,
            HealthInsuranceStatus = insurance.Status,
            HealthInsuranceExpiryDate = insurance.ExpiryDate,
            InsuranceCoveragePercent = insurance.CoveragePercent,
            HealthInsuranceRejectReason = insurance.RejectReason,
            CreatedAt = GetFirstDateTime(sources, "createdAt"),
            UpdatedAt = GetFirstDateTime(sources, "updatedAt", "healthInsuranceUpdatedAt")
        };
    }

    private PatientDetailsViewModel MapPatientDetails(string requestedId, IReadOnlyList<DocumentSnapshot> snapshots)
    {
        var sources = SortSources(snapshots);
        var id = GetFirstString(sources, "uid", "userId", "patientId", "documentId") ?? requestedId;
        var insurance = ResolveInsurance(sources);

        return new PatientDetailsViewModel
        {
            Id = id,
            Code = GetFirstString(sources, "patientCode", "userCode", "code") ?? id,
            FullName = GetFirstString(sources, "fullName", "name", "displayName", "username", "patientName") ?? "Chưa có tên",
            Username = GetFirstString(sources, "username", "displayName") ?? string.Empty,
            Phone = GetFirstString(sources, "phone", "phoneNumber", "mobile", "sdt") ?? "Chưa có số điện thoại",
            Email = GetFirstString(sources, "email") ?? string.Empty,
            Cccd = GetFirstString(sources, "cccd", "citizenId", "identityNumber", "idCard") ?? string.Empty,
            Gender = ParseGender(GetFirstString(sources, "gender", "sex")),
            Dob = GetFirstDateOnly(sources, "dateOfBirth", "birthDate", "dob", "birthday"),
            Address = GetFirstString(sources, "address", "permanentAddress", "currentAddress") ?? "Chưa cập nhật",
            BloodType = GetFirstString(sources, "bloodType", "bloodGroup") ?? string.Empty,
            Allergy = GetFirstString(sources, "allergy", "allergies") ?? string.Empty,
            ChronicDisease = GetFirstString(sources, "chronicDisease", "chronicDiseases", "medicalHistory") ?? string.Empty,
            HealthInsuranceNumber = insurance.Number,
            PendingHealthInsuranceNumber = insurance.PendingNumber,
            InsuranceCode = insurance.Code,
            HealthInsuranceStatus = insurance.Status,
            HealthInsuranceExpiryDate = insurance.ExpiryDate,
            InsuranceCoveragePercent = insurance.CoveragePercent,
            HealthInsuranceRejectReason = insurance.RejectReason,
            Status = ParsePatientStatus(GetFirstString(sources, "status", "accountStatus", "isActive")),
            CreatedAt = GetFirstDateTime(sources, "createdAt"),
            UpdatedAt = GetFirstDateTime(sources, "updatedAt", "healthInsuranceUpdatedAt"),
            LastVisitAt = GetFirstDateTime(sources, "lastVisitAt", "lastAppointmentAt"),
            Notes = GetFirstString(sources, "notes", "note", "remark") ?? string.Empty
        };
    }

    private async Task<List<PatientAppointmentSummaryViewModel>> LoadRecentAppointmentsAsync(
        string patientId,
        string patientCode,
        CancellationToken cancellationToken)
    {
        var result = new List<PatientAppointmentSummaryViewModel>();
        foreach (var collectionName in _settings.AppointmentCollections.DefaultIfEmpty("Appointments"))
        {
            var snapshot = await _firestore.Collection(collectionName).GetSnapshotAsync(cancellationToken);
            foreach (var doc in snapshot.Documents.Where(x => x.Exists))
            {
                var currentPatientId = GetString(doc, "patientId", "PatientId", "userId", "UserId", "customerId");
                var currentPatientCode = GetString(doc, "patientCode", "userCode");
                if (!string.Equals(currentPatientId, patientId, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(currentPatientCode, patientCode, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(new PatientAppointmentSummaryViewModel
                {
                    Id = doc.Id,
                    DepartmentName = GetString(doc, "departmentName", "specialtyName", "SpecialtyName", "department") ?? "Chưa cập nhật",
                    DoctorName = GetString(doc, "doctorName", "DoctorName") ?? "Chưa phân bác sĩ",
                    AppointmentDate = GetDateTime(doc, "scheduledAt", "appointmentDate", "date", "createdAt"),
                    Status = GetString(doc, "status", "Status") ?? "pending",
                    Symptoms = GetString(doc, "symptoms", "reason", "note", "description") ?? string.Empty
                });
            }
        }

        return result
            .OrderByDescending(x => x.AppointmentDate ?? DateTime.MinValue)
            .Take(5)
            .ToList();
    }

    private async Task CreateInsuranceNotificationAsync(
        string patientId,
        string title,
        string body,
        CancellationToken cancellationToken)
    {
        var notificationCode = await _codeGenerator.GenerateNextCodeAsync("notifications", cancellationToken);
        var now = Timestamp.GetCurrentTimestamp();
        await _firestore.Collection("Notifications").AddAsync(new Dictionary<string, object?>
        {
            ["notificationCode"] = notificationCode,
            ["userId"] = patientId,
            ["patientId"] = patientId,
            ["recipientRole"] = "patient",
            ["type"] = "insurance",
            ["category"] = "health_insurance",
            ["title"] = title,
            ["body"] = body,
            ["isRead"] = false,
            ["createdAt"] = now,
            ["updatedAt"] = now
        }, cancellationToken);
    }

    private static InsuranceResolution ResolveInsurance(IReadOnlyList<DocumentSnapshot> sources)
    {
        var pendingNumber = GetFirstString(sources, "pendingInsuranceNumber", "pendingHealthInsuranceNumber", "pendingHealthInsuranceNo");
        var number = GetFirstString(sources, "insuranceNumber", "healthInsuranceNumber", "bhytNumber", "bhyt", "BHYT", "maBHYT") ?? string.Empty;
        var statusRaw = GetFirstString(sources, "pendingHealthInsuranceStatus", "insuranceStatus", "healthInsuranceStatus", "status_bhyt", "bhytStatus", "status");
        var expiryDate = GetFirstDateOnly(sources, "insuranceExpiryDate", "healthInsuranceExpiryDate", "expiryDate", "expiredAt");
        var status = NormalizeInsuranceStatus(statusRaw, pendingNumber, number, expiryDate);

        return new InsuranceResolution(
            Number: number,
            PendingNumber: pendingNumber ?? string.Empty,
            Code: GetFirstString(sources, "insuranceCode", "healthInsuranceCode") ?? string.Empty,
            Status: status,
            ExpiryDate: expiryDate,
            CoveragePercent: GetFirstInt(sources, "insuranceCoveragePercent", "coveragePercent"),
            RejectReason: GetFirstString(sources, "insuranceRejectReason", "healthInsuranceRejectReason", "pendingHealthInsuranceRejectReason", "rejectReason", "reject_reason") ?? string.Empty);
    }

    private static string NormalizeInsuranceStatus(string? rawStatus, string? pendingNumber, string? number, DateOnly? expiryDate)
    {
        if (!string.IsNullOrWhiteSpace(pendingNumber)) return "pending";

        var normalized = rawStatus?.Trim().ToLowerInvariant();
        var status = normalized switch
        {
            "approved" or "active" or "verified" or "da_duyet" or "đã duyệt" => "approved",
            "pending" or "submitted" or "waiting" or "waiting_review" or "cho_duyet" or "chờ duyệt" => "pending",
            "rejected" or "denied" or "tu_choi" or "từ chối" => "rejected",
            "expired" or "het_han" or "hết hạn" => "expired",
            "none" or "missing" or "unverified" => "none",
            _ => string.IsNullOrWhiteSpace(number) ? "none" : string.Empty
        };

        if (status == "approved" && expiryDate.HasValue)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (expiryDate.Value < today) return "expired";
        }

        return status;
    }

    private static IReadOnlyList<DocumentSnapshot> SortSources(IReadOnlyList<DocumentSnapshot> snapshots)
    {
        return snapshots
            .OrderByDescending(x => GetUpdatedAt(x) ?? DateTime.MinValue)
            .ThenBy(x => SourceRank(x.Reference.Parent.Id))
            .ToList();
    }

    private static void AddToGroup(Dictionary<string, List<DocumentSnapshot>> groups, string key, DocumentSnapshot doc)
    {
        if (!groups.TryGetValue(key, out var list))
        {
            list = new List<DocumentSnapshot>();
            groups[key] = list;
        }

        if (list.All(x => !string.Equals(x.Reference.Path, doc.Reference.Path, StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(doc);
        }
    }

    private static string ResolvePatientKey(DocumentSnapshot doc)
    {
        return GetString(doc, "uid", "userId", "patientId", "documentId", "UserId", "PatientId") ?? doc.Id;
    }

    private IEnumerable<string> GetUserCollections()
    {
        return _settings.UserCollections.DefaultIfEmpty("users")
            .Concat(new[] { "users", "Users" })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetPatientCollections()
    {
        return new[] { "patients", "Patients" };
    }

    private static bool IsPatientProfileCollection(string collectionName)
    {
        return collectionName.Equals("users", StringComparison.OrdinalIgnoreCase) ||
            collectionName.Equals("patients", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInsuranceCollection(string collectionName)
    {
        return collectionName.Equals("health_insurances", StringComparison.OrdinalIgnoreCase);
    }

    private static int SourceRank(string collectionName)
    {
        if (collectionName.Equals("users", StringComparison.OrdinalIgnoreCase)) return 0;
        if (collectionName.Equals("patients", StringComparison.OrdinalIgnoreCase)) return 1;
        if (collectionName.Equals("health_insurances", StringComparison.OrdinalIgnoreCase)) return 2;
        return 3;
    }

    private static bool IsApprovedStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() is "approved" or "active" or "verified";
    }

    private static string? GetFirstString(IReadOnlyList<DocumentSnapshot> snapshots, params string[] fields)
    {
        foreach (var snapshot in snapshots)
        {
            var value = GetString(snapshot, fields);
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }

        return null;
    }

    private static DateOnly? GetFirstDateOnly(IReadOnlyList<DocumentSnapshot> snapshots, params string[] fields)
    {
        foreach (var snapshot in snapshots)
        {
            var value = GetDateOnly(snapshot, fields);
            if (value.HasValue) return value.Value;
        }

        return null;
    }

    private static DateTime? GetFirstDateTime(IReadOnlyList<DocumentSnapshot> snapshots, params string[] fields)
    {
        foreach (var snapshot in snapshots)
        {
            var value = GetDateTime(snapshot, fields);
            if (value.HasValue) return value.Value;
        }

        return null;
    }

    private static int? GetFirstInt(IReadOnlyList<DocumentSnapshot> snapshots, params string[] fields)
    {
        foreach (var snapshot in snapshots)
        {
            var value = GetInt(snapshot, fields);
            if (value.HasValue) return value.Value;
        }

        return null;
    }

    private static string? GetString(DocumentSnapshot doc, params string[] fields)
    {
        foreach (var field in fields)
        {
            if (!doc.ContainsField(field)) continue;
            try
            {
                var value = doc.GetValue<object?>(field);
                if (value == null) continue;
                var text = value.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
            catch { }
        }

        return null;
    }

    private static DateOnly? GetDateOnly(DocumentSnapshot doc, params string[] fields)
    {
        var value = GetDateTime(doc, fields);
        return value.HasValue ? DateOnly.FromDateTime(value.Value) : null;
    }

    private static DateTime? GetUpdatedAt(DocumentSnapshot doc)
    {
        return GetDateTime(doc, "updatedAt", "healthInsuranceUpdatedAt", "modifiedAt", "createdAt");
    }

    private static DateTime? GetDateTime(DocumentSnapshot doc, params string[] fields)
    {
        foreach (var field in fields)
        {
            if (!doc.ContainsField(field)) continue;
            try
            {
                var value = doc.GetValue<object?>(field);
                switch (value)
                {
                    case Timestamp ts:
                        return ts.ToDateTime().ToLocalTime();
                    case DateTime dt:
                        return dt;
                    case string s:
                        if (TryParseDateTime(s, out var parsed)) return parsed;
                        break;
                }
            }
            catch { }
        }

        return null;
    }

    private static bool TryParseDateTime(string value, out DateTime parsed)
    {
        var text = value.Trim();
        var formats = new[]
        {
            "dd/MM/yyyy",
            "d/M/yyyy",
            "yyyy-MM-dd",
            "yyyy-M-d",
            "dd/MM/yyyy HH:mm",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-dd HH:mm:ss"
        };

        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            return true;
        }

        return DateTime.TryParse(text, new CultureInfo("vi-VN"), DateTimeStyles.None, out parsed) ||
            DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);
    }

    private static int? GetInt(DocumentSnapshot doc, params string[] fields)
    {
        foreach (var field in fields)
        {
            if (!doc.ContainsField(field)) continue;
            try
            {
                var value = doc.GetValue<object?>(field);
                return value switch
                {
                    int i => i,
                    long l => Convert.ToInt32(l),
                    double d => Convert.ToInt32(d),
                    decimal d => Convert.ToInt32(d),
                    string s when int.TryParse(s, out var parsed) => parsed,
                    _ => null
                };
            }
            catch { }
        }

        return null;
    }

    private static Gender ParseGender(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "male" or "nam" or "m" or "1" => Gender.Male,
            "female" or "nữ" or "nu" or "f" or "2" => Gender.Female,
            "other" or "khác" or "khac" or "3" => Gender.Other,
            _ => Gender.Unknown
        };
    }

    private static PatientStatus ParsePatientStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "false" or "inactive" or "disabled" or "paused" or "2" => PatientStatus.Inactive,
            "blocked" or "banned" or "3" => PatientStatus.Blocked,
            _ => PatientStatus.Active
        };
    }

    private sealed record InsuranceResolution(
        string Number,
        string PendingNumber,
        string Code,
        string Status,
        DateOnly? ExpiryDate,
        int? CoveragePercent,
        string RejectReason);
}
