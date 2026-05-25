using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;

namespace Web_Admin_Booking_App.Controllers;

public sealed class PatientsController : Controller
{
    private readonly FirestoreDb _firestore;

    public PatientsController(FirestoreDb firestore)
    {
        _firestore = firestore;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? genderFilter,
        string? statusFilter,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var allPatients = await LoadPatientsAsync(cancellationToken);

        var filtered = ApplyPatientFilters(allPatients, search, genderFilter, statusFilter).ToList();
        var pageSize = 10;
        var safePage = Math.Max(1, page);
        var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
        safePage = Math.Min(safePage, totalPages);

        var model = new PatientIndexViewModel
        {
            Search = search,
            GenderFilter = genderFilter,
            StatusFilter = statusFilter,
            Page = safePage,
            PageSize = pageSize,
            TotalCount = filtered.Count,
            ActiveCount = filtered.Count(x => x.Status == PatientStatus.Active),
            MissingInsuranceCount = filtered.Count(x => string.IsNullOrWhiteSpace(x.HealthInsuranceNumber)),
            Items = filtered
                .Skip((safePage - 1) * pageSize)
                .Take(pageSize)
                .ToList()
        };

        if (IsAjaxRequest())
        {
            return PartialView("_PatientTable", model);
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var patient = await LoadPatientDetailsAsync(id, cancellationToken);
        if (patient == null) return NotFound();

        patient.RecentAppointments = await LoadRecentAppointmentsAsync(id, cancellationToken);
        return View(patient);
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<PatientListItemViewModel>> LoadPatientsAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, PatientListItemViewModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var collectionName in new[] { "users", "Users" })
        {
            var snapshot = await _firestore.Collection(collectionName).GetSnapshotAsync(cancellationToken);
            foreach (var doc in snapshot.Documents)
            {
                var role = GetString(doc, "role", "Role")?.Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(role) && role != "patient") continue;

                var patient = MapPatientListItem(doc);
                MergePatient(result, doc.Id, patient);
            }
        }

        foreach (var collectionName in new[] { "patients", "Patients" })
        {
            var snapshot = await _firestore.Collection(collectionName).GetSnapshotAsync(cancellationToken);
            foreach (var doc in snapshot.Documents)
            {
                var userId = GetString(doc, "uid", "userId", "patientId", "UserId", "PatientId") ?? doc.Id;
                var patient = MapPatientListItem(doc);
                patient.Id = userId;
                MergePatient(result, userId, patient);
            }
        }

