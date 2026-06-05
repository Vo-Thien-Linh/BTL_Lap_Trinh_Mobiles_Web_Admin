using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;

namespace Web_Admin_Booking_App.Controllers;

[Authorize(Policy = "AdminOnly")]
public sealed class SpecialtiesController : Controller
{
    private readonly FirestoreDb _firestore;
    private readonly IWebHostEnvironment _environment;
    private const string PrimaryCollection = "Departments";

    public SpecialtiesController(FirestoreDb firestore, IWebHostEnvironment environment)
    {
        _firestore = firestore;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? statusFilter, int page = 1, CancellationToken cancellationToken = default)
    {
        var allSpecialties = await LoadSpecialtiesAsync(cancellationToken);
        var filterError = ValidateFilters(search, statusFilter);
        var filtered = filterError == null
            ? ApplySpecialtyFilters(allSpecialties, search, statusFilter).ToList()
            : new List<SpecialtyListItemViewModel>();
        var pageSize = 8;
        var safePage = Math.Max(1, page);
        var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
        safePage = Math.Min(safePage, totalPages);

        var now = DateTime.Now;
        var model = new SpecialtyIndexViewModel
        {
            Search = search,
            StatusFilter = statusFilter,
            FilterError = filterError,
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
        var model = await BuildUpsertModelAsync(new SpecialtyUpsertViewModel(), CancellationToken.None);
        model.CodePreview = await GetNextDepartmentCodePreviewAsync(CancellationToken.None);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SpecialtyUpsertViewModel model, CancellationToken cancellationToken = default)
    {
        NormalizeRooms(model);
        await EnforceUniqueSpecialtyAsync(model, null, CancellationToken.None);
        if (ModelState.IsValid)
        {
            await SaveDepartmentLogoAsync(model);
        }
        if (!ModelState.IsValid)
        {
            model = await BuildUpsertModelAsync(model, CancellationToken.None);
            return View(model);
        }

        try
        {
            var departmentId = await CreateSpecialtyAsync(model);
            TempData["SuccessMessage"] = $"Đã thêm chuyên khoa mới. Mã khoa: {departmentId}.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Không lưu được chuyên khoa lên Firebase: {ex.Message}");
            model = await BuildUpsertModelAsync(model, CancellationToken.None);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken = default)
    {
        var item = await FindSpecialtyAsync(id, CancellationToken.None);
        if (item == null) return NotFound();

        var model = new SpecialtyUpsertViewModel
        {
            Id = item.Id,
            Name = item.Name,
            Code = item.Code,
            CodePreview = item.Code,
            Description = item.Description,
            Phone = item.Phone,
            ImageUrl = item.ImageUrl,
            RoomNumbers = (await LoadDepartmentRoomLinesAsync(item.Id, CancellationToken.None)).ToList(),
            HeadDoctor = item.HeadDoctor,
            DoctorCount = item.DoctorCount,
            LocationNote = item.LocationNote,
            Status = item.Status,
            Icon = item.Icon
        };

        model = await BuildUpsertModelAsync(model, CancellationToken.None);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(SpecialtyUpsertViewModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Id)) return BadRequest();

        NormalizeRooms(model);
        await EnforceUniqueSpecialtyAsync(model, model.Id, CancellationToken.None);
        if (ModelState.IsValid)
        {
            await SaveDepartmentLogoAsync(model);
        }
        if (!ModelState.IsValid)
        {
            model = await BuildUpsertModelAsync(model, CancellationToken.None);
            return View(model);
        }

        var docRef = await FindSpecialtyDocumentReferenceAsync(model.Id, CancellationToken.None);
        if (docRef == null) return NotFound();

        try
        {
            await docRef.SetAsync(ToFirestorePayload(model, false), SetOptions.MergeAll, CancellationToken.None);
            TempData["SuccessMessage"] = "Đã cập nhật chuyên khoa.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Không lưu được chuyên khoa lên Firebase: {ex.Message}");
            model = await BuildUpsertModelAsync(model, CancellationToken.None);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest();

        var docRef = await FindSpecialtyDocumentReferenceAsync(id, CancellationToken.None);
        if (docRef == null) return NotFound();

        await docRef.DeleteAsync(cancellationToken: CancellationToken.None);
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
        model.DoctorSuggestions = string.IsNullOrWhiteSpace(model.Id)
            ? new List<string>()
            : await LoadDoctorSuggestionsAsync(model.Id, cancellationToken);
        return model;
    }

