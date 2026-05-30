using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;

namespace Web_Admin_Booking_App.Controllers;

public sealed class SpecialtiesController : Controller
{
    private readonly FirestoreDb _firestore;
    private const string PrimaryCollection = "Departments";

    public SpecialtiesController(FirestoreDb firestore)
    {
        _firestore = firestore;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? statusFilter, int page = 1, CancellationToken cancellationToken = default)
    {
        var allSpecialties = await LoadSpecialtiesAsync(cancellationToken);
        var filtered = ApplySpecialtyFilters(allSpecialties, search, statusFilter).ToList();
        var pageSize = 8;
        var safePage = Math.Max(1, page);
        var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
        safePage = Math.Min(safePage, totalPages);

        var now = DateTime.Now;
        var model = new SpecialtyIndexViewModel
        {
            Search = search,
            StatusFilter = statusFilter,
            Page = safePage,
            PageSize = pageSize,
            TotalCount = filtered.Count,
            ActiveCount = filtered.Count(x => x.Status == SpecialtyStatus.Active),
            NewThisMonthCount = filtered.Count(x => x.CreatedAt.HasValue && x.CreatedAt.Value.Month == now.Month && x.CreatedAt.Value.Year == now.Year),
            NewThisMonthNames = string.Join(", ", filtered
                .Where(x => x.CreatedAt.HasValue && x.CreatedAt.Value.Month == now.Month && x.CreatedAt.Value.Year == now.Year)
                .Select(x => x.Name)
                .Take(3)),
            Items = filtered
                .Skip((safePage - 1) * pageSize)
                .Take(pageSize)
                .ToList()
        };

        if (IsAjaxRequest())
        {
            return PartialView("_SpecialtyTable", model);
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        var model = await BuildUpsertModelAsync(new SpecialtyUpsertViewModel(), cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SpecialtyUpsertViewModel model, CancellationToken cancellationToken = default)
    {
        await EnforceUniqueSpecialtyAsync(model, null, cancellationToken);
        if (!ModelState.IsValid)
        {
            model = await BuildUpsertModelAsync(model, cancellationToken);
            return View(model);
        }

        var documentId = BuildSpecialtyDocumentId(model.Code, model.Name);
        await _firestore.Collection(PrimaryCollection).Document(documentId).SetAsync(ToFirestorePayload(model, true), cancellationToken: cancellationToken);
        TempData["SuccessMessage"] = "Đã thêm chuyên khoa mới.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken = default)
    {
        var item = await FindSpecialtyAsync(id, cancellationToken);
        if (item == null) return NotFound();

        var model = new SpecialtyUpsertViewModel
        {
            Id = item.Id,
            Name = item.Name,
            Code = item.Code,
            HeadDoctor = item.HeadDoctor,
            DoctorCount = item.DoctorCount,
            LocationNote = item.LocationNote,
            Status = item.Status,
            Icon = item.Icon
        };

        model = await BuildUpsertModelAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(SpecialtyUpsertViewModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Id)) return BadRequest();

        await EnforceUniqueSpecialtyAsync(model, model.Id, cancellationToken);
        if (!ModelState.IsValid)
        {
            model = await BuildUpsertModelAsync(model, cancellationToken);
            return View(model);
        }

        var docRef = await FindSpecialtyDocumentReferenceAsync(model.Id, cancellationToken);
        if (docRef == null) return NotFound();

        await docRef.SetAsync(ToFirestorePayload(model, false), SetOptions.MergeAll, cancellationToken);
        TempData["SuccessMessage"] = "Đã cập nhật chuyên khoa.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest();

        var docRef = await FindSpecialtyDocumentReferenceAsync(id, cancellationToken);
        if (docRef == null) return NotFound();

        await docRef.DeleteAsync(cancellationToken: cancellationToken);
        TempData["SuccessMessage"] = "Đã xóa chuyên khoa.";
        return RedirectToAction(nameof(Index));
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<SpecialtyUpsertViewModel> BuildUpsertModelAsync(SpecialtyUpsertViewModel model, CancellationToken cancellationToken)
    {
        var specialties = await LoadSpecialtiesAsync(cancellationToken);
        model.ExistingNames = specialties.Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        model.ExistingCodes = specialties.Select(x => x.Code).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        model.LocationSuggestions = specialties.Select(x => x.LocationNote).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        model.DoctorSuggestions = await LoadDoctorSuggestionsAsync(cancellationToken);
        return model;
    }

    private async Task<List<string>> LoadDoctorSuggestionsAsync(CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var collectionName in new[] { "Doctors", "doctors", "Users", "users" })
        {
            var snapshot = await _firestore.Collection(collectionName).GetSnapshotAsync(cancellationToken);
            foreach (var doc in snapshot.Documents)
            {
                var role = GetString(doc, "role", "Role")?.Trim().ToLowerInvariant();
                if (collectionName.Contains("user", StringComparison.OrdinalIgnoreCase) && role != "doctor") continue;

                var name = GetString(doc, "fullName", "doctorName", "name", "username", "FullName");
                if (!string.IsNullOrWhiteSpace(name)) result.Add(name);
            }
        }
        return result.OrderBy(x => x).ToList();
    }

    private async Task EnforceUniqueSpecialtyAsync(SpecialtyUpsertViewModel model, string? currentId, CancellationToken cancellationToken)
    {
        var all = await LoadSpecialtiesAsync(cancellationToken);
        var normalizedName = NormalizeText(model.Name);
        var normalizedCode = NormalizeText(model.Code);

        if (all.Any(x => !string.Equals(x.Id, currentId, StringComparison.OrdinalIgnoreCase) && NormalizeText(x.Name) == normalizedName))
        {
            ModelState.AddModelError(nameof(model.Name), "Tên chuyên khoa này đã tồn tại.");
        }

        if (all.Any(x => !string.Equals(x.Id, currentId, StringComparison.OrdinalIgnoreCase) && NormalizeText(x.Code) == normalizedCode))
        {
            ModelState.AddModelError(nameof(model.Code), "Mã khoa này đã tồn tại.");
        }
    }

    private static Dictionary<string, object?> ToFirestorePayload(SpecialtyUpsertViewModel model, bool isCreate)
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = model.Name.Trim(),
            ["code"] = model.Code.Trim(),
            ["headDoctor"] = model.HeadDoctor.Trim(),
            ["doctorCount"] = Math.Max(0, model.DoctorCount),
            ["location"] = model.LocationNote.Trim(),
            ["locationNote"] = model.LocationNote.Trim(),
            ["status"] = model.Status == SpecialtyStatus.Active ? "active" : "paused",
            ["icon"] = model.Icon.ToString(),
            ["updatedAt"] = Timestamp.GetCurrentTimestamp()
        };

        if (isCreate)
        {
            payload["createdAt"] = Timestamp.GetCurrentTimestamp();
        }

        return payload;
    }