        return result.Values
            .OrderBy(x => x.FullName)
            .ToList();
    }

    private static void MergePatient(Dictionary<string, PatientListItemViewModel> result, string id, PatientListItemViewModel incoming)
    {
        if (!result.TryGetValue(id, out var current))
        {
            result[id] = incoming;
            return;
        }

        current.FullName = Prefer(current.FullName, incoming.FullName, "Chưa có tên");
        current.Phone = Prefer(current.Phone, incoming.Phone, "Chưa có số điện thoại");
        current.Email = Prefer(current.Email, incoming.Email);
        current.Gender = current.Gender == Gender.Unknown ? incoming.Gender : current.Gender;
        current.Dob ??= incoming.Dob;
        current.HealthInsuranceNumber = Prefer(current.HealthInsuranceNumber, incoming.HealthInsuranceNumber);
        current.HealthInsuranceStatus = Prefer(current.HealthInsuranceStatus, incoming.HealthInsuranceStatus);
        current.Status = current.Status == PatientStatus.Active ? current.Status : incoming.Status;
    }

    private static string Prefer(string? first, string? second, string fallback = "")
    {
        if (!string.IsNullOrWhiteSpace(first) && first != fallback) return first;
        if (!string.IsNullOrWhiteSpace(second)) return second;
        return fallback;
    }

    private PatientListItemViewModel MapPatientListItem(DocumentSnapshot doc)
    {
        return new PatientListItemViewModel
        {
            Id = doc.Id,
            FullName = GetString(doc, "fullName", "FullName", "name", "username", "patientName") ?? "Chưa có tên",
            Phone = GetString(doc, "phone", "phoneNumber", "Phone", "mobile", "sdt") ?? "Chưa có số điện thoại",
            Email = GetString(doc, "email", "Email") ?? string.Empty,
            Gender = ParseGender(GetString(doc, "gender", "Gender", "sex")),
            Dob = GetDateOnly(doc, "dateOfBirth", "dob", "birthDate", "birthday"),
            Status = ParsePatientStatus(GetString(doc, "status", "Status")),
            HealthInsuranceNumber = GetString(doc, "healthInsuranceNumber", "insuranceNumber", "bhyt", "BHYT") ?? string.Empty,
            HealthInsuranceStatus = GetString(doc, "healthInsuranceStatus", "insuranceStatus") ?? string.Empty
        };
    }

    private async Task<PatientDetailsViewModel?> LoadPatientDetailsAsync(string id, CancellationToken cancellationToken)
    {
        var snapshots = new List<DocumentSnapshot>();
        foreach (var collectionName in new[] { "users", "Users", "patients", "Patients" })
        {
            var snap = await _firestore.Collection(collectionName).Document(id).GetSnapshotAsync(cancellationToken);
            if (snap.Exists) snapshots.Add(snap);
        }

        if (snapshots.Count == 0) return null;

        string? Get(params string[] fields)
        {
            foreach (var snap in snapshots)
            {
                var value = GetString(snap, fields);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            return null;
        }

        DateOnly? GetDate(params string[] fields)
        {
            foreach (var snap in snapshots)
            {
                var value = GetDateOnly(snap, fields);
                if (value.HasValue) return value;
            }
            return null;
        }

        DateTime? GetDateTimeValue(params string[] fields)
        {
            foreach (var snap in snapshots)
            {
                var value = GetDateTime(snap, fields);
                if (value.HasValue) return value;
            }
            return null;
        }

        return new PatientDetailsViewModel
        {
            Id = id,
            FullName = Get("fullName", "FullName", "name", "username", "patientName") ?? "Chưa có tên",
            Username = Get("username", "Username") ?? string.Empty,
            Phone = Get("phone", "phoneNumber", "Phone", "mobile", "sdt") ?? "Chưa có số điện thoại",
            Email = Get("email", "Email") ?? string.Empty,
            Cccd = Get("cccd", "citizenId", "identityNumber", "idCard") ?? string.Empty,
            Gender = ParseGender(Get("gender", "Gender", "sex")),
            Dob = GetDate("dateOfBirth", "dob", "birthDate", "birthday"),
            Address = Get("address", "Address") ?? "Chưa cập nhật",
            BloodType = Get("bloodType", "bloodGroup") ?? string.Empty,
            Allergy = Get("allergy", "allergies") ?? string.Empty,
            ChronicDisease = Get("chronicDisease", "chronicDiseases", "medicalHistory") ?? string.Empty,
            HealthInsuranceNumber = Get("healthInsuranceNumber", "insuranceNumber", "bhyt", "BHYT") ?? string.Empty,
            HealthInsuranceStatus = Get("healthInsuranceStatus", "insuranceStatus") ?? string.Empty,
            Status = ParsePatientStatus(Get("status", "Status")),
            CreatedAt = GetDateTimeValue("createdAt", "CreatedAt"),
            UpdatedAt = GetDateTimeValue("updatedAt", "UpdatedAt", "modifiedAt"),
            LastVisitAt = GetDateTimeValue("lastVisitAt", "lastAppointmentAt"),
            Notes = Get("notes", "note", "remark") ?? string.Empty
        };
    }

    private async Task<List<PatientAppointmentSummaryViewModel>> LoadRecentAppointmentsAsync(string patientId, CancellationToken cancellationToken)
    {
        var result = new List<PatientAppointmentSummaryViewModel>();
        foreach (var collectionName in new[] { "appointments", "Appointments" })
        {
            var snapshot = await _firestore.Collection(collectionName).GetSnapshotAsync(cancellationToken);
            foreach (var doc in snapshot.Documents)
            {
                var currentPatientId = GetString(doc, "patientId", "PatientId", "userId", "UserId", "customerId");
                if (!string.Equals(currentPatientId, patientId, StringComparison.OrdinalIgnoreCase)) continue;

                result.Add(new PatientAppointmentSummaryViewModel
                {
                    Id = doc.Id,
                    DepartmentName = GetString(doc, "departmentName", "specialtyName", "SpecialtyName", "department") ?? "Chưa cập nhật",
                    DoctorName = GetString(doc, "doctorName", "DoctorName") ?? "Chưa phân bác sĩ",
                    AppointmentDate = GetDateTime(doc, "appointmentDate", "scheduledAt", "date", "createdAt"),
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

    private static IEnumerable<PatientListItemViewModel> ApplyPatientFilters(
        IEnumerable<PatientListItemViewModel> source,
        string? search,
        string? genderFilter,
        string? statusFilter)
    {
        var query = source;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.FullName.ToLowerInvariant().Contains(key) ||
                x.Phone.ToLowerInvariant().Contains(key) ||
                x.Email.ToLowerInvariant().Contains(key) ||
                x.Id.ToLowerInvariant().Contains(key) ||
                x.HealthInsuranceNumber.ToLowerInvariant().Contains(key));
        }

        if (!string.IsNullOrWhiteSpace(genderFilter) && Enum.TryParse<Gender>(genderFilter, true, out var gender))
        {
            query = query.Where(x => x.Gender == gender);
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<PatientStatus>(statusFilter, true, out var status))
        {
            query = query.Where(x => x.Status == status);
        }

        return query;
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
        var dt = GetDateTime(doc, fields);
        return dt.HasValue ? DateOnly.FromDateTime(dt.Value) : null;
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
                    case Timestamp ts: return ts.ToDateTime().ToLocalTime();
                    case DateTime dt: return dt;
                    case string s when DateTime.TryParse(s, out var parsed): return parsed;
                }
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
            "inactive" or "disabled" or "paused" or "2" => PatientStatus.Inactive,
            "blocked" or "banned" or "3" => PatientStatus.Blocked,
            _ => PatientStatus.Active
        };
    }
}