    private async Task<string> GetNextDepartmentCodePreviewAsync(CancellationToken cancellationToken)
    {
        var counterRef = _firestore.Collection("Counters").Document("document_codes");
        var counterSnapshot = await counterRef.GetSnapshotAsync(cancellationToken);
        var nextDepartmentNumber = Math.Max(1, GetInt(counterSnapshot, "departmentsNext") ?? 1);

        while (true)
        {
            var departmentId = FormatSequentialCode("K", nextDepartmentNumber);
            if (await IsDepartmentCodeAvailableAsync(departmentId, nextDepartmentNumber, cancellationToken))
            {
                return departmentId;
            }

            nextDepartmentNumber++;
        }
    }

    private async Task<bool> IsDepartmentCodeAvailableAsync(string departmentId, int departmentNumber, CancellationToken cancellationToken)
    {
        var departmentSnapshot = await _firestore.Collection(PrimaryCollection).Document(departmentId).GetSnapshotAsync(cancellationToken);
        if (departmentSnapshot.Exists) return false;

        var legacyDepartmentId = $"K{departmentNumber:000}";
        if (string.Equals(legacyDepartmentId, departmentId, StringComparison.OrdinalIgnoreCase)) return true;

        var legacySnapshot = await _firestore.Collection(PrimaryCollection).Document(legacyDepartmentId).GetSnapshotAsync(cancellationToken);
        return !legacySnapshot.Exists;
    }

    private async Task<List<string>> LoadDoctorSuggestionsAsync(string departmentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(departmentId)) return new List<string>();

        var userNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usersSnapshot = await _firestore.Collection("users").GetSnapshotAsync(cancellationToken);
        foreach (var doc in usersSnapshot.Documents)
        {
            var role = GetString(doc, "role", "Role")?.Trim().ToLowerInvariant();
            if (role != "doctor") continue;

            var uid = GetString(doc, "uid", "Uid", "userId", "UserId") ?? doc.Id;
            var name = GetString(doc, "fullName", "doctorName", "name", "username", "FullName");
            if (!string.IsNullOrWhiteSpace(uid) && !string.IsNullOrWhiteSpace(name))
            {
                userNames[uid] = name;
            }
        }

        foreach (var collectionName in new[] { "Doctors", "doctors" })
        {
            var doctorsSnapshot = await _firestore.Collection(collectionName)
                .WhereEqualTo("departmentId", departmentId.Trim())
                .GetSnapshotAsync(cancellationToken);

            foreach (var doctorDoc in doctorsSnapshot.Documents.Where(x => x.Exists))
            {
                var userId = GetString(doctorDoc, "userId", "UserId");
                var name = GetString(doctorDoc, "fullName", "doctorName", "name");
                if (string.IsNullOrWhiteSpace(name) &&
                    !string.IsNullOrWhiteSpace(userId) &&
                    userNames.TryGetValue(userId, out var userName))
                {
                    name = userName;
                }

                if (!string.IsNullOrWhiteSpace(name)) result.Add(name);
            }

            if (result.Count > 0) break;
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

        if (!string.IsNullOrWhiteSpace(normalizedCode) &&
            all.Any(x => !string.Equals(x.Id, currentId, StringComparison.OrdinalIgnoreCase) && NormalizeText(x.Code) == normalizedCode))
        {
            ModelState.AddModelError(nameof(model.Code), "Mã khoa này đã tồn tại.");
        }
    }