    private async Task<List<SpecialtyListItemViewModel>> LoadSpecialtiesAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, SpecialtyListItemViewModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var collectionName in new[] { "Departments", "departments" })
        {
            var snapshot = await _firestore.Collection(collectionName).GetSnapshotAsync(cancellationToken);
            foreach (var doc in snapshot.Documents)
            {
                var item = MapSpecialty(doc);
                result[item.Id] = item;
            }
        }
        return result.Values.OrderBy(x => x.Name).ToList();
    }

    private static SpecialtyListItemViewModel MapSpecialty(DocumentSnapshot doc)
    {
        return new SpecialtyListItemViewModel
        {
            Id = doc.Id,
            Name = GetString(doc, "name", "Name", "departmentName", "specialtyName") ?? "Chưa có tên",
            Code = GetString(doc, "code", "Code", "departmentCode") ?? doc.Id,
            HeadDoctor = GetString(doc, "headDoctor", "HeadDoctor", "managerName", "leaderName") ?? "Chưa phân công",
            DoctorCount = GetInt(doc, "doctorCount", "DoctorCount", "totalDoctors") ?? 0,
            LocationNote = GetString(doc, "location", "Location", "locationNote", "floor") ?? "Chưa cập nhật",
            Status = ParseSpecialtyStatus(GetString(doc, "status", "Status")),
            Icon = ParseSpecialtyIcon(GetString(doc, "icon", "Icon")),
            CreatedAt = GetDateTime(doc, "createdAt", "CreatedAt")
        };
    }

    private async Task<SpecialtyListItemViewModel?> FindSpecialtyAsync(string id, CancellationToken cancellationToken)
    {
        foreach (var collectionName in new[] { "Departments", "departments" })
        {
            var snap = await _firestore.Collection(collectionName).Document(id).GetSnapshotAsync(cancellationToken);
            if (snap.Exists) return MapSpecialty(snap);
        }
        return null;
    }

    private async Task<DocumentReference?> FindSpecialtyDocumentReferenceAsync(string id, CancellationToken cancellationToken)
    {
        foreach (var collectionName in new[] { "Departments", "departments" })
        {
            var docRef = _firestore.Collection(collectionName).Document(id);
            var snap = await docRef.GetSnapshotAsync(cancellationToken);
            if (snap.Exists) return docRef;
        }
        return null;
    }

    private static IEnumerable<SpecialtyListItemViewModel> ApplySpecialtyFilters(IEnumerable<SpecialtyListItemViewModel> source, string? search, string? statusFilter)
    {
        var query = source;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Name.ToLowerInvariant().Contains(key) ||
                x.Code.ToLowerInvariant().Contains(key) ||
                x.HeadDoctor.ToLowerInvariant().Contains(key) ||
                x.LocationNote.ToLowerInvariant().Contains(key));
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<SpecialtyStatus>(statusFilter, true, out var status))
        {
            query = query.Where(x => x.Status == status);
        }

        return query;
    }

    private static string BuildSpecialtyDocumentId(string code, string name)
    {
        var source = !string.IsNullOrWhiteSpace(code) ? code : name;
        var safe = new string(source.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        while (safe.Contains("__", StringComparison.Ordinal)) safe = safe.Replace("__", "_", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(safe) ? Guid.NewGuid().ToString("N") : safe.Trim('_');
    }

    private static string NormalizeText(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static string? GetString(DocumentSnapshot doc, params string[] fields)
    {
        foreach (var field in fields)
        {
            if (!doc.ContainsField(field)) continue;
            try
            {
                var value = doc.GetValue<object?>(field);
                var text = value?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
            catch { }
        }
        return null;
    }

    private static int? GetInt(DocumentSnapshot doc, params string[] fields)
    {
        foreach (var field in fields)
        {
            if (!doc.ContainsField(field)) continue;
            try
            {
                var value = doc.GetValue<object?>(field);
                switch (value)
                {
                    case int i: return i;
                    case long l: return (int)l;
                    case double d: return (int)d;
                    case string s when int.TryParse(s, out var parsed): return parsed;
                }
            }
            catch { }
        }
        return null;
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

    private static SpecialtyStatus ParseSpecialtyStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "paused" or "inactive" or "disabled" or "tam_ngung" or "tạm ngưng" => SpecialtyStatus.Paused,
            _ => SpecialtyStatus.Active
        };
    }

    private static SpecialtyIcon ParseSpecialtyIcon(string? value)
    {
        return Enum.TryParse<SpecialtyIcon>(value, true, out var icon) ? icon : SpecialtyIcon.General;
    }
}