    private async Task<string> CreateSpecialtyAsync(SpecialtyUpsertViewModel model)
    {
        return await _firestore.RunTransactionAsync(async transaction =>
        {
            var now = Timestamp.GetCurrentTimestamp();
            var counterRef = _firestore.Collection("Counters").Document("document_codes");
            var counterSnapshot = await transaction.GetSnapshotAsync(counterRef, CancellationToken.None);
            var nextDepartmentNumber = Math.Max(1, GetInt(counterSnapshot, "departmentsNext") ?? 1);
            string departmentId;
            DocumentReference departmentRef;

            while (true)
            {
                departmentId = FormatSequentialCode("K", nextDepartmentNumber);
                departmentRef = _firestore.Collection(PrimaryCollection).Document(departmentId);
                var departmentSnapshot = await transaction.GetSnapshotAsync(departmentRef, CancellationToken.None);
                var legacyDepartmentId = $"K{nextDepartmentNumber:000}";
                var legacyDepartmentSnapshot = string.Equals(legacyDepartmentId, departmentId, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : await transaction.GetSnapshotAsync(_firestore.Collection(PrimaryCollection).Document(legacyDepartmentId), CancellationToken.None);
                if (!departmentSnapshot.Exists && legacyDepartmentSnapshot?.Exists != true) break;
                nextDepartmentNumber++;
            }

            model.Code = departmentId;
            transaction.Set(departmentRef, ToFirestorePayload(model, true), SetOptions.MergeAll);
            transaction.Set(counterRef, new Dictionary<string, object>
            {
                ["departmentsNext"] = nextDepartmentNumber + 1,
                ["updatedAt"] = now
            }, SetOptions.MergeAll);

            return departmentId;
        }, cancellationToken: CancellationToken.None);
    }

    private static Dictionary<string, object?> ToFirestorePayload(SpecialtyUpsertViewModel model, bool isCreate)
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = model.Name.Trim(),
            ["departmentName"] = model.Name.Trim(),
            ["description"] = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            ["phone"] = string.IsNullOrWhiteSpace(model.Phone) ? null : NormalizeDigits(model.Phone),
            ["imageUrl"] = string.IsNullOrWhiteSpace(model.ImageUrl) ? null : model.ImageUrl.Trim(),
            ["headDoctor"] = isCreate ? string.Empty : model.HeadDoctor?.Trim() ?? string.Empty,
            ["doctorCount"] = isCreate ? 0 : Math.Max(0, model.DoctorCount),
            ["location"] = model.LocationNote?.Trim() ?? string.Empty,
            ["locationNote"] = model.LocationNote?.Trim() ?? string.Empty,
            ["status"] = model.Status == SpecialtyStatus.Active ? "active" : "paused",
            ["isActive"] = model.Status == SpecialtyStatus.Active,
            ["icon"] = model.Icon.ToString(),
            ["updatedAt"] = Timestamp.GetCurrentTimestamp()
        };

        if (!string.IsNullOrWhiteSpace(model.Code))
        {
            payload["code"] = model.Code.Trim();
            payload["departmentCode"] = model.Code.Trim();
            payload["departmentId"] = model.Code.Trim();
        }

        var rooms = BuildRoomPayload(model.RoomNumbers);
        payload["rooms"] = rooms;
        payload["roomNames"] = model.RoomNumbers
            .Select(x => x?.Trim() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (isCreate)
        {
            payload["createdAt"] = Timestamp.GetCurrentTimestamp();
        }

        return payload;
    }

    private async Task<List<SpecialtyListItemViewModel>> LoadSpecialtiesAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, SpecialtyListItemViewModel>(StringComparer.OrdinalIgnoreCase);
        var doctorCounts = await LoadDoctorCountsByDepartmentAsync(cancellationToken);
        foreach (var collectionName in new[] { "Departments", "departments" })
        {
            var snapshot = await _firestore.Collection(collectionName).GetSnapshotAsync(cancellationToken);
            foreach (var doc in snapshot.Documents)
            {
                var item = MapSpecialty(doc);
                item.DoctorCount = doctorCounts.TryGetValue(item.Id, out var countById)
                    ? countById
                    : doctorCounts.TryGetValue(item.Code, out var countByCode) ? countByCode : 0;
                result[item.Id] = item;
            }
        }
        return result.Values.OrderBy(x => x.Name).ToList();
    }

    private async Task<Dictionary<string, int>> LoadDoctorCountsByDepartmentAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var collectionName in new[] { "Doctors", "doctors" })
        {
            var snapshot = await _firestore.Collection(collectionName).GetSnapshotAsync(cancellationToken);
            foreach (var doc in snapshot.Documents)
            {
                var departmentId = GetString(doc, "departmentId", "DepartmentId", "specialtyId", "SpecialtyId", "departmentCode");
                if (string.IsNullOrWhiteSpace(departmentId)) continue;
                result[departmentId] = result.TryGetValue(departmentId, out var current) ? current + 1 : 1;
            }

            if (result.Count > 0) break;
        }

        return result;
    }

    private static SpecialtyListItemViewModel MapSpecialty(DocumentSnapshot doc)
    {
        return new SpecialtyListItemViewModel
        {
            Id = doc.Id,
            Name = GetString(doc, "name", "Name", "departmentName", "specialtyName") ?? "Chưa có tên",
            Code = GetString(doc, "code", "Code", "departmentCode") ?? doc.Id,
            Description = GetString(doc, "description", "Description") ?? string.Empty,
            Phone = GetString(doc, "phone", "Phone") ?? string.Empty,
            ImageUrl = GetString(doc, "imageUrl", "ImageUrl", "image", "thumbnailUrl") ?? string.Empty,
            RoomCount = GetRoomDisplayNames(doc).Count,
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

    private async Task<IReadOnlyList<string>> LoadDepartmentRoomLinesAsync(string id, CancellationToken cancellationToken)
    {
        foreach (var collectionName in new[] { "Departments", "departments" })
        {
            var snap = await _firestore.Collection(collectionName).Document(id).GetSnapshotAsync(cancellationToken);
            if (snap.Exists) return GetRoomDisplayNames(snap);
        }

        return Array.Empty<string>();
    }

    private static void NormalizeRooms(SpecialtyUpsertViewModel model)
    {
        model.RoomNumbers = model.RoomNumbers
            .Select(x => x?.Trim() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task SaveDepartmentLogoAsync(SpecialtyUpsertViewModel model)
    {
        if (model.LogoFile is null || model.LogoFile.Length == 0) return;

        var extension = Path.GetExtension(model.LogoFile.FileName).ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowed.Contains(extension))
        {
            ModelState.AddModelError(nameof(model.LogoFile), "Logo khoa chỉ hỗ trợ JPG, PNG hoặc WEBP.");
            return;
        }

        if (model.LogoFile.Length > 2 * 1024 * 1024)
        {
            ModelState.AddModelError(nameof(model.LogoFile), "Logo khoa không được vượt quá 2MB.");
            return;
        }

        var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "departments");
        Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadsRoot, fileName);
        await using var stream = System.IO.File.Create(fullPath);
        await model.LogoFile.CopyToAsync(stream);

        model.ImageUrl = $"/uploads/departments/{fileName}";
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

    private static string? ValidateFilters(string? search, string? statusFilter)
    {
        if (!string.IsNullOrWhiteSpace(search) && search.Trim().Length > 100)
        {
            return "Từ khóa tìm kiếm không được vượt quá 100 ký tự.";
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) && !Enum.TryParse<SpecialtyStatus>(statusFilter, true, out _))
        {
            return "Bộ lọc trạng thái chuyên khoa không hợp lệ.";
        }

        return null;
    }
    private static string NormalizeText(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static string NormalizeDigits(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : new string(value.Where(char.IsDigit).ToArray());
    }

    private static string FormatSequentialCode(string prefix, int number)
    {
        return $"{prefix}{number:0000}";
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

    private static List<Dictionary<string, object>> BuildRoomPayload(IEnumerable<string> roomNumbers)
    {
        return roomNumbers
            .Select(x => x?.Trim() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(room =>
            {
                var roomNumber = room.Trim();
                return new Dictionary<string, object>
                {
                    ["roomId"] = NormalizeRoomId(roomNumber),
                    ["roomNumber"] = roomNumber,
                    ["name"] = roomNumber,
                    ["isActive"] = true
                };
            })
            .ToList();
    }

    private static IReadOnlyList<string> GetRoomDisplayNames(DocumentSnapshot doc)
    {
        foreach (var field in new[] { "rooms", "Rooms", "roomNames", "RoomNames" })
        {
            if (!doc.ContainsField(field)) continue;

            try
            {
                var value = doc.GetValue<object?>(field);
                return value switch
                {
                    IEnumerable<object> list => list
                        .Select(GetRoomDisplayName)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)
                        .ToList(),
                    string text => text
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

    private static string? GetRoomDisplayName(object? value)
    {
        if (value is null) return null;
        if (value is string text) return text.Trim();

        if (value is IDictionary<string, object> map)
        {
            return GetRoomField(map, "roomNumber")
                ?? GetRoomField(map, "name")
                ?? GetRoomField(map, "roomId");
        }

        return value.ToString()?.Trim();
    }

    private static string? GetRoomField(IDictionary<string, object> map, string fieldName)
    {
        if (!map.TryGetValue(fieldName, out var value)) return null;

        var text = value?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string NormalizeRoomId(string value)
    {
        return new string(value.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
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
