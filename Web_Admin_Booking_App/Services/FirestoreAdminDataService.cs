using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;
using System.Globalization;
using Web_Admin_Booking_App.Models;

namespace Web_Admin_Booking_App.Services;

public sealed class FirestoreAdminDataService
{
    private readonly FirestoreDb _firestore;
    private readonly FirebaseSettings _settings;
    private readonly CodeGeneratorService _codeGenerator;

    public FirestoreAdminDataService(
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
        var docs = await GetFirstAvailableCollectionAsync(_settings.UserCollections, cancellationToken);
        var result = new List<PatientListItemViewModel>();
        var addedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var doc in docs.Documents)
        {
            var role = GetString(doc, "role", "Role")?.ToLowerInvariant();
            if (role != "patient") continue;

            result.Add(new PatientListItemViewModel
            {
                Id = doc.Id,
                Code = GetString(doc, "userCode", "patientCode", "code") ?? doc.Id,
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

        foreach (var collectionName in new[] { "patients", "Patients" })
        {
            var patientDocs = await _firestore.Collection(collectionName).GetSnapshotAsync(cancellationToken);
            foreach (var doc in patientDocs.Documents)
            {
                var linkedId = GetString(doc, "uid", "Uid", "userId", "UserId", "patientId", "PatientId") ?? doc.Id;
                if (!addedIds.Add(linkedId)) continue;

                result.Add(new PatientListItemViewModel
                {
                    Id = doc.Id,
                    Code = GetString(doc, "userCode", "patientCode", "code") ?? linkedId,
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
        }

        return result;
    }

    public async Task<UserUniqueCheckResult> CheckUserUniqueFieldsAsync(
        string email,
        string phone,
        string cccd,
        string? ignoredUserId = null,
        CancellationToken cancellationToken = default)
    {
        var docs = await GetFirstAvailableCollectionAsync(_settings.UserCollections, cancellationToken);
        var normalizedEmail = NormalizeEmail(email);
        var normalizedPhone = NormalizeDigits(phone);
        var normalizedCccd = NormalizeDigits(cccd);
        var ignored = string.IsNullOrWhiteSpace(ignoredUserId) ? null : ignoredUserId.Trim();

        var result = new UserUniqueCheckResult();

        foreach (var doc in docs.Documents)
        {
            if (!string.IsNullOrWhiteSpace(ignored) &&
                string.Equals(doc.Id, ignored, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var docEmail = NormalizeEmail(GetString(doc, "email", "Email"));
            if (!string.IsNullOrWhiteSpace(normalizedEmail) &&
                string.Equals(docEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                result.EmailExists = true;
            }

            var docPhone = NormalizeDigits(GetString(doc, "phone", "phoneNumber", "Phone", "mobile", "sdt"));
            if (!string.IsNullOrWhiteSpace(normalizedPhone) && docPhone == normalizedPhone)
            {
                result.PhoneExists = true;
            }

            var docCccd = NormalizeDigits(GetString(doc, "cccd", "citizenId", "identityNumber", "idCard"));
            if (!string.IsNullOrWhiteSpace(normalizedCccd) && docCccd == normalizedCccd)
            {
                result.CccdExists = true;
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<SelectOption>> GetDepartmentOptionsAsync(CancellationToken cancellationToken = default)
    {
        var departments = await LoadDepartmentNamesAsync(cancellationToken);
        return departments
            .OrderBy(x => x.Value)
            .Select(x => new SelectOption { Value = x.Key, Text = x.Value })
            .ToList();
    }

    public async Task<IReadOnlyList<StaffListItemViewModel>> GetStaffAsync(CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, StaffListItemViewModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var collectionName in _settings.UserCollections.DefaultIfEmpty("users"))
        {
            var snapshot = await _firestore.Collection(collectionName).GetSnapshotAsync(cancellationToken);
            foreach (var doc in snapshot.Documents)
            {
                var role = NormalizeStaffRole(GetString(doc, "role", "Role"));
                if (role != "staff") continue;

                result[doc.Id] = new StaffListItemViewModel
                {
                    Id = doc.Id,
                    StaffCode = GetString(doc, "staffCode", "employeeCode", "userCode", "code") ?? doc.Id,
                    FullName = GetString(doc, "fullName", "FullName", "name", "username") ?? "Nhân viên",
                    Email = GetString(doc, "email", "Email") ?? string.Empty,
                    Phone = GetString(doc, "phone", "phoneNumber", "Phone") ?? string.Empty,
                    Role = role,
                    StaffType = GetString(doc, "staffType", "type") ?? "general",
                    Position = GetString(doc, "position", "jobTitle") ?? string.Empty,
                    Status = GetString(doc, "status", "Status") ?? string.Empty
                };
            }
        }

        foreach (var collectionName in new[] { "Staffs", "staffs", "Employees", "employees" })
        {
            var snapshot = await _firestore.Collection(collectionName).GetSnapshotAsync(cancellationToken);
            foreach (var doc in snapshot.Documents)
            {
                var uid = GetString(doc, "uid", "userId", "UserId") ?? doc.Id;
                if (result.ContainsKey(uid)) continue;

                result[uid] = new StaffListItemViewModel
                {
                    Id = uid,
                    StaffCode = GetString(doc, "staffCode", "employeeCode", "code") ?? doc.Id,
                    FullName = GetString(doc, "fullName", "FullName", "name") ?? "Nhân viên",
                    Email = GetString(doc, "email", "Email") ?? string.Empty,
                    Phone = GetString(doc, "phone", "phoneNumber", "Phone") ?? string.Empty,
                    Role = NormalizeStaffRole(GetString(doc, "role", "Role")) is { Length: > 0 } fallbackRole ? fallbackRole : "staff",
                    StaffType = GetString(doc, "staffType", "type") ?? "general",
                    Position = GetString(doc, "position", "jobTitle") ?? string.Empty,
                    Status = GetString(doc, "status", "Status") ?? string.Empty
                };
            }
        }

        return result.Values
            .OrderBy(x => x.StaffCode)
            .ThenBy(x => x.FullName)
            .ToList();
    }

    public async Task<string> CreateStaffAsync(
        StaffCreateViewModel model,
        string uid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            throw new ArgumentException("UID Firebase Auth không được để trống.", nameof(uid));
        }

        return await _firestore.RunTransactionAsync(async transaction =>
        {
            var now = Timestamp.GetCurrentTimestamp();
            var staffCode = await _codeGenerator.GenerateNextCodeAsync(transaction, "staff");
            var normalizedRole = "staff";
            var normalizedStatus = NormalizeStaffStatus(model.Status);

            var userRef = _firestore.Collection(_settings.UserCollections.First()).Document(uid);
            var staffRef = _firestore.Collection("Staffs").Document(uid);

            var payload = new Dictionary<string, object?>
            {
                ["uid"] = uid,
                ["userId"] = uid,
                ["userCode"] = staffCode,
                ["staffCode"] = staffCode,
                ["employeeCode"] = staffCode,
                ["fullName"] = model.FullName.Trim(),
                ["email"] = NormalizeEmail(model.Email),
                ["phone"] = NormalizeDigits(model.Phone),
                ["cccd"] = NormalizeDigits(model.Cccd),
                ["role"] = normalizedRole,
                ["staffType"] = string.IsNullOrWhiteSpace(model.StaffType) ? "general" : model.StaffType.Trim().ToLowerInvariant(),
                ["position"] = string.IsNullOrWhiteSpace(model.Position) ? "Nhân viên quầy" : model.Position.Trim(),
                ["status"] = normalizedStatus,
                ["createdAt"] = now,
                ["updatedAt"] = now
            };

            transaction.Set(userRef, payload, SetOptions.MergeAll);
            transaction.Set(staffRef, payload, SetOptions.MergeAll);

            return staffCode;
        }, cancellationToken: CancellationToken.None);
    }

    public async Task<string> CreateDoctorAsync(
        DoctorCreateViewModel model,
        string uid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            throw new ArgumentException("UID Firebase Auth không được để trống.", nameof(uid));
        }

        return await _firestore.RunTransactionAsync(async transaction =>
        {
            var now = Timestamp.GetCurrentTimestamp();
            var doctorCode = await _codeGenerator.GenerateNextCodeAsync(transaction, "doctors");
            var doctorRef = _firestore.Collection(_settings.DoctorCollections.First()).Document(uid);

            var userRef = _firestore.Collection(_settings.UserCollections.First()).Document(uid);
            transaction.Set(userRef, new Dictionary<string, object?>
            {
                ["uid"] = uid,
                ["userCode"] = doctorCode,
                ["doctorCode"] = doctorCode,
                ["email"] = NormalizeEmail(model.Email),
                ["fullName"] = model.FullName.Trim(),
                ["phone"] = NormalizeDigits(model.Phone),
                ["cccd"] = NormalizeDigits(model.Cccd),
                ["avatarUrl"] = string.IsNullOrWhiteSpace(model.AvatarUrl) ? null : model.AvatarUrl.Trim(),
                ["gender"] = string.IsNullOrWhiteSpace(model.Gender) ? null : model.Gender.Trim(),
                ["dateOfBirth"] = model.DateOfBirth?.ToString("yyyy-MM-dd"),
                ["role"] = "doctor",
                ["status"] = string.IsNullOrWhiteSpace(model.UserStatus) ? "pending" : model.UserStatus.Trim(),
                ["emailVerified"] = model.EmailVerified,
                ["createdAt"] = now,
                ["updatedAt"] = now
            });

            transaction.Set(doctorRef, new Dictionary<string, object?>
            {
                ["doctorCode"] = doctorCode,
                ["userId"] = uid,
                ["departmentId"] = model.DepartmentId.Trim(),
                ["specialization"] = model.Specialization.Trim(),
                ["licenseNumber"] = model.LicenseNumber.Trim(),
                ["degree"] = string.IsNullOrWhiteSpace(model.Degree) ? null : model.Degree.Trim(),
                ["yearsOfExperience"] = model.YearsOfExperience ?? 0,
                ["consultationFee"] = Convert.ToInt64(model.ConsultationFee ?? 0),
                ["biography"] = string.IsNullOrWhiteSpace(model.Biography) ? null : model.Biography.Trim(),
                ["isActive"] = model.IsActive,
                ["isFeatured"] = model.IsFeatured,
                ["featuredRank"] = model.IsFeatured ? model.FeaturedRank : null,
                ["verificationStatus"] = string.IsNullOrWhiteSpace(model.VerificationStatus) ? "pending" : model.VerificationStatus.Trim(),
                ["createdAt"] = now,
                ["updatedAt"] = now
            });

            return doctorCode;
        }, cancellationToken: CancellationToken.None);
    }

    public async Task<IReadOnlyList<DoctorListItemViewModel>> GetDoctorsAsync(CancellationToken cancellationToken = default)
    {
        var docs = await GetFirstAvailableCollectionAsync(_settings.DoctorCollections, cancellationToken);
        var users = await GetFirstAvailableCollectionAsync(_settings.UserCollections, cancellationToken);
        var departments = await LoadDepartmentNamesAsync(cancellationToken);
        var result = new List<DoctorListItemViewModel>();

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
                DoctorCode = GetString(doc, "doctorCode", "code") ?? doc.Id,
                UserId = GetString(doc, "userId", "UserId"),
                FullName = GetStringFromSources(sources, "fullName", "FullName", "doctorName", "name", "username", "Username") ?? $"Bác sĩ {doc.Id}",
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
                IsFeatured = GetBool(doc, "isFeatured", "IsFeatured"),
                FeaturedRank = GetNullableInt(doc, "featuredRank", "FeaturedRank"),
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
            IsFeatured = GetBool(snapshot, "isFeatured", "IsFeatured"),
            FeaturedRank = GetNullableInt(snapshot, "featuredRank", "FeaturedRank"),
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

    public async Task<DoctorEditViewModel?> GetDoctorEditAsync(string doctorDocumentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(doctorDocumentId)) return null;

        var snapshot = await GetFirstExistingDocumentAsync(_settings.DoctorCollections, doctorDocumentId, cancellationToken);
        if (snapshot is null || !snapshot.Exists) return null;

        var users = await GetFirstAvailableCollectionAsync(_settings.UserCollections, cancellationToken);
        var user = FindLinkedDoctorUser(snapshot, users.Documents);
        var sources = new DocumentSnapshot?[] { snapshot, user };

        return new DoctorEditViewModel
        {
            DocumentId = snapshot.Id,
            UserId = GetString(snapshot, "userId", "UserId") ?? user?.Id ?? string.Empty,
            FullName = GetStringFromSources(sources, "fullName", "FullName", "doctorName", "name", "username", "Username") ?? string.Empty,
            Email = GetStringFromSources(new DocumentSnapshot?[] { user }, "email", "Email") ?? string.Empty,
            Phone = GetStringFromSources(sources, "phone", "phoneNumber", "phone_number", "mobile", "mobileNumber", "contactPhone", "sdt", "soDienThoai", "Phone") ?? string.Empty,
            Cccd = GetStringFromSources(new DocumentSnapshot?[] { user }, "cccd", "citizenId", "identityNumber", "idCard") ?? string.Empty,
            AvatarUrl = GetStringFromSources(sources, "avatarUrl", "AvatarUrl", "photoUrl", "profileImage", "imageUrl"),
            Gender = GetStringFromSources(sources, "gender", "Gender", "sex"),
            DateOfBirth = GetDateTimeFromSources(sources, "dateOfBirth", "dob", "birthDate", "birthday"),
            UserStatus = GetStringFromSources(new DocumentSnapshot?[] { user }, "status", "Status") ?? "active",
            EmailVerified = user is not null && GetBool(user, "emailVerified", "EmailVerified") == true,
            DepartmentId = GetString(snapshot, "departmentId", "DepartmentId") ?? string.Empty,
            Specialization = GetString(snapshot, "specialization", "Specialization") ?? string.Empty,
            LicenseNumber = GetString(snapshot, "licenseNumber", "medicalLicense", "license", "certificateNumber") ?? string.Empty,
            Degree = GetString(snapshot, "degree", "Degree", "academicDegree", "qualification", "qualifications", "education"),
            YearsOfExperience = GetNullableInt(snapshot, "yearsOfExperience", "experienceYears", "experience", "workExperience"),
            ConsultationFee = GetDecimal(snapshot, "consultationFee", "fee", "price", "examinationFee"),
            Biography = GetStringFromSources(sources, "bio", "biography", "description", "about", "introduction"),
            IsActive = GetBool(snapshot, "isActive", "IsActive") ?? true,
            IsFeatured = GetBool(snapshot, "isFeatured", "IsFeatured") ?? false,
            FeaturedRank = GetNullableInt(snapshot, "featuredRank", "FeaturedRank"),
            VerificationStatus = GetString(snapshot, "verificationStatus", "VerificationStatus") ?? "verified"
        };
    }

    public async Task UpdateDoctorAsync(DoctorEditViewModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.DocumentId))
        {
            throw new ArgumentException("Mã hồ sơ bác sĩ không được để trống.", nameof(model));
        }

        var doctorSnapshot = await GetFirstExistingDocumentAsync(_settings.DoctorCollections, model.DocumentId.Trim(), cancellationToken);
        if (doctorSnapshot is null || !doctorSnapshot.Exists)
        {
            throw new InvalidOperationException("Không tìm thấy hồ sơ bác sĩ cần sửa.");
        }

        var userId = string.IsNullOrWhiteSpace(model.UserId)
            ? GetString(doctorSnapshot, "userId", "UserId")
            : model.UserId.Trim();
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("Hồ sơ bác sĩ thiếu userId nên không thể cập nhật users.");
        }

        var now = Timestamp.GetCurrentTimestamp();
        var batch = _firestore.StartBatch();
        var userRef = _firestore.Collection(_settings.UserCollections.First()).Document(userId);
        var doctorRef = _firestore.Collection(_settings.DoctorCollections.First()).Document(doctorSnapshot.Id);

        batch.Set(userRef, new Dictionary<string, object?>
        {
            ["uid"] = userId,
            ["fullName"] = model.FullName.Trim(),
            ["phone"] = NormalizeDigits(model.Phone),
            ["cccd"] = NormalizeDigits(model.Cccd),
            ["avatarUrl"] = string.IsNullOrWhiteSpace(model.AvatarUrl) ? null : model.AvatarUrl.Trim(),
            ["gender"] = string.IsNullOrWhiteSpace(model.Gender) ? null : model.Gender.Trim(),
            ["dateOfBirth"] = model.DateOfBirth?.ToString("yyyy-MM-dd"),
            ["role"] = "doctor",
            ["status"] = string.IsNullOrWhiteSpace(model.UserStatus) ? "active" : model.UserStatus.Trim(),
            ["emailVerified"] = model.EmailVerified,
            ["updatedAt"] = now
        }, SetOptions.MergeAll);

        batch.Set(doctorRef, new Dictionary<string, object?>
        {
            ["userId"] = userId,
            ["departmentId"] = model.DepartmentId.Trim(),
            ["specialization"] = string.IsNullOrWhiteSpace(model.Specialization) ? null : model.Specialization.Trim(),
            ["licenseNumber"] = string.IsNullOrWhiteSpace(model.LicenseNumber) ? null : model.LicenseNumber.Trim(),
            ["degree"] = string.IsNullOrWhiteSpace(model.Degree) ? null : model.Degree.Trim(),
            ["yearsOfExperience"] = model.YearsOfExperience ?? 0,
            ["consultationFee"] = Convert.ToInt64(model.ConsultationFee ?? 0),
            ["biography"] = string.IsNullOrWhiteSpace(model.Biography) ? null : model.Biography.Trim(),
            ["isActive"] = model.IsActive,
            ["isFeatured"] = model.IsFeatured,
            ["featuredRank"] = model.IsFeatured ? model.FeaturedRank : null,
            ["verificationStatus"] = string.IsNullOrWhiteSpace(model.VerificationStatus) ? "verified" : model.VerificationStatus.Trim(),
            ["updatedAt"] = now
        }, SetOptions.MergeAll);

        await batch.CommitAsync(CancellationToken.None);
    }

    public async Task UpdateDoctorVerificationAsync(
        string doctorDocumentId,
        string verificationStatus,
        string? rejectReason = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(doctorDocumentId)) return;

        var doctorSnapshot = await GetFirstExistingDocumentAsync(_settings.DoctorCollections, doctorDocumentId, cancellationToken);
        if (doctorSnapshot is null || !doctorSnapshot.Exists) return;

        var userId = GetString(doctorSnapshot, "userId", "UserId");
        var now = Timestamp.GetCurrentTimestamp();
        var normalizedStatus = verificationStatus.Trim().ToLowerInvariant();
        var isVerified = normalizedStatus == "verified";

        var batch = _firestore.StartBatch();
        var doctorRef = _firestore.Collection(_settings.DoctorCollections.First()).Document(doctorSnapshot.Id);
        var doctorPayload = new Dictionary<string, object?>
        {
            ["verificationStatus"] = normalizedStatus,
            ["isActive"] = isVerified,
            ["updatedAt"] = now
        };

        if (normalizedStatus == "rejected")
        {
            doctorPayload["rejectReason"] = string.IsNullOrWhiteSpace(rejectReason) ? "Hồ sơ không được duyệt." : rejectReason.Trim();
        }
        else
        {
            doctorPayload["rejectReason"] = FieldValue.Delete;
        }

        batch.Update(doctorRef, doctorPayload);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var userRef = _firestore.Collection(_settings.UserCollections.First()).Document(userId);
            batch.Update(userRef, new Dictionary<string, object>
            {
                ["status"] = isVerified ? "active" : "blocked",
                ["updatedAt"] = now
            });
        }

        await batch.CommitAsync(CancellationToken.None);
    }

    public async Task<IReadOnlyList<AppointmentListItemViewModel>> GetAppointmentsAsync(CancellationToken cancellationToken = default)
    {
        var docs = await GetFirstAvailableCollectionAsync(_settings.AppointmentCollections, cancellationToken);
        var users = await LoadAppointmentUsersAsync(cancellationToken);
        var departments = await LoadDepartmentNamesAsync(cancellationToken);
        var doctors = await LoadAppointmentDoctorsAsync(cancellationToken);
        var result = new List<AppointmentListItemViewModel>();

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
                ScheduleId = GetString(doc, "scheduleId", "ScheduleId") ?? string.Empty,
                PatientId = patientId,
                PatientCode = GetString(doc, "patientCode", "userCode") ?? patient?.Code ?? patientId ?? string.Empty,
                PatientPhone = patient?.Phone
                    ?? GetString(doc, "patientPhone", "phone", "phoneNumber", "PatientPhone") ?? string.Empty,
                PatientEmail = patient?.Email
                    ?? GetString(doc, "patientEmail", "email", "PatientEmail") ?? string.Empty,
                DoctorId = doctorId,
                DoctorCode = GetString(doc, "doctorCode") ?? doctor?.DocumentId ?? doctorId ?? string.Empty,
                DoctorPhone = doctor?.Phone ?? string.Empty,
                DoctorEmail = doctor?.Email ?? string.Empty,
                DepartmentId = departmentId ?? string.Empty,
                RoomName = GetString(doc, "roomNumber", "roomName", "room", "clinicRoom", "location") ?? string.Empty,
                QueueNumber = GetInt(doc, "queueNumber", "QueueNumber"),
                AppointmentType = FormatAppointmentType(GetString(doc, "type", "appointmentType", "consultationType")),
                Duration = FormatDuration(GetString(doc, "duration", "durationMinutes", "slotDuration")),
                ConsultationFee = FormatConsultationFee(fee) ?? string.Empty,
                PaymentStatus = FormatPaymentStatus(GetString(doc, "paymentStatus", "payment_state", "PaymentStatus")),
                CancelReason = GetString(doc, "cancelReason", "cancellationReason", "cancelledReason") ?? string.Empty,
                CancelRequestedBy = GetString(doc, "cancelRequestedBy", "CancelRequestedBy") ?? string.Empty,
                CancelRequestedAt = GetDateTime(doc, "cancelRequestedAt", "CancelRequestedAt"),
                CancelledBy = GetString(doc, "cancelledBy", "canceledBy", "CancelledBy", "CanceledBy") ?? string.Empty,
                CancelledAt = GetDateTime(doc, "cancelledAt", "canceledAt", "CancelledAt"),
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

    public async Task ApproveAppointmentCancelRequestAsync(
        string appointmentId,
        string adminIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appointmentId))
        {
            throw new ArgumentException("Mã lịch hẹn không được để trống.", nameof(appointmentId));
        }

        var appointments = _settings.AppointmentCollections
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var schedules = _settings.DoctorScheduleCollections
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (appointments.Length == 0 || schedules.Length == 0)
        {
            throw new InvalidOperationException("Thiếu cấu hình collection Appointments hoặc DoctorSchedules.");
        }

        string? notificationPatientId = null;
        string? notificationDoctorId = null;
        string notificationAppointmentCode = appointmentId.Trim();

        await _firestore.RunTransactionAsync(async transaction =>
        {
            var appointmentRef = _firestore.Collection(appointments[0]).Document(appointmentId.Trim());
            var appointmentSnapshot = await transaction.GetSnapshotAsync(appointmentRef, CancellationToken.None);
            if (!appointmentSnapshot.Exists)
            {
                throw new InvalidOperationException("Không tìm thấy lịch hẹn cần duyệt hủy.");
            }

            var currentStatus = GetString(appointmentSnapshot, "status", "Status")?.Trim().ToLowerInvariant();
            if (currentStatus != "cancel_requested")
            {
                throw new InvalidOperationException("Lịch hẹn này không ở trạng thái chờ duyệt hủy hoặc đã được xử lý.");
            }

            var scheduleId = GetString(appointmentSnapshot, "scheduleId", "ScheduleId");
            if (string.IsNullOrWhiteSpace(scheduleId))
            {
                throw new InvalidOperationException("Lịch hẹn thiếu scheduleId nên không thể trả slot chính xác.");
            }

            var scheduleRef = _firestore.Collection(schedules[0]).Document(scheduleId.Trim());
            var scheduleSnapshot = await transaction.GetSnapshotAsync(scheduleRef, CancellationToken.None);
            if (!scheduleSnapshot.Exists)
            {
                throw new InvalidOperationException("Không tìm thấy DoctorSchedules tương ứng để trả slot.");
            }

            var availableSlots = Math.Max(0, GetInt(scheduleSnapshot, "remainingSlots", "availableSlots", "AvailableSlots", "slots"));
            var maxSlots = GetInt(scheduleSnapshot, "slotCapacity", "maxSlots", "MaxSlots");
            var bookedSlots = Math.Max(0, GetInt(scheduleSnapshot, "bookedSlots", "BookedSlots"));
            var nextAvailableSlots = maxSlots > 0
                ? Math.Min(availableSlots + 1, maxSlots)
                : availableSlots + 1;
            var nextBookedSlots = Math.Max(0, bookedSlots - 1);
            var now = Timestamp.GetCurrentTimestamp();
            var patientId = GetString(appointmentSnapshot, "patientId", "PatientId", "userId", "UserId")?.Trim();
            notificationPatientId = patientId;
            notificationDoctorId = GetString(appointmentSnapshot, "doctorUserId", "DoctorUserId", "doctorId", "DoctorId")?.Trim();
            notificationAppointmentCode = GetString(appointmentSnapshot, "appointmentCode", "code", "bookingCode") ?? notificationAppointmentCode;
            var appointmentDate = GetDateTime(appointmentSnapshot, "appointmentDate", "scheduledAt", "AppointmentDate");
            var shiftId = GetString(appointmentSnapshot, "shiftId", "ShiftId")?.Trim();

            transaction.Update(appointmentRef, new Dictionary<string, object?>
            {
                ["status"] = "cancelled",
                ["cancelRequestStatus"] = "approved",
                ["cancelledBy"] = string.IsNullOrWhiteSpace(adminIdentifier) ? "admin" : adminIdentifier.Trim(),
                ["cancelledAt"] = now,
                ["cancelReviewedByStaffId"] = string.IsNullOrWhiteSpace(adminIdentifier) ? "admin" : adminIdentifier.Trim(),
                ["cancelReviewedAt"] = now,
                ["updatedAt"] = now
            });

            transaction.Update(scheduleRef, new Dictionary<string, object>
            {
                ["availableSlots"] = nextAvailableSlots,
                ["remainingSlots"] = nextAvailableSlots,
                ["bookedSlots"] = nextBookedSlots,
                ["slotCapacity"] = maxSlots > 0 ? maxSlots : nextAvailableSlots,
                ["maxSlots"] = maxSlots > 0 ? maxSlots : nextAvailableSlots,
                ["updatedAt"] = now
            });

            if (!string.IsNullOrWhiteSpace(patientId) &&
                appointmentDate.HasValue &&
                !string.IsNullOrWhiteSpace(shiftId))
            {
                var lockRef = _firestore
                    .Collection("PatientScheduleLocks")
                    .Document(BuildPatientScheduleLockId(patientId, appointmentDate.Value, shiftId));
                transaction.Set(lockRef, new Dictionary<string, object?>
                {
                    ["patientId"] = patientId,
                    ["appointmentId"] = appointmentId,
                    ["appointmentDate"] = Timestamp.FromDateTime(DateTime.SpecifyKind(appointmentDate.Value.Date, DateTimeKind.Utc)),
                    ["shiftId"] = shiftId,
                    ["status"] = "cancelled",
                    ["updatedAt"] = now
                }, SetOptions.MergeAll);
            }
        }, cancellationToken: CancellationToken.None);

        if (!string.IsNullOrWhiteSpace(notificationPatientId))
        {
            await CreateBasicNotificationAsync(
                notificationPatientId,
                "patient",
                "Lịch hẹn đã được hủy",
                $"Yêu cầu hủy lịch {notificationAppointmentCode} đã được duyệt.",
                "appointment_cancelled",
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(notificationDoctorId))
        {
            await CreateBasicNotificationAsync(
                notificationDoctorId,
                "doctor",
                "Lịch hẹn đã bị hủy",
                $"Lịch hẹn {notificationAppointmentCode} đã được hủy theo yêu cầu của bệnh nhân.",
                "appointment_cancelled",
                cancellationToken);
        }
    }

    public async Task MarkAppointmentCompletedAsync(
        string appointmentId,
        string adminIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appointmentId))
        {
            throw new ArgumentException("Mã lịch hẹn không được để trống.", nameof(appointmentId));
        }

        var appointments = _settings.AppointmentCollections
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (appointments.Length == 0)
        {
            throw new InvalidOperationException("Thiếu cấu hình collection Appointments.");
        }

        await _firestore.RunTransactionAsync(async transaction =>
        {
            var appointmentRef = _firestore.Collection(appointments[0]).Document(appointmentId.Trim());
            var appointmentSnapshot = await transaction.GetSnapshotAsync(appointmentRef, CancellationToken.None);
            if (!appointmentSnapshot.Exists)
            {
                throw new InvalidOperationException("Không tìm thấy lịch hẹn cần cập nhật.");
            }

            var currentStatus = GetString(appointmentSnapshot, "status", "Status")?.Trim().ToLowerInvariant();
            if (currentStatus is "cancelled" or "canceled" or "cancel_requested" or "completed" or "no_show")
            {
                throw new InvalidOperationException("Không thể chuyển lịch hẹn đã hủy, đang chờ hủy, đã khám hoặc không đến sang đã khám.");
            }

            var doctorId = GetString(appointmentSnapshot, "doctorId", "DoctorId");
            if (string.IsNullOrWhiteSpace(doctorId))
            {
                throw new InvalidOperationException("Lịch hẹn thiếu doctorId nên app không thể tính số lượt khám hoàn tất.");
            }

            var now = Timestamp.GetCurrentTimestamp();
            transaction.Update(appointmentRef, new Dictionary<string, object?>
            {
                ["status"] = "completed",
                ["completedBy"] = string.IsNullOrWhiteSpace(adminIdentifier) ? "admin" : adminIdentifier.Trim(),
                ["completedAt"] = now,
                ["updatedAt"] = now
            });
        }, cancellationToken: CancellationToken.None);
    }

    public async Task RejectAppointmentCancelRequestAsync(
        string appointmentId,
        string adminIdentifier,
        string? rejectReason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appointmentId))
        {
            throw new ArgumentException("Mã lịch hẹn không được để trống.", nameof(appointmentId));
        }

        var appointments = _settings.AppointmentCollections
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (appointments.Length == 0)
        {
            throw new InvalidOperationException("Thiếu cấu hình collection Appointments.");
        }

        string? notificationPatientId = null;
        string notificationAppointmentCode = appointmentId.Trim();
        string notificationRejectReason = string.IsNullOrWhiteSpace(rejectReason)
            ? "Yêu cầu hủy lịch không được duyệt."
            : rejectReason.Trim();

        await _firestore.RunTransactionAsync(async transaction =>
        {
            var appointmentRef = _firestore.Collection(appointments[0]).Document(appointmentId.Trim());
            var appointmentSnapshot = await transaction.GetSnapshotAsync(appointmentRef, CancellationToken.None);
            if (!appointmentSnapshot.Exists)
            {
                throw new InvalidOperationException("Không tìm thấy lịch hẹn cần từ chối hủy.");
            }

            var currentStatus = GetString(appointmentSnapshot, "status", "Status")?.Trim().ToLowerInvariant();
            if (currentStatus != "cancel_requested")
            {
                throw new InvalidOperationException("Lịch hẹn này không ở trạng thái chờ duyệt hủy.");
            }

            var statusBeforeCancel = GetString(appointmentSnapshot, "statusBeforeCancelRequest")?.Trim();
            if (string.IsNullOrWhiteSpace(statusBeforeCancel) ||
                statusBeforeCancel.Equals("cancel_requested", StringComparison.OrdinalIgnoreCase) ||
                statusBeforeCancel.Equals("cancelled", StringComparison.OrdinalIgnoreCase))
            {
                statusBeforeCancel = "confirmed";
            }

            var now = Timestamp.GetCurrentTimestamp();
            notificationPatientId = GetString(appointmentSnapshot, "patientId", "PatientId", "userId", "UserId")?.Trim();
            notificationAppointmentCode = GetString(appointmentSnapshot, "appointmentCode", "code", "bookingCode") ?? notificationAppointmentCode;
            transaction.Update(appointmentRef, new Dictionary<string, object?>
            {
                ["status"] = statusBeforeCancel,
                ["cancelRequestStatus"] = "rejected",
                ["cancelRejectReason"] = notificationRejectReason,
                ["cancelReviewedByStaffId"] = string.IsNullOrWhiteSpace(adminIdentifier) ? "admin" : adminIdentifier.Trim(),
                ["cancelReviewedAt"] = now,
                ["updatedAt"] = now
            });
        }, cancellationToken: CancellationToken.None);

        if (!string.IsNullOrWhiteSpace(notificationPatientId))
        {
            await CreateBasicNotificationAsync(
                notificationPatientId,
                "patient",
                "Yêu cầu hủy lịch bị từ chối",
                $"Yêu cầu hủy lịch {notificationAppointmentCode} bị từ chối. Lý do: {notificationRejectReason}",
                "appointment_cancel_rejected",
                cancellationToken);
        }
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
        var appointmentCode = await _codeGenerator.GenerateNextCodeAsync("appointments", cancellationToken);
        var payload = new Dictionary<string, object?>
        {
            ["appointmentCode"] = appointmentCode,
            ["patientName"] = model.PatientName.Trim(),
            ["patientPhone"] = model.PatientPhone?.Trim(),
            ["doctorId"] = model.DoctorId,
            ["doctorCode"] = doctor?.DocumentId ?? model.DoctorId,
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

        foreach (var doc in docs.Documents)
        {
            var amount = GetDecimal(doc, "patientPayAmount", "finalAmount", "amount", "totalAmount", "AmountVnd");
            var originalAmount = GetDecimal(doc, "originalAmount", "subtotalAmount", "totalAmount", "amount", "AmountVnd");
            if (originalAmount <= 0) originalAmount = amount;
            var insuranceSupport = GetDecimal(doc, "insuranceSupportAmount", "insuranceDiscountAmount", "bhytSupportAmount", "discountAmount");
            if (insuranceSupport <= 0 && originalAmount > amount)
            {
                insuranceSupport = originalAmount - amount;
            }
            var status = ParseTransactionStatus(GetString(doc, "paymentStatus", "PaymentStatus", "status", "Status"));
            var paidAt = GetDateTime(doc, "paidAt", "createdAt", "CreatedAt") ?? DateTime.Now;

            transactions.Add(new TransactionListItemViewModel
            {
                Id = doc.Id,
                SourceCollection = doc.Reference.Parent.Id,
                InvoiceCode = GetString(doc, "paymentCode", "invoiceCode", "code", "id") ?? doc.Id,
                AppointmentCode = GetString(doc, "appointmentCode", "bookingCode", "appointmentId") ?? string.Empty,
                PatientCode = GetString(doc, "patientCode", "userCode") ?? string.Empty,
                PatientName = GetString(doc, "patientName", "PatientName") ?? string.Empty,
                PatientPhone = GetString(doc, "patientPhone", "phone", "phoneNumber") ?? string.Empty,
                ServiceName = GetString(doc, "serviceName", "description") ?? "Dịch vụ khám",
                OriginalAmountVnd = originalAmount,
                InsuranceSupportVnd = insuranceSupport,
                AmountVnd = amount,
                PaidAt = paidAt,
                Method = ParsePaymentMethod(GetString(doc, "paymentMethod", "method")),
                Status = status
            });
        }

        var filtered = transactions.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim().ToLowerInvariant();
            filtered = filtered.Where(x =>
                x.InvoiceCode.ToLowerInvariant().Contains(key) ||
                x.AppointmentCode.ToLowerInvariant().Contains(key) ||
                x.PatientCode.ToLowerInvariant().Contains(key) ||
                x.PatientName.ToLowerInvariant().Contains(key) ||
                x.PatientPhone.ToLowerInvariant().Contains(key) ||
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

    public async Task<PaymentRecord?> FindPaymentRecordByIdAsync(
        string paymentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(paymentId))
        {
            return null;
        }

        var doc = await FindPaymentDocumentAsync(paymentId.Trim(), null, cancellationToken);
        return doc is null || !doc.Exists ? null : MapPaymentRecord(doc);
    }

    public async Task ConfirmCashPaymentAsync(
        string id,
        string? sourceCollection,
        string staffId,
        string staffName,
        CancellationToken cancellationToken = default)
    {
        await ConfirmManualPaymentAsync(id, sourceCollection, PaymentMethod.Cash, staffId, staffName, cancellationToken);
    }

    public async Task ConfirmBankTransferPaymentAsync(
        string id,
        string? sourceCollection,
        string staffId,
        string staffName,
        CancellationToken cancellationToken = default)
    {
        await ConfirmManualPaymentAsync(id, sourceCollection, PaymentMethod.BankTransfer, staffId, staffName, cancellationToken);
    }

    private async Task ConfirmManualPaymentAsync(
        string id,
        string? sourceCollection,
        PaymentMethod method,
        string staffId,
        string staffName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException("Thiếu mã thanh toán.");
        }

        var doc = await FindPaymentDocumentAsync(id.Trim(), sourceCollection, cancellationToken);
        if (doc is null || !doc.Exists)
        {
            throw new InvalidOperationException("Không tìm thấy thanh toán cần xác nhận.");
        }

        var payment = MapPaymentRecord(doc);
        if (ParseTransactionStatus(payment.PaymentStatus) == TransactionStatus.Paid ||
            ParseTransactionStatus(payment.Status) == TransactionStatus.Paid)
        {
            throw new InvalidOperationException("Thanh toán này đã được xác nhận trước đó.");
        }

        var now = Timestamp.GetCurrentTimestamp();
        var methodValue = method == PaymentMethod.BankTransfer ? "bank_transfer" : "cash";
        var updates = new Dictionary<string, object?>
        {
            ["status"] = "paid",
            ["paymentStatus"] = "paid",
            ["method"] = methodValue,
            ["paymentMethod"] = methodValue,
            ["paidAt"] = now,
            ["confirmedByStaffId"] = string.IsNullOrWhiteSpace(staffId) ? "staff" : staffId.Trim(),
            ["confirmedByStaffName"] = string.IsNullOrWhiteSpace(staffName) ? "Nhân viên" : staffName.Trim(),
            ["updatedAt"] = now
        };

        await doc.Reference.SetAsync(updates, SetOptions.MergeAll, cancellationToken);

        if (!string.IsNullOrWhiteSpace(payment.AppointmentId))
        {
            await _firestore.Collection(_settings.AppointmentCollections.First())
                .Document(payment.AppointmentId)
                .SetAsync(new Dictionary<string, object?>
                {
                    ["paymentStatus"] = "paid",
                    ["paymentMethod"] = methodValue,
                    ["updatedAt"] = now
                }, SetOptions.MergeAll, cancellationToken);
        }

        await CreatePaymentSuccessNotificationAsync(payment, cancellationToken);
    }

    public async Task<PaymentRecord?> FindPaymentRecordByGatewayOrderCodeAsync(
        long orderCode,
        CancellationToken cancellationToken = default)
    {
        var collections = GetPaymentCollectionNames();
        foreach (var collectionName in collections)
        {
            foreach (var fieldName in new[] { "gatewayOrderCode", "orderCode" })
            {
                var numericSnapshot = await _firestore.Collection(collectionName)
                    .WhereEqualTo(fieldName, orderCode)
                    .Limit(1)
                    .GetSnapshotAsync(cancellationToken);
                var numericDoc = numericSnapshot.Documents.FirstOrDefault(x => x.Exists);
                if (numericDoc is not null)
                {
                    return MapPaymentRecord(numericDoc);
                }

                var stringSnapshot = await _firestore.Collection(collectionName)
                    .WhereEqualTo(fieldName, orderCode.ToString(CultureInfo.InvariantCulture))
                    .Limit(1)
                    .GetSnapshotAsync(cancellationToken);
                var stringDoc = stringSnapshot.Documents.FirstOrDefault(x => x.Exists);
                if (stringDoc is not null)
                {
                    return MapPaymentRecord(stringDoc);
                }
            }
        }

        return null;
    }

    public async Task<PaymentRecord?> FindPaymentRecordByGatewayPaymentLinkIdAsync(
        string paymentLinkId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(paymentLinkId)) return null;

        var collections = GetPaymentCollectionNames();
        foreach (var collectionName in collections)
        {
            foreach (var fieldName in new[] { "gatewayPaymentLinkId", "paymentLinkId" })
            {
                var snapshot = await _firestore.Collection(collectionName)
                    .WhereEqualTo(fieldName, paymentLinkId.Trim())
                    .Limit(1)
                    .GetSnapshotAsync(cancellationToken);
                var doc = snapshot.Documents.FirstOrDefault(x => x.Exists);
                if (doc is not null)
                {
                    return MapPaymentRecord(doc);
                }
            }
        }

        return null;
    }

    public async Task UpdatePaymentAsync(
        string collectionName,
        string documentId,
        Dictionary<string, object?> updates,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collectionName) ||
            string.IsNullOrWhiteSpace(documentId) ||
            updates.Count == 0)
        {
            return;
        }

        await _firestore.Collection(collectionName.Trim())
            .Document(documentId.Trim())
            .SetAsync(updates, SetOptions.MergeAll, cancellationToken);
    }

    public async Task CreatePaymentSuccessNotificationAsync(
        PaymentRecord payment,
        CancellationToken cancellationToken = default)
    {
        var patientId = payment.PatientId ?? payment.UserId ?? payment.PatientUid;
        if (string.IsNullOrWhiteSpace(patientId))
        {
            return;
        }

        var notificationCode = await _codeGenerator.GenerateNextCodeAsync("notifications", cancellationToken);
        var amount = ConvertMoneyToVndLong(payment.Amount);
        var amountText = $"{amount:N0} VND";
        var now = Timestamp.GetCurrentTimestamp();

        await _firestore.Collection("Notifications").AddAsync(new Dictionary<string, object?>
        {
            ["notificationCode"] = notificationCode,
            ["userId"] = patientId.Trim(),
            ["patientId"] = patientId.Trim(),
            ["recipientRole"] = "patient",
            ["type"] = "payment",
            ["category"] = "payment",
            ["title"] = "Thanh toán thành công",
            ["body"] = $"Thanh toán thành công. Hóa đơn {payment.PaymentCode} với số tiền {amountText} đã được xác nhận.",
            ["data"] = new Dictionary<string, object?>
            {
                ["paymentId"] = payment.Id,
                ["paymentCode"] = payment.PaymentCode,
                ["finalAmount"] = amount,
                ["amount"] = amount,
                ["amountText"] = amountText
            },
            ["isRead"] = false,
            ["sendPush"] = true,
            ["deliveryStatus"] = "pending",
            ["createdAt"] = now,
            ["timestamp"] = now,
            ["updatedAt"] = now
        }, cancellationToken);
    }

    private static long ConvertMoneyToVndLong(decimal value)
    {
        return Convert.ToInt64(decimal.Round(value, 0, MidpointRounding.AwayFromZero));
    }

    private async Task CreateBasicNotificationAsync(
        string userId,
        string recipientRole,
        string title,
        string body,
        string type,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        var notificationCode = await _codeGenerator.GenerateNextCodeAsync("notifications", cancellationToken);
        var now = Timestamp.GetCurrentTimestamp();

        await _firestore.Collection("Notifications").AddAsync(new Dictionary<string, object?>
        {
            ["notificationCode"] = notificationCode,
            ["userId"] = userId.Trim(),
            ["recipientRole"] = recipientRole,
            ["type"] = type,
            ["category"] = "appointment",
            ["title"] = title,
            ["body"] = body,
            ["isRead"] = false,
            ["sendPush"] = true,
            ["deliveryStatus"] = "pending",
            ["createdAt"] = now,
            ["timestamp"] = now,
            ["updatedAt"] = now
        }, cancellationToken);
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

    public async Task UpdatePaymentStatusAsync(
        string id,
        string? sourceCollection,
        TransactionStatus status,
        string staffIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException("Không xác định được giao dịch cần cập nhật.");
        }

        var doc = await FindPaymentDocumentAsync(id.Trim(), sourceCollection, cancellationToken);
        if (doc is null || !doc.Exists)
        {
            throw new InvalidOperationException("Không tìm thấy giao dịch thanh toán.");
        }

        var now = Timestamp.GetCurrentTimestamp();
        var normalizedStatus = status switch
        {
            TransactionStatus.Paid => "paid",
            TransactionStatus.Failed => "failed",
            _ => "pending"
        };
        var payload = new Dictionary<string, object?>
        {
            ["status"] = normalizedStatus,
            ["paymentStatus"] = normalizedStatus,
            ["updatedAt"] = now,
            ["processedBy"] = string.IsNullOrWhiteSpace(staffIdentifier) ? "staff" : staffIdentifier.Trim(),
            ["processedAt"] = now
        };

        if (status == TransactionStatus.Paid)
        {
            payload["paidAt"] = now;
        }

        await doc.Reference.SetAsync(payload, SetOptions.MergeAll, cancellationToken);
    }

    public async Task<MedicineIndexViewModel> GetMedicinesAsync(
        string? search = null,
        string? groupFilter = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetFirstAvailableCollectionAsync(_settings.MedicineCollections, cancellationToken);
        var medicines = snapshot.Documents.Select(MapMedicine).ToList();
        var filtered = medicines.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim().ToLowerInvariant();
            filtered = filtered.Where(x =>
                x.MedicineCode.ToLowerInvariant().Contains(key) ||
                x.Name.ToLowerInvariant().Contains(key) ||
                x.Strength.ToLowerInvariant().Contains(key) ||
                x.Group.ToLowerInvariant().Contains(key));
        }

        if (!string.IsNullOrWhiteSpace(groupFilter))
        {
            filtered = filtered.Where(x => string.Equals(x.Group, groupFilter.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return new MedicineIndexViewModel
        {
            Search = search,
            GroupFilter = groupFilter,
            TotalCount = medicines.Count,
            LowStockCount = medicines.Count(x => x.Quantity <= 10),
            InventoryValue = medicines.Sum(x => x.UnitPrice * x.Quantity),
            Groups = medicines
                .Select(x => x.Group)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList(),
            Items = filtered
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Strength)
                .ToList()
        };
    }

    public async Task<MedicineUpsertViewModel> BuildMedicineUpsertModelAsync(
        MedicineUpsertViewModel model,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetFirstAvailableCollectionAsync(_settings.MedicineCollections, cancellationToken);
        var medicines = snapshot.Documents.Select(MapMedicine).ToList();

        model.UnitSuggestions = medicines
            .Select(x => x.Unit)
            .Concat(new[] { "viên", "gói", "ống", "lọ", "chai", "tuýp", "hộp" })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        model.GroupSuggestions = medicines
            .Select(x => x.Group)
            .Concat(new[] { "Kháng sinh", "Giảm đau", "Hạ sốt", "Vitamin", "Tim mạch", "Tiêu hóa", "Dị ứng" })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        return model;
    }

    public async Task<MedicineUpsertViewModel?> GetMedicineForEditAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var snapshot = await GetFirstExistingDocumentAsync(_settings.MedicineCollections, id.Trim(), cancellationToken);
        if (snapshot is null || !snapshot.Exists) return null;

        var model = new MedicineUpsertViewModel
        {
            Id = snapshot.Id,
            MedicineCode = GetMedicineCode(snapshot),
            Name = GetString(snapshot, "name", "medicineName", "tenThuoc", "drugName", "displayName") ?? string.Empty,
            Strength = GetString(snapshot, "strength", "dosage", "concentration", "hamLuong", "content") ?? string.Empty,
            Unit = GetString(snapshot, "unit", "donVi", "unitName") ?? string.Empty,
            UnitPrice = GetDecimal(snapshot, "unitPrice", "price", "donGia", "salePrice"),
            Group = GetString(snapshot, "group", "medicineGroup", "category", "nhomThuoc", "drugGroup") ?? string.Empty,
            Quantity = GetInt(snapshot, "quantity", "stock", "stockQuantity", "soLuong", "inventoryQuantity"),
            LowStockThreshold = Math.Max(0, GetInt(snapshot, "lowStockThreshold", "minQuantity", "nguongCanhBao")),
            Note = GetString(snapshot, "note", "description", "ghiChu"),
            IsActive = GetBool(snapshot, "isActive", "active", "status") ?? true
        };

        return await BuildMedicineUpsertModelAsync(model, cancellationToken);
    }

    public async Task<string> CreateMedicineAsync(MedicineUpsertViewModel model, CancellationToken cancellationToken = default)
    {
        var medicineCollection = await GetFirstAvailableCollectionNameAsync(_settings.MedicineCollections, cancellationToken);
        return await _firestore.RunTransactionAsync(async transaction =>
        {
            var now = Timestamp.GetCurrentTimestamp();
            var counterRef = _firestore.Collection("Counters").Document("document_codes");
            var counterSnapshot = await transaction.GetSnapshotAsync(counterRef, CancellationToken.None);
            var nextMedicineNumber = Math.Max(1, GetInt(counterSnapshot, "medicinesNext"));
            string medicineId;
            DocumentReference medicineRef;

            while (true)
            {
                medicineId = FormatSequentialCode("TH", nextMedicineNumber);
                medicineRef = _firestore.Collection(medicineCollection).Document(medicineId);
                var medicineSnapshot = await transaction.GetSnapshotAsync(medicineRef, CancellationToken.None);
                var legacyMedicineId = FormatLegacySequentialCode("TH", nextMedicineNumber);
                var legacyMedicineSnapshot = string.Equals(legacyMedicineId, medicineId, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : await transaction.GetSnapshotAsync(_firestore.Collection(medicineCollection).Document(legacyMedicineId), CancellationToken.None);

                if (!medicineSnapshot.Exists && legacyMedicineSnapshot?.Exists != true) break;
                nextMedicineNumber++;
            }

            model.MedicineCode = medicineId;
            transaction.Set(medicineRef, ToMedicinePayload(model, true, now), SetOptions.MergeAll);
            transaction.Set(counterRef, new Dictionary<string, object>
            {
                ["medicinesNext"] = nextMedicineNumber + 1,
                ["updatedAt"] = now
            }, SetOptions.MergeAll);

            return medicineId;
        }, cancellationToken: CancellationToken.None);
    }

    public async Task UpdateMedicineAsync(MedicineUpsertViewModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Id))
        {
            throw new InvalidOperationException("Không xác định được thuốc cần cập nhật.");
        }

        var snapshot = await GetFirstExistingDocumentAsync(_settings.MedicineCollections, model.Id.Trim(), cancellationToken);
        if (snapshot is null || !snapshot.Exists)
        {
            throw new InvalidOperationException("Không tìm thấy thuốc cần cập nhật.");
        }

        model.MedicineCode = string.IsNullOrWhiteSpace(model.MedicineCode)
            ? GetMedicineCode(snapshot)
            : model.MedicineCode.Trim();

        await snapshot.Reference.SetAsync(ToMedicinePayload(model, false, Timestamp.GetCurrentTimestamp()), SetOptions.MergeAll, cancellationToken);
    }

    private static MedicineListItemViewModel MapMedicine(DocumentSnapshot doc)
    {
        return new MedicineListItemViewModel
        {
            Id = doc.Id,
            MedicineCode = GetMedicineCode(doc),
            Name = GetString(doc, "name", "medicineName", "tenThuoc", "drugName", "displayName") ?? "Chưa có tên",
            Strength = GetString(doc, "strength", "dosage", "concentration", "hamLuong", "content") ?? string.Empty,
            Unit = GetString(doc, "unit", "donVi", "unitName") ?? string.Empty,
            UnitPrice = GetDecimal(doc, "unitPrice", "price", "donGia", "salePrice"),
            Group = GetString(doc, "group", "medicineGroup", "category", "nhomThuoc", "drugGroup") ?? string.Empty,
            Quantity = GetInt(doc, "quantity", "stock", "stockQuantity", "soLuong", "inventoryQuantity"),
            IsActive = GetBool(doc, "isActive", "active", "status") ?? true,
            UpdatedAt = GetDateTime(doc, "updatedAt", "createdAt")
        };
    }

    private static string GetMedicineCode(DocumentSnapshot doc)
    {
        return GetString(doc, "medicineCode", "code", "maThuoc", "drugCode", "id") ?? doc.Id;
    }

    private static Dictionary<string, object?> ToMedicinePayload(MedicineUpsertViewModel model, bool isCreate, Timestamp now)
    {
        var payload = new Dictionary<string, object?>
        {
            ["medicineCode"] = model.MedicineCode?.Trim() ?? string.Empty,
            ["name"] = model.Name.Trim(),
            ["strength"] = model.Strength.Trim(),
            ["unit"] = model.Unit.Trim(),
            ["unitPrice"] = Convert.ToInt64(model.UnitPrice),
            ["group"] = model.Group.Trim(),
            ["quantity"] = Math.Max(0, model.Quantity),
            ["lowStockThreshold"] = Math.Max(0, model.LowStockThreshold),
            ["note"] = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim(),
            ["isActive"] = model.IsActive,
            ["updatedAt"] = now
        };

        if (isCreate)
        {
            payload["createdAt"] = now;
        }

        return payload;
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

    private IReadOnlyList<string> GetPaymentCollectionNames()
    {
        return _settings.PaymentCollections
            .Concat(_settings.InvoiceCollections)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static PaymentRecord MapPaymentRecord(DocumentSnapshot doc)
    {
        var amount = GetDecimal(
            doc,
            "patientPayAmount",
            "finalAmount",
            "amountDue",
            "payableAmount",
            "amount",
            "totalAmount",
            "AmountVnd",
            "paidAmount",
            "total",
            "grandTotal");

        return new PaymentRecord
        {
            Id = doc.Id,
            CollectionName = doc.Reference.Parent.Id,
            AppointmentId = GetString(doc, "appointmentId", "AppointmentId", "bookingId"),
            PatientId = GetString(doc, "patientId", "PatientId"),
            UserId = GetString(doc, "userId", "UserId", "uid", "Uid"),
            PatientUid = GetString(doc, "patientUid", "PatientUid"),
            Amount = amount,
            PaymentCode = GetString(doc, "paymentCode", "invoiceCode", "code", "id", "receiptCode") ?? doc.Id,
            InvoiceCode = GetString(doc, "invoiceCode", "receiptCode") ?? string.Empty,
            AppointmentCode = GetString(doc, "appointmentCode", "bookingCode", "scheduleCode") ?? string.Empty,
            PaymentStatus = GetString(doc, "paymentStatus", "PaymentStatus", "payment_state") ?? string.Empty,
            Status = GetString(doc, "status", "Status") ?? string.Empty,
            PaymentMethod = GetString(doc, "paymentMethod", "PaymentMethod") ?? string.Empty,
            Method = GetString(doc, "method", "Method") ?? string.Empty,
            GatewayProvider = GetString(doc, "gatewayProvider", "GatewayProvider"),
            GatewayOrderCode = GetLong(doc, "gatewayOrderCode", "orderCode"),
            GatewayPaymentLinkId = GetString(doc, "gatewayPaymentLinkId", "paymentLinkId"),
            CheckoutUrl = GetString(doc, "checkoutUrl", "CheckoutUrl", "payosCheckoutUrl"),
            CreatedAt = GetDateTime(doc, "createdAt", "CreatedAt"),
            UpdatedAt = GetDateTime(doc, "updatedAt", "UpdatedAt"),
            PaidAt = GetDateTime(doc, "paidAt", "PaidAt", "paymentDate")
        };
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
        var users = await LoadAppointmentUsersAsync(cancellationToken);
        var departments = await LoadDepartmentNamesAsync(cancellationToken);
        var doctors = await LoadAppointmentDoctorsAsync(cancellationToken);

        foreach (var collectionName in new[] { "MedicalRecords", "medicalRecords", "medical_records" })
        {
            var snapshot = await _firestore.Collection(collectionName).Limit(500).GetSnapshotAsync(cancellationToken);
            foreach (var doc in snapshot.Documents)
            {
                records.Add(MapMedicalRecordListItem(doc, users, doctors, departments, isAppointmentSource: false));
            }

            if (records.Count > 0) break;
        }

        if (records.Count == 0)
        {
            var appointmentDocs = await GetFirstAvailableCollectionAsync(_settings.AppointmentCollections, cancellationToken);
            foreach (var doc in appointmentDocs.Documents)
            {
                records.Add(MapMedicalRecordListItem(doc, users, doctors, departments, isAppointmentSource: true));
            }
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

    public async Task<MedicalRecordDetailsViewModel?> GetMedicalRecordDetailsAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var users = await LoadAppointmentUsersAsync(cancellationToken);
        var departments = await LoadDepartmentNamesAsync(cancellationToken);
        var doctors = await LoadAppointmentDoctorsAsync(cancellationToken);

        foreach (var collectionName in new[] { "MedicalRecords", "medicalRecords", "medical_records" })
        {
            var doc = await _firestore.Collection(collectionName).Document(id.Trim()).GetSnapshotAsync(cancellationToken);
            if (doc.Exists)
            {
                return MapMedicalRecordDetails(doc, users, doctors, departments, isAppointmentSource: false);
            }
        }

        foreach (var collectionName in _settings.AppointmentCollections.DefaultIfEmpty("Appointments"))
        {
            var doc = await _firestore.Collection(collectionName).Document(id.Trim()).GetSnapshotAsync(cancellationToken);
            if (doc.Exists)
            {
                return MapMedicalRecordDetails(doc, users, doctors, departments, isAppointmentSource: true);
            }
        }

        return null;
    }

    public Task<WorkShiftsIndexViewModel> GetWorkShiftsAsync(
        string? mode = null,
        DateOnly? selectedWeekStart = null,
        CancellationToken cancellationToken = default)
    {
        return GetWorkShiftsAsync(mode, selectedWeekStart, null, cancellationToken);
    }

    public async Task<WorkShiftsIndexViewModel> GetWorkShiftsAsync(
        string? mode = null,
        DateOnly? selectedWeekStart = null,
        string? selectedDoctorId = null,
        CancellationToken cancellationToken = default)
    {
        mode = string.Equals(mode, "calendar", StringComparison.OrdinalIgnoreCase) ? "calendar" : "list";
        selectedDoctorId = selectedDoctorId?.Trim() ?? string.Empty;
        var hasDoctorFilter = !string.IsNullOrWhiteSpace(selectedDoctorId);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var weekStartSource = selectedWeekStart ?? today;
        var weekStart = weekStartSource.AddDays(-DayOfWeekToMondayBased(weekStartSource.DayOfWeek));
        var weekEnd = weekStart.AddDays(6);
        var calendar = CalendarBuilder.BuildMonth(monthStart);

        var scheduleRangeStart = weekStart < monthStart ? weekStart : monthStart;
        var scheduleRangeEnd = weekEnd > monthEnd ? weekEnd : monthEnd;
        var shiftDocs = await GetFirstAvailableCollectionAsync(_settings.ShiftCollections, cancellationToken);
        var shifts = LoadShiftInfo(shiftDocs.Documents);
        var doctors = await LoadAppointmentDoctorsAsync(cancellationToken);
        var selectedDoctor = hasDoctorFilter
            ? doctors.FirstOrDefault(x =>
                string.Equals(x.DocumentId, selectedDoctorId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.UserId, selectedDoctorId, StringComparison.OrdinalIgnoreCase))
            : null;
        var scheduleDocs = hasDoctorFilter
            ? await QueryDoctorSchedulesByDoctorAsync(selectedDoctorId, selectedDoctor?.UserId, cancellationToken)
            : await QueryDoctorSchedulesByDateRangeAsync(scheduleRangeStart, scheduleRangeEnd, cancellationToken);
        var departments = await LoadDepartmentNamesAsync(cancellationToken);
        var departmentRooms = await LoadDepartmentRoomsAsync(cancellationToken);

        var monthAssignments = new List<TodayAssignmentViewModel>();
        var todayAssignments = new List<TodayAssignmentViewModel>();
        var scheduleRows = new List<DoctorScheduleRowViewModel>();
        var unassignedCount = 0;

        foreach (var doc in scheduleDocs)
        {
            var date = GetScheduleDate(doc);
            if (date is null) continue;

            var doctorId = GetString(doc, "doctorId", "DoctorId");
            var doctorUserId = GetString(doc, "doctorUserId", "DoctorUserId", "userId", "UserId");
            var doctor = ResolveAppointmentDoctor(doctors, doctorId, doctorUserId);
            if (doctor is null)
            {
                continue;
            }

            var shiftId = GetString(doc, "shiftId", "ShiftId");
            var shift = !string.IsNullOrWhiteSpace(shiftId) && shifts.TryGetValue(shiftId, out var foundShift)
                ? foundShift
                : ShiftInfo.FromSchedule(doc);
            var shiftType = ParseShiftType(GetString(doc, "shiftType", "type", "ShiftType") ?? shift.Type);
            var departmentId = GetString(doc, "departmentId", "DepartmentId", "specialtyId", "SpecialtyId");
            var department = !string.IsNullOrWhiteSpace(departmentId) && departments.TryGetValue(departmentId, out var foundDepartment)
                ? foundDepartment
                : GetString(doc, "departmentName", "DepartmentName", "specialtyName", "department")
                    ?? doctor.Specialization
                    ?? departmentId
                    ?? "Chưa rõ khoa";

            var roomNumber = GetString(doc, "roomNumber", "room", "roomName", "clinicRoom", "location") ?? shift.RoomNumber;
            var roomId = GetString(doc, "roomId", "RoomId") ?? NormalizeRoomId(roomNumber);
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
                    BuildScheduleSubtitle(roomNumber, department));
            }

            var assignment = new TodayAssignmentViewModel
            {
                Initial = Initials(doctorName),
                DoctorName = doctorName,
                DoctorTitle = GetString(doc, "doctorTitle", "degree", "title"),
                Specialty = department,
                ShiftLabel = shiftLabel,
                Room = roomNumber,
                Status = status
            };

            if (date.Value >= monthStart && date.Value <= monthEnd)
            {
                monthAssignments.Add(assignment);
            }

            if (hasDoctorFilter ||
                date.Value >= monthStart && date.Value <= monthEnd ||
                date.Value >= weekStart && date.Value <= weekEnd)
            {
                scheduleRows.Add(new DoctorScheduleRowViewModel
                {
                    DocumentId = doc.Id,
                    DoctorId = doctorId ?? string.Empty,
                    Date = date.Value,
                    DoctorName = doctorName,
                    DepartmentId = departmentId ?? string.Empty,
                    DepartmentName = department,
                    RoomId = roomId,
                    RoomNumber = roomNumber,
                    ShiftId = shiftId ?? string.Empty,
                    ShiftType = shiftType,
                    ShiftName = shift.Name,
                    ShiftTime = FormatShiftTime(shift),
                    MaxSlots = GetInt(doc, "slotCapacity", "maxSlots", "MaxSlots"),
                    AvailableSlots = GetInt(doc, "remainingSlots", "availableSlots", "AvailableSlots", "slots"),
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
            SelectedDoctorId = selectedDoctorId,
            RangeLabel = $"{monthStart:dd/MM} - {monthEnd:dd/MM/yyyy}",
            TotalDoctorsLabel = $"{doctors.Count} Bác sĩ",
            UnassignedCount = unassignedCount,
            FillRatePercent = fillRate,
            ConflictCount = CountScheduleConflicts(scheduleDocs),
            MonthLabel = $"Tháng {monthStart.Month}, {monthStart.Year}",
            Calendar = calendar,
            WeeklyTable = BuildWeeklyScheduleTable(scheduleRows, weekStart, weekEnd),
            TodayLabel = $"Phân bổ nhân sự hôm nay ({today:dd/MM})",
            TodayAssignments = todayAssignments,
            Schedules = scheduleRows.OrderBy(x => x.Date).ThenBy(x => x.DoctorName).ThenBy(x => x.ShiftName).ToList(),
            Doctors = doctors
                .OrderBy(x => x.FullName)
                .Select(x => new SelectOption { Value = x.DocumentId, Text = x.FullName, Group = x.DepartmentId })
                .ToList(),
            Departments = departments
                .OrderBy(x => x.Value)
                .Select(x => new SelectOption { Value = x.Key, Text = x.Value })
                .ToList(),
            DepartmentRooms = departmentRooms,
            Shifts = shifts
                .Where(x => ParseShiftType(x.Value.Type ?? x.Key) is ShiftType.Morning or ShiftType.Afternoon)
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
        if (string.IsNullOrWhiteSpace(model.DoctorId))
        {
            throw new InvalidOperationException("Vui lòng chọn bác sĩ trước khi sinh lịch.");
        }

        if (string.IsNullOrWhiteSpace(model.RoomNumber))
        {
            throw new InvalidOperationException("Vui lòng chọn phòng khám trước khi sinh lịch.");
        }

        var selectedPairs = BuildDayShiftSelections(model);
        if (selectedPairs.Count == 0)
        {
            throw new InvalidOperationException("Vui lòng chọn ít nhất một thứ và một ca làm việc.");
        }

        var collection = _firestore.Collection(_settings.DoctorScheduleCollections.First());
        var doctors = await LoadAppointmentDoctorsAsync(cancellationToken);
        var departments = await LoadDepartmentNamesAsync(cancellationToken);
        var doctor = doctors.FirstOrDefault(x => string.Equals(x.DocumentId, model.DoctorId, StringComparison.OrdinalIgnoreCase));
        if (doctor is null)
        {
            throw new InvalidOperationException("Không tìm thấy bác sĩ đã chọn.");
        }

        var doctorDepartmentId = doctor.DepartmentId?.Trim();
        var selectedDepartmentId = string.IsNullOrWhiteSpace(doctorDepartmentId)
            ? model.DepartmentId?.Trim()
            : doctorDepartmentId;
        if (string.IsNullOrWhiteSpace(selectedDepartmentId))
        {
            throw new InvalidOperationException("Bác sĩ đã chọn chưa có khoa. Vui lòng chọn khoa trước khi phân lịch.");
        }

        model.DepartmentId = selectedDepartmentId;
        var departmentName = departments.TryGetValue(model.DepartmentId, out var foundDepartment)
            ? foundDepartment
            : model.DepartmentId;
        var weeksAhead = Math.Clamp(model.WeeksAhead, 1, 8);
        var availableSlots = Math.Max(model.AvailableSlots, 0);
        var roomNumber = model.RoomNumber.Trim();
        var roomId = string.IsNullOrWhiteSpace(model.RoomId) ? NormalizeRoomId(roomNumber) : model.RoomId.Trim();
        var startDate = model.StartDate;
        var endDate = startDate.AddDays(weeksAhead * 7 - 1);
        var payloads = new List<Dictionary<string, object?>>();
        var validDoctorIds = doctors
            .SelectMany(x => new[] { x.DocumentId, x.UserId })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            foreach (var shiftId in selectedPairs
                         .Where(x => x.Day == date.DayOfWeek)
                         .Select(x => x.ShiftId)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var existingSchedules = await QueryDoctorSchedulesForConflictAsync(date, shiftId, cancellationToken);
                if (HasDoctorScheduleConflict(
                    existingSchedules,
                    model.DoctorId,
                    roomNumber,
                    date,
                    shiftId,
                    departmentId: model.DepartmentId,
                    roomId: roomId,
                    validDoctorIds: validDoctorIds))
                {
                    continue;
                }

                payloads.Add(new Dictionary<string, object?>
                {
                    ["doctorId"] = model.DoctorId,
                    ["userId"] = doctor.UserId ?? string.Empty,
                    ["departmentId"] = model.DepartmentId,
                    ["departmentName"] = departmentName,
                    ["scheduleDate"] = Timestamp.FromDateTime(date.ToDateTime(TimeOnly.MinValue).ToUniversalTime()),
                    ["shiftId"] = shiftId,
                    ["roomId"] = roomId,
                    ["roomNumber"] = roomNumber,
                    ["maxSlots"] = availableSlots,
                    ["availableSlots"] = availableSlots,
                    ["slotCapacity"] = availableSlots,
                    ["remainingSlots"] = availableSlots,
                    ["bookedSlots"] = 0,
                    ["isActive"] = true,
                    ["createdAt"] = Timestamp.GetCurrentTimestamp(),
                    ["updatedAt"] = Timestamp.GetCurrentTimestamp()
                });
            }
        }

        if (payloads.Count == 0)
        {
            throw new InvalidOperationException("Không có lịch mới được tạo vì toàn bộ ngày/ca đã trùng lịch bác sĩ hoặc trùng phòng. Vui lòng chọn ngày, ca hoặc phòng khác.");
        }

        return await _firestore.RunTransactionAsync(async transaction =>
        {
            var counterRef = _firestore.Collection("Counters").Document("document_codes");
            var counterSnapshot = await transaction.GetSnapshotAsync(counterRef, CancellationToken.None);
            var nextScheduleNumber = Math.Max(1, GetInt(counterSnapshot, "doctorSchedulesNext"));
            var scheduleRefs = new List<(string Id, DocumentReference Ref)>();

            while (scheduleRefs.Count < payloads.Count)
            {
                var scheduleId = FormatSequentialCode("LLV", nextScheduleNumber);
                var scheduleRef = collection.Document(scheduleId);
                var scheduleSnapshot = await transaction.GetSnapshotAsync(scheduleRef, CancellationToken.None);
                var legacyScheduleId = FormatLegacySequentialCode("LLV", nextScheduleNumber);
                var legacyScheduleSnapshot = string.Equals(legacyScheduleId, scheduleId, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : await transaction.GetSnapshotAsync(collection.Document(legacyScheduleId), CancellationToken.None);

                if (!scheduleSnapshot.Exists && legacyScheduleSnapshot?.Exists != true)
                {
                    scheduleRefs.Add((scheduleId, scheduleRef));
                }

                nextScheduleNumber++;
            }

            for (var i = 0; i < payloads.Count; i++)
            {
                payloads[i]["scheduleCode"] = scheduleRefs[i].Id;
                transaction.Set(scheduleRefs[i].Ref, payloads[i]);
            }

            transaction.Set(counterRef, new Dictionary<string, object>
            {
                ["doctorSchedulesNext"] = nextScheduleNumber,
                ["updatedAt"] = Timestamp.GetCurrentTimestamp()
            }, SetOptions.MergeAll);

            return payloads.Count;
        }, cancellationToken: CancellationToken.None);
    }

    public async Task UpdateDoctorScheduleAsync(string documentId, bool isActive, int availableSlots, string? roomNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new InvalidOperationException("Không xác định được lịch cần cập nhật.");
        }

        var snapshot = await GetFirstExistingDocumentAsync(_settings.DoctorScheduleCollections, documentId.Trim(), cancellationToken);
        if (snapshot is null || !snapshot.Exists)
        {
            throw new InvalidOperationException("Không tìm thấy lịch cần cập nhật.");
        }

        if (await HasAppointmentsForScheduleAsync(documentId, cancellationToken))
        {
            throw new InvalidOperationException("Lịch này đã có lịch hẹn. Không thể sửa phòng, slot hoặc trạng thái nếu chưa xử lý các lịch hẹn liên quan.");
        }

        var date = GetScheduleDate(snapshot);
        var shiftId = GetString(snapshot, "shiftId", "ShiftId", "shiftType", "type", "ShiftType");
        var doctorId = GetString(snapshot, "doctorId", "DoctorId", "doctorUserId", "DoctorUserId", "userId", "UserId");
        var departmentId = GetString(snapshot, "departmentId", "DepartmentId");
        var roomId = NormalizeRoomId(roomNumber);

        if (date.HasValue && !string.IsNullOrWhiteSpace(shiftId) && !string.IsNullOrWhiteSpace(doctorId))
        {
            var existingSchedules = await QueryDoctorSchedulesForConflictAsync(date.Value, shiftId, cancellationToken);
            if (HasDoctorScheduleConflict(existingSchedules, doctorId, roomNumber, date.Value, shiftId, documentId, departmentId, roomId))
            {
                throw new InvalidOperationException("Lịch bị trùng bác sĩ hoặc trùng phòng trong cùng ngày, cùng ca.");
            }
        }

        await snapshot.Reference.UpdateAsync(new Dictionary<string, object>
        {
            ["isActive"] = isActive,
            ["maxSlots"] = Math.Max(availableSlots, 0),
            ["availableSlots"] = Math.Max(availableSlots, 0),
            ["slotCapacity"] = Math.Max(availableSlots, 0),
            ["remainingSlots"] = Math.Max(availableSlots, 0),
            ["bookedSlots"] = 0,
            ["roomId"] = NormalizeRoomId(roomNumber),
            ["roomNumber"] = roomNumber?.Trim() ?? string.Empty,
            ["updatedAt"] = Timestamp.GetCurrentTimestamp()
        }, cancellationToken: cancellationToken);
    }

    public async Task<WorkScheduleEditViewModel?> GetDoctorScheduleEditAsync(string documentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId)) return null;

        var snapshot = await GetFirstExistingDocumentAsync(_settings.DoctorScheduleCollections, documentId.Trim(), cancellationToken);
        if (snapshot is null || !snapshot.Exists) return null;

        var shiftDocs = await GetFirstAvailableCollectionAsync(_settings.ShiftCollections, cancellationToken);
        var shifts = LoadShiftInfo(shiftDocs.Documents);
        var doctors = await LoadAppointmentDoctorsAsync(cancellationToken);
        var departments = await LoadDepartmentNamesAsync(cancellationToken);
        var departmentRooms = await LoadDepartmentRoomsAsync(cancellationToken);
        var shiftId = GetString(snapshot, "shiftId", "ShiftId") ?? string.Empty;

        return new WorkScheduleEditViewModel
        {
            DocumentId = snapshot.Id,
            DoctorId = GetString(snapshot, "doctorId", "DoctorId") ?? string.Empty,
            DepartmentId = GetString(snapshot, "departmentId", "DepartmentId") ?? string.Empty,
            RoomId = GetString(snapshot, "roomId", "RoomId") ?? string.Empty,
            RoomNumber = GetString(snapshot, "roomNumber", "room", "roomName", "clinicRoom", "location") ?? string.Empty,
            ShiftId = shiftId,
            ScheduleDate = GetScheduleDate(snapshot) ?? DateOnly.FromDateTime(DateTime.Today),
            MaxSlots = Math.Max(1, GetInt(snapshot, "maxSlots", "MaxSlots", "availableSlots", "AvailableSlots", "slots")),
            AvailableSlots = Math.Max(0, GetInt(snapshot, "availableSlots", "AvailableSlots", "slots")),
            IsActive = GetBool(snapshot, "isActive", "IsActive") ?? true,
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
                .Where(x => ParseShiftType(x.Value.Type ?? x.Key) is ShiftType.Morning or ShiftType.Afternoon)
                .OrderBy(x => x.Key)
                .Select(x => new SelectOption { Value = x.Key, Text = BuildShiftLabel(x.Value, ParseShiftType(x.Value.Type ?? x.Key)) })
                .ToList()
        };
    }

    public async Task UpdateDoctorScheduleAsync(WorkScheduleEditViewModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.DocumentId))
        {
            throw new InvalidOperationException("Không xác định được lịch cần sửa.");
        }

        var snapshot = await GetFirstExistingDocumentAsync(_settings.DoctorScheduleCollections, model.DocumentId.Trim(), cancellationToken);
        if (snapshot is null || !snapshot.Exists)
        {
            throw new InvalidOperationException("Không tìm thấy lịch cần sửa.");
        }

        if (await HasAppointmentsForScheduleAsync(model.DocumentId, cancellationToken))
        {
            throw new InvalidOperationException("Lịch này đã có lịch hẹn. Không thể sửa bác sĩ, ngày, ca, phòng hoặc slot nếu chưa xử lý các lịch hẹn liên quan.");
        }

        var existingSchedules = await QueryDoctorSchedulesForConflictAsync(model.ScheduleDate, model.ShiftId, cancellationToken);
        if (HasDoctorScheduleConflict(
            existingSchedules,
            model.DoctorId,
            model.RoomNumber,
            model.ScheduleDate,
            model.ShiftId,
            model.DocumentId,
            model.DepartmentId,
            model.RoomId))
        {
            throw new InvalidOperationException("Lịch bị trùng bác sĩ hoặc trùng phòng trong cùng ngày, cùng ca.");
        }

        var doctors = await LoadAppointmentDoctorsAsync(cancellationToken);
        var doctor = doctors.FirstOrDefault(x => string.Equals(x.DocumentId, model.DoctorId, StringComparison.OrdinalIgnoreCase));
        var maxSlots = Math.Max(1, model.MaxSlots);
        var availableSlots = Math.Clamp(model.AvailableSlots, 0, maxSlots);
        var roomNumber = model.RoomNumber.Trim();

        await snapshot.Reference.SetAsync(new Dictionary<string, object?>
        {
            ["doctorId"] = model.DoctorId.Trim(),
            ["userId"] = doctor?.UserId ?? string.Empty,
            ["departmentId"] = model.DepartmentId.Trim(),
            ["scheduleDate"] = Timestamp.FromDateTime(model.ScheduleDate.ToDateTime(TimeOnly.MinValue).ToUniversalTime()),
            ["shiftId"] = model.ShiftId.Trim(),
            ["roomId"] = string.IsNullOrWhiteSpace(model.RoomId) ? NormalizeRoomId(roomNumber) : model.RoomId.Trim(),
            ["roomNumber"] = roomNumber,
            ["maxSlots"] = maxSlots,
            ["availableSlots"] = availableSlots,
            ["isActive"] = model.IsActive,
            ["updatedAt"] = Timestamp.GetCurrentTimestamp()
        }, SetOptions.MergeAll, cancellationToken);
    }

    public async Task<string> DuplicateDoctorScheduleAsync(string sourceScheduleId, DateOnly targetDate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceScheduleId))
        {
            throw new InvalidOperationException("Không xác định được lịch mẫu cần bổ sung.");
        }

        var sourceSnapshot = await GetFirstExistingDocumentAsync(_settings.DoctorScheduleCollections, sourceScheduleId.Trim(), cancellationToken);
        if (sourceSnapshot is null || !sourceSnapshot.Exists)
        {
            throw new InvalidOperationException("Không tìm thấy lịch mẫu cần bổ sung.");
        }

        var doctorId = GetString(sourceSnapshot, "doctorId", "DoctorId");
        var departmentId = GetString(sourceSnapshot, "departmentId", "DepartmentId");
        var shiftId = GetString(sourceSnapshot, "shiftId", "ShiftId");
        var roomNumber = GetString(sourceSnapshot, "roomNumber", "room", "roomName", "clinicRoom", "location");
        var roomId = GetString(sourceSnapshot, "roomId", "RoomId") ?? NormalizeRoomId(roomNumber);

        if (string.IsNullOrWhiteSpace(doctorId) ||
            string.IsNullOrWhiteSpace(departmentId) ||
            string.IsNullOrWhiteSpace(shiftId) ||
            string.IsNullOrWhiteSpace(roomNumber))
        {
            throw new InvalidOperationException("Lịch mẫu thiếu bác sĩ, khoa, ca hoặc phòng nên không thể bổ sung.");
        }

        var existingSchedules = await QueryDoctorSchedulesForConflictAsync(targetDate, shiftId, cancellationToken);
        if (HasDoctorScheduleConflict(existingSchedules, doctorId, roomNumber, targetDate, shiftId, departmentId: departmentId, roomId: roomId))
        {
            throw new InvalidOperationException("Không thể bổ sung vì bác sĩ hoặc phòng đã có lịch trong ngày này, cùng ca.");
        }

        var maxSlots = Math.Max(1, GetInt(sourceSnapshot, "maxSlots", "MaxSlots", "availableSlots", "AvailableSlots", "slots"));
        var userId = GetString(sourceSnapshot, "userId", "UserId") ?? string.Empty;
        var isActive = GetBool(sourceSnapshot, "isActive", "IsActive") ?? true;
        var collection = _firestore.Collection(_settings.DoctorScheduleCollections.First());

        return await _firestore.RunTransactionAsync(async transaction =>
        {
            var counterRef = _firestore.Collection("Counters").Document("document_codes");
            var counterSnapshot = await transaction.GetSnapshotAsync(counterRef, CancellationToken.None);
            var nextScheduleNumber = Math.Max(1, GetInt(counterSnapshot, "doctorSchedulesNext"));
            string scheduleId;
            DocumentReference scheduleRef;

            while (true)
            {
                scheduleId = FormatSequentialCode("LLV", nextScheduleNumber);
                scheduleRef = collection.Document(scheduleId);
                var scheduleSnapshot = await transaction.GetSnapshotAsync(scheduleRef, CancellationToken.None);
                var legacyScheduleId = FormatLegacySequentialCode("LLV", nextScheduleNumber);
                var legacyScheduleSnapshot = string.Equals(legacyScheduleId, scheduleId, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : await transaction.GetSnapshotAsync(collection.Document(legacyScheduleId), CancellationToken.None);

                if (!scheduleSnapshot.Exists && legacyScheduleSnapshot?.Exists != true) break;
                nextScheduleNumber++;
            }

            var now = Timestamp.GetCurrentTimestamp();
            transaction.Set(scheduleRef, new Dictionary<string, object?>
            {
                ["scheduleCode"] = scheduleId,
                ["doctorId"] = doctorId,
                ["userId"] = userId,
                ["departmentId"] = departmentId,
                ["scheduleDate"] = Timestamp.FromDateTime(targetDate.ToDateTime(TimeOnly.MinValue).ToUniversalTime()),
                ["shiftId"] = shiftId,
                ["roomId"] = roomId,
                ["roomNumber"] = roomNumber.Trim(),
                ["maxSlots"] = maxSlots,
                ["availableSlots"] = maxSlots,
                ["isActive"] = isActive,
                ["createdAt"] = now,
                ["updatedAt"] = now
            });

            transaction.Set(counterRef, new Dictionary<string, object>
            {
                ["doctorSchedulesNext"] = nextScheduleNumber + 1,
                ["updatedAt"] = now
            }, SetOptions.MergeAll);

            return scheduleId;
        }, cancellationToken: CancellationToken.None);
    }

    public async Task<int> BackfillScheduleRoomsAsync(WorkScheduleBackfillRoomViewModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.RoomNumber)) return 0;

        var docs = await QueryDoctorSchedulesMissingRoomAsync(cancellationToken);
        var collection = _firestore.Collection(_settings.DoctorScheduleCollections.First());
        var updatedCount = 0;

        foreach (var doc in docs)
        {
            var currentRoom = GetString(doc, "roomNumber", "room", "roomId", "roomName");
            if (!string.IsNullOrWhiteSpace(currentRoom)) continue;
            if (await HasAppointmentsForScheduleAsync(doc.Id, cancellationToken)) continue;

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
                ["roomId"] = string.IsNullOrWhiteSpace(model.RoomId) ? NormalizeRoomId(model.RoomNumber) : model.RoomId.Trim(),
                ["roomNumber"] = model.RoomNumber.Trim(),
                ["updatedAt"] = Timestamp.GetCurrentTimestamp()
            }, cancellationToken: cancellationToken);
            updatedCount++;
        }

        return updatedCount;
    }

    private async Task<IReadOnlyList<DocumentSnapshot>> QueryDoctorSchedulesByDateRangeAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var result = new List<DocumentSnapshot>();
        var start = ToScheduleTimestamp(startDate);
        var endExclusive = ToScheduleTimestamp(endDate.AddDays(1));

        foreach (var collectionName in _settings.DoctorScheduleCollections
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal))
        {
            var snapshot = await _firestore.Collection(collectionName)
                .WhereGreaterThanOrEqualTo("scheduleDate", start)
                .WhereLessThan("scheduleDate", endExclusive)
                .GetSnapshotAsync(cancellationToken);

            result.AddRange(snapshot.Documents);
        }

        return result;
    }

    private async Task<IReadOnlyList<DocumentSnapshot>> QueryDoctorSchedulesByDoctorAsync(
        string doctorId,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(doctorId)) return Array.Empty<DocumentSnapshot>();

        var result = new List<DocumentSnapshot>();
        var ids = new[] { doctorId.Trim(), userId?.Trim() }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var collectionName in _settings.DoctorScheduleCollections
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal))
        {
            foreach (var id in ids)
            {
                var byDoctor = await _firestore.Collection(collectionName)
                    .WhereEqualTo("doctorId", id)
                    .GetSnapshotAsync(cancellationToken);
                result.AddRange(byDoctor.Documents);

                var byUser = await _firestore.Collection(collectionName)
                    .WhereEqualTo("userId", id)
                    .GetSnapshotAsync(cancellationToken);
                result.AddRange(byUser.Documents);

                var byDoctorUser = await _firestore.Collection(collectionName)
                    .WhereEqualTo("doctorUserId", id)
                    .GetSnapshotAsync(cancellationToken);
                result.AddRange(byDoctorUser.Documents);
            }
        }

        return result
            .GroupBy(x => x.Reference.Path, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    private async Task<IReadOnlyList<DocumentSnapshot>> QueryDoctorSchedulesMissingRoomAsync(CancellationToken cancellationToken)
    {
        var result = new List<DocumentSnapshot>();

        foreach (var collectionName in _settings.DoctorScheduleCollections
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal))
        {
            var snapshot = await _firestore.Collection(collectionName)
                .WhereEqualTo("roomNumber", null)
                .GetSnapshotAsync(cancellationToken);

            result.AddRange(snapshot.Documents);
        }

        return result;
    }

    private async Task<IReadOnlyList<DocumentSnapshot>> QueryDoctorSchedulesForConflictAsync(
        DateOnly scheduleDate,
        string shiftId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(shiftId)) return Array.Empty<DocumentSnapshot>();

        var result = new List<DocumentSnapshot>();
        var start = ToScheduleTimestamp(scheduleDate);
        var endExclusive = ToScheduleTimestamp(scheduleDate.AddDays(1));

        foreach (var collectionName in _settings.DoctorScheduleCollections
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal))
        {
            var snapshot = await _firestore.Collection(collectionName)
                .WhereGreaterThanOrEqualTo("scheduleDate", start)
                .WhereLessThan("scheduleDate", endExclusive)
                .GetSnapshotAsync(cancellationToken);

            result.AddRange(snapshot.Documents.Where(doc =>
                string.Equals(
                    GetString(doc, "shiftId", "ShiftId", "shiftType", "type", "ShiftType"),
                    shiftId.Trim(),
                    StringComparison.OrdinalIgnoreCase)));
        }

        return result;
    }

    private static Timestamp ToScheduleTimestamp(DateOnly date)
    {
        return Timestamp.FromDateTime(date.ToDateTime(TimeOnly.MinValue).ToUniversalTime());
    }

    private static IReadOnlyList<(DayOfWeek Day, string ShiftId)> BuildDayShiftSelections(WorkScheduleGenerateViewModel model)
    {
        var result = new List<(DayOfWeek Day, string ShiftId)>();

        foreach (var value in model.DayShiftSelections.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var parts = value.Split('|', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) continue;
            if (!Enum.TryParse<DayOfWeek>(parts[0], true, out var day)) continue;
            if (string.IsNullOrWhiteSpace(parts[1])) continue;

            result.Add((day, parts[1]));
        }

        if (result.Count == 0)
        {
            result.AddRange(model.DaysOfWeek.SelectMany(day => model.ShiftIds
                .Where(shiftId => !string.IsNullOrWhiteSpace(shiftId))
                .Select(shiftId => (day, shiftId))));
        }

        return result
            .GroupBy(x => $"{x.Day}|{x.ShiftId}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
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
                GetString(x, "roomNumber", "room", "roomName", "location") ?? string.Empty),
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
                        .Select(GetRoomDisplayName)
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

        if (value is Dictionary<string, object?> nullableMap)
        {
            return GetNullableRoomField(nullableMap, "roomNumber")
                ?? GetNullableRoomField(nullableMap, "name")
                ?? GetNullableRoomField(nullableMap, "roomId");
        }

        return value.ToString()?.Trim();
    }

    private static string? GetRoomField(IDictionary<string, object> map, string fieldName)
    {
        if (!map.TryGetValue(fieldName, out var value)) return null;

        var text = value?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? GetNullableRoomField(IReadOnlyDictionary<string, object?> map, string fieldName)
    {
        if (!map.TryGetValue(fieldName, out var value)) return null;

        var text = value?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string FormatShiftTime(ShiftInfo shift)
    {
        if (string.IsNullOrWhiteSpace(shift.StartTime) && string.IsNullOrWhiteSpace(shift.EndTime)) return string.Empty;
        return $"{shift.StartTime} - {shift.EndTime}";
    }

    private static string FormatSequentialCode(string prefix, int number)
    {
        return $"{prefix}{Math.Max(1, number):0000}";
    }

    private static string FormatLegacySequentialCode(string prefix, int number)
    {
        return $"{prefix}{Math.Max(1, number):000}";
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
        _ => "Ca làm việc"
    };

    private static string ShiftPrefix(ShiftType type) => type switch
    {
        ShiftType.Morning => "S",
        ShiftType.Afternoon => "C",
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
                Room = NormalizeRoom(GetString(x, "roomNumber", "room", "roomId", "roomName"))
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.DoctorId) && x.Date.HasValue && !string.IsNullOrWhiteSpace(x.Shift))
            .GroupBy(x => $"{x.DoctorId}|{x.Date}|{x.Shift}", StringComparer.OrdinalIgnoreCase)
            .Count(x => x.Count() > 1) +
            docs
                .Select(x => new
                {
                    Date = GetScheduleDate(x),
                    Shift = GetString(x, "shiftId", "ShiftId", "shiftType", "type", "ShiftType"),
                    Room = NormalizeRoom(GetString(x, "roomNumber", "room", "roomId", "roomName"))
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
        string? ignoredDocumentId = null,
        string? departmentId = null,
        string? roomId = null,
        IReadOnlySet<string>? validDoctorIds = null)
    {
        var normalizedRoom = NormalizeRoom(room);
        var normalizedRoomId = NormalizeRoom(roomId);
        var normalizedDepartmentId = departmentId?.Trim();

        foreach (var doc in docs)
        {
            if (!string.IsNullOrWhiteSpace(ignoredDocumentId) &&
                string.Equals(doc.Id, ignoredDocumentId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var isActive = GetBool(doc, "isActive", "IsActive") ??
                !string.Equals(GetString(doc, "status", "Status"), "unavailable", StringComparison.OrdinalIgnoreCase);
            if (!isActive) continue;

            var existingDate = GetScheduleDate(doc);
            if (existingDate != date) continue;

            var existingShift = GetString(doc, "shiftId", "ShiftId", "shiftType", "type", "ShiftType");
            if (!string.Equals(existingShift, shiftId, StringComparison.OrdinalIgnoreCase)) continue;

            var existingDoctor = GetString(doc, "doctorId", "DoctorId", "doctorUserId", "DoctorUserId", "userId", "UserId");
            if (validDoctorIds is not null &&
                !string.IsNullOrWhiteSpace(existingDoctor) &&
                !validDoctorIds.Contains(existingDoctor))
            {
                continue;
            }

            if (string.Equals(existingDoctor, doctorId, StringComparison.OrdinalIgnoreCase)) return true;

            var existingDepartmentId = GetString(doc, "departmentId", "DepartmentId")?.Trim();
            var sameDepartment = !string.IsNullOrWhiteSpace(normalizedDepartmentId) &&
                string.Equals(existingDepartmentId, normalizedDepartmentId, StringComparison.OrdinalIgnoreCase);
            if (!sameDepartment) continue;

            var existingRoomId = NormalizeRoom(GetString(doc, "roomId", "RoomId"));
            var existingRoom = NormalizeRoom(GetString(doc, "roomNumber", "room", "roomName", "clinicRoom", "location"));
            var sameRoomId = !string.IsNullOrWhiteSpace(normalizedRoomId) &&
                string.Equals(existingRoomId, normalizedRoomId, StringComparison.OrdinalIgnoreCase);
            var sameRoomNumber = !string.IsNullOrWhiteSpace(normalizedRoom) &&
                string.Equals(existingRoom, normalizedRoom, StringComparison.OrdinalIgnoreCase);

            if (sameRoomId || sameRoomNumber)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> HasAppointmentsForScheduleAsync(string scheduleId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scheduleId)) return false;

        foreach (var collectionName in _settings.AppointmentCollections.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal))
        {
            var snapshot = await _firestore.Collection(collectionName)
                .WhereEqualTo("scheduleId", scheduleId)
                .Limit(1)
                .GetSnapshotAsync(cancellationToken);

            if (snapshot.Count > 0) return true;
        }

        return false;
    }

    private static string NormalizeRoom(string? room)
    {
        return room?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static string NormalizeRoomId(string? roomNumber)
    {
        if (string.IsNullOrWhiteSpace(roomNumber)) return string.Empty;

        return roomNumber.Trim()
            .ToLowerInvariant()
            .Replace(" ", "_", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .Replace("\\", "_", StringComparison.Ordinal);
    }

    private static string BuildPatientScheduleLockId(string patientId, DateTime date, string shiftId)
    {
        var raw = $"{patientId}_{date:yyyyMMdd}_{shiftId}";
        return new string(raw
            .Select(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_')
            .ToArray());
    }

    private static WeeklyScheduleTableViewModel BuildWeeklyScheduleTable(
        IReadOnlyList<DoctorScheduleRowViewModel> schedules,
        DateOnly weekStart,
        DateOnly weekEnd)
    {
        var days = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var date = weekStart.AddDays(offset);
                return new WeeklyScheduleDayViewModel
                {
                    Date = date,
                    Label = $"{DayText(date.DayOfWeek).ToUpperInvariant()} ({date:dd/MM})",
                    IsToday = date == DateOnly.FromDateTime(DateTime.Today)
                };
            })
            .ToList();

        var rows = schedules
            .Where(x => x.Date >= weekStart && x.Date <= weekEnd)
            .GroupBy(x => string.IsNullOrWhiteSpace(x.RoomNumber)
                ? $"{x.DepartmentName}|_no_room"
                : $"{x.DepartmentName}|{x.RoomNumber}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.First().DepartmentName)
            .ThenBy(x => x.First().RoomNumber)
            .Select((group, index) =>
            {
                var first = group.First();
                var itemsByDate = days.ToDictionary(
                    day => day.Date,
                    day => (IReadOnlyList<WeeklyScheduleCellItemViewModel>)group
                        .Where(x => x.Date == day.Date)
                        .OrderBy(x => ShiftSortKey(x.ShiftName, x.ShiftTime))
                        .ThenBy(x => x.DoctorName)
                        .Select(x => new WeeklyScheduleCellItemViewModel
                        {
                            DoctorName = x.DoctorName,
                            ShiftName = x.ShiftName,
                            ShiftTime = x.ShiftTime,
                            ShiftType = x.ShiftType,
                            IsActive = x.IsActive,
                            AvailableSlots = x.AvailableSlots,
                            MaxSlots = x.MaxSlots
                        })
                        .ToList());

                return new WeeklyScheduleRoomRowViewModel
                {
                    Index = index + 1,
                    DepartmentName = string.IsNullOrWhiteSpace(first.DepartmentName) ? "Chưa rõ khoa" : first.DepartmentName,
                    RoomNumber = string.IsNullOrWhiteSpace(first.RoomNumber) ? "Chưa gán phòng" : first.RoomNumber,
                    LocationLabel = BuildRoomLocationLabel(first.RoomNumber),
                    ItemsByDate = itemsByDate
                };
            })
            .ToList();

        return new WeeklyScheduleTableViewModel
        {
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            Days = days,
            Rows = rows
        };
    }

    private static string BuildRoomLocationLabel(string? roomNumber)
    {
        if (string.IsNullOrWhiteSpace(roomNumber)) return string.Empty;
        return roomNumber.Contains("tầng", StringComparison.OrdinalIgnoreCase)
            ? roomNumber.Trim()
            : $"Phòng {roomNumber.Trim()}";
    }

    private static string DayText(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Thứ 2",
        DayOfWeek.Tuesday => "Thứ 3",
        DayOfWeek.Wednesday => "Thứ 4",
        DayOfWeek.Thursday => "Thứ 5",
        DayOfWeek.Friday => "Thứ 6",
        DayOfWeek.Saturday => "Thứ 7",
        DayOfWeek.Sunday => "Chủ nhật",
        _ => "-"
    };

    private static int ShiftSortKey(string shiftName, string shiftTime)
    {
        var value = $"{shiftName} {shiftTime}".ToLowerInvariant();
        if (value.Contains("sáng") || value.Contains("morning") || value.Contains("07:") || value.Contains("08:")) return 1;
        if (value.Contains("chiều") || value.Contains("afternoon") || value.Contains("13:")) return 2;
        return 9;
    }

    private static int DayOfWeekToMondayBased(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => 0,
            DayOfWeek.Tuesday => 1,
            DayOfWeek.Wednesday => 2,
            DayOfWeek.Thursday => 3,
            DayOfWeek.Friday => 4,
            DayOfWeek.Saturday => 5,
            DayOfWeek.Sunday => 6,
            _ => 0
        };
    }

    private sealed record ShiftInfo(
        string Name,
        string? Type,
        string? StartTime,
        string? EndTime,
        string RoomNumber)
    {
        public static ShiftInfo FromSchedule(DocumentSnapshot doc)
        {
            return new ShiftInfo(
                GetString(doc, "shiftName", "name", "title") ?? string.Empty,
                GetString(doc, "shiftType", "type", "ShiftType"),
                GetString(doc, "startTime", "start", "from"),
                GetString(doc, "endTime", "end", "to"),
                GetString(doc, "roomNumber", "room", "roomName", "location") ?? string.Empty);
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
        var fallbackCollection = await GetFirstAvailableCollectionNameAsync(collectionNames, cancellationToken);
        return await _firestore.Collection(fallbackCollection).GetSnapshotAsync(cancellationToken);
    }

    private async Task<string> GetFirstAvailableCollectionNameAsync(string[] collectionNames, CancellationToken cancellationToken)
    {
        var normalizedNames = collectionNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var collectionName in normalizedNames)
        {
            var snapshot = await _firestore.Collection(collectionName).Limit(1).GetSnapshotAsync(cancellationToken);
            if (snapshot.Count > 0)
            {
                return collectionName;
            }
        }

        return normalizedNames.FirstOrDefault() ?? "_missing_collection";
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

    private static MedicalRecordListItemViewModel MapMedicalRecordListItem(
        DocumentSnapshot doc,
        IReadOnlyDictionary<string, AppointmentUserInfo> users,
        IReadOnlyList<AppointmentDoctorInfo> doctors,
        IReadOnlyDictionary<string, string> departments,
        bool isAppointmentSource)
    {
        var patientId = GetString(doc, "patientId", "PatientId", "userId", "UserId", "customerId") ?? string.Empty;
        var doctorId = GetString(doc, "doctorId", "DoctorId");
        var doctorUserId = GetString(doc, "doctorUserId", "DoctorUserId");
        var departmentId = GetString(doc, "departmentId", "DepartmentId", "specialtyId", "SpecialtyId");
        users.TryGetValue(patientId, out var patient);
        var doctor = ResolveAppointmentDoctor(doctors, doctorId, doctorUserId);

        return new MedicalRecordListItemViewModel
        {
            Id = doc.Id,
            RecordCode = GetString(doc, "recordCode", "code", "medicalRecordCode", "appointmentCode", "bookingCode")
                ?? (isAppointmentSource ? $"LH-{doc.Id}" : $"BA-{doc.Id}"),
            PatientId = patientId,
            PatientName = patient?.FullName
                ?? GetString(doc, "patientName", "PatientName", "customerName")
                ?? "Chưa có tên",
            DoctorName = doctor?.FullName
                ?? GetString(doc, "doctorName", "DoctorName")
                ?? "Chưa phân bác sĩ",
            SpecialtyName = ResolveDepartmentName(doc, departments, departmentId, doctor),
            CreatedAt = GetDateTime(doc, "createdAt", "CreatedAt", "appointmentDate", "scheduledAt", "date")
                ?? DateTime.MinValue,
            Status = ParseMedicalRecordStatus(GetString(doc, "status", "Status"))
        };
    }

    private static MedicalRecordDetailsViewModel MapMedicalRecordDetails(
        DocumentSnapshot doc,
        IReadOnlyDictionary<string, AppointmentUserInfo> users,
        IReadOnlyList<AppointmentDoctorInfo> doctors,
        IReadOnlyDictionary<string, string> departments,
        bool isAppointmentSource)
    {
        var item = MapMedicalRecordListItem(doc, users, doctors, departments, isAppointmentSource);
        users.TryGetValue(item.PatientId, out var patient);

        return new MedicalRecordDetailsViewModel
        {
            Id = item.Id,
            RecordCode = item.RecordCode,
            PatientName = item.PatientName,
            PatientPhone = patient?.Phone
                ?? GetString(doc, "patientPhone", "phone", "phoneNumber", "PatientPhone"),
            DoctorName = item.DoctorName,
            SpecialtyName = item.SpecialtyName,
            CreatedAt = item.CreatedAt,
            Status = item.Status,
            Diagnosis = GetString(doc, "diagnosis", "Diagnosis", "diagnostic", "result"),
            ClinicalNotes = GetString(doc, "clinicalNotes", "ClinicalNotes", "symptoms", "reason", "note", "description"),
            Prescription = GetMedicalText(
                doc,
                "prescription",
                "Prescription",
                "prescriptionItems",
                "PrescriptionItems",
                "items",
                "medicines",
                "medicineItems",
                "medications",
                "drugs",
                "medicineNote")
        };
    }

    private static string? GetMedicalText(DocumentSnapshot doc, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (!doc.ContainsField(fieldName)) continue;

            try
            {
                var value = doc.GetValue<object?>(fieldName);
                var text = FormatMedicalValue(value);
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static string? FormatMedicalValue(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case string text:
                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            case Timestamp timestamp:
                return timestamp.ToDateTime().ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
            case IDictionary<string, object> map:
                return FormatPrescriptionMap(map);
            case IEnumerable<object> items:
                var lines = items
                    .Select((item, index) => FormatPrescriptionItem(item, index + 1))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
                return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
            default:
                var fallback = value.ToString();
                return string.IsNullOrWhiteSpace(fallback) ? null : fallback.Trim();
        }
    }

    private static string? FormatPrescriptionItem(object? value, int index)
    {
        if (value is IDictionary<string, object> map)
        {
            var name = GetPrescriptionMapString(map, "medicineName", "name", "drugName", "tenThuoc", "displayName", "medicineCode", "code");
            var strength = GetPrescriptionMapString(map, "strength", "dosage", "hamLuong", "concentration");
            var quantity = GetPrescriptionMapString(map, "quantity", "qty", "soLuong");
            var unit = GetPrescriptionMapString(map, "unit", "donVi");
            var instruction = GetPrescriptionMapString(map, "instruction", "instructions", "usage", "note", "lieuDung", "cachDung");

            var main = string.Join(" ", new[] { name, strength }.Where(x => !string.IsNullOrWhiteSpace(x)));
            var amount = string.Join(" ", new[] { quantity, unit }.Where(x => !string.IsNullOrWhiteSpace(x)));
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(main)) parts.Add(main);
            if (!string.IsNullOrWhiteSpace(amount)) parts.Add($"SL: {amount}");
            if (!string.IsNullOrWhiteSpace(instruction)) parts.Add(instruction);

            return parts.Count == 0 ? null : $"{index}. {string.Join(" - ", parts)}";
        }

        var text = FormatMedicalValue(value);
        return string.IsNullOrWhiteSpace(text) ? null : $"{index}. {text}";
    }

    private static string? FormatPrescriptionMap(IDictionary<string, object> map)
    {
        var nestedItems = GetMapValue(map, "items", "medicines", "medicineItems", "prescriptionItems", "medications", "drugs");
        if (nestedItems is not null)
        {
            var nestedText = FormatMedicalValue(nestedItems);
            if (!string.IsNullOrWhiteSpace(nestedText)) return nestedText;
        }

        var name = GetPrescriptionMapString(map, "medicineName", "name", "drugName", "tenThuoc", "displayName", "medicineCode", "code");
        var strength = GetPrescriptionMapString(map, "strength", "dosage", "hamLuong", "concentration");
        var quantity = GetPrescriptionMapString(map, "quantity", "qty", "soLuong");
        var unit = GetPrescriptionMapString(map, "unit", "donVi");
        var instruction = GetPrescriptionMapString(map, "instruction", "instructions", "usage", "note", "lieuDung", "cachDung");

        var parts = new List<string>();
        var main = string.Join(" ", new[] { name, strength }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var amount = string.Join(" ", new[] { quantity, unit }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (!string.IsNullOrWhiteSpace(main)) parts.Add(main);
        if (!string.IsNullOrWhiteSpace(amount)) parts.Add($"SL: {amount}");
        if (!string.IsNullOrWhiteSpace(instruction)) parts.Add(instruction);

        return parts.Count == 0
            ? string.Join(Environment.NewLine, map.Select(x => $"{x.Key}: {x.Value}"))
            : string.Join(" - ", parts);
    }

    private static object? GetMapValue(IDictionary<string, object> map, params string[] keys)
    {
        foreach (var key in keys)
        {
            var match = map.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Key)) return match.Value;
        }

        return null;
    }

    private static string? GetPrescriptionMapString(IDictionary<string, object> map, params string[] keys)
    {
        var value = GetMapValue(map, keys);
        return value?.ToString()?.Trim();
    }

    private static string ResolveDepartmentName(
        DocumentSnapshot doc,
        IReadOnlyDictionary<string, string> departments,
        string? departmentId,
        AppointmentDoctorInfo? doctor)
    {
        if (!string.IsNullOrWhiteSpace(departmentId) &&
            departments.TryGetValue(departmentId, out var departmentName) &&
            !string.IsNullOrWhiteSpace(departmentName))
        {
            return departmentName;
        }

        if (!string.IsNullOrWhiteSpace(doctor?.DepartmentId) &&
            departments.TryGetValue(doctor.DepartmentId, out var doctorDepartmentName) &&
            !string.IsNullOrWhiteSpace(doctorDepartmentName))
        {
            return doctorDepartmentName;
        }

        return GetString(doc, "specialtyName", "departmentName", "DepartmentName", "department", "specialization")
            ?? doctor?.Specialization
            ?? "Chưa cập nhật";
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
                GetString(x, "patientCode", "userCode", "code") ?? x.Id,
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
                GetString(doctor, "departmentId", "DepartmentId"),
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
        string Code,
        string FullName,
        string Phone,
        string Email);

    private sealed record AppointmentDoctorInfo(
        string DocumentId,
        string? UserId,
        string? DepartmentId,
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
                    string s when s.Equals("active", StringComparison.OrdinalIgnoreCase) => true,
                    string s when s.Equals("enabled", StringComparison.OrdinalIgnoreCase) => true,
                    string s when s.Equals("available", StringComparison.OrdinalIgnoreCase) => true,
                    string s when s.Equals("inactive", StringComparison.OrdinalIgnoreCase) => false,
                    string s when s.Equals("disabled", StringComparison.OrdinalIgnoreCase) => false,
                    string s when s.Equals("unavailable", StringComparison.OrdinalIgnoreCase) => false,
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

    private static int? GetNullableInt(DocumentSnapshot snapshot, params string[] fieldNames)
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

    private static long? GetLong(DocumentSnapshot snapshot, params string[] fieldNames)
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
                    long l => l,
                    double d => Convert.ToInt64(d),
                    decimal d => Convert.ToInt64(d),
                    string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
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
            ("Bác sĩ nổi bật", FormatBooleanText(doctor.IsFeatured)),
            ("Thứ hạng nổi bật", doctor.FeaturedRank?.ToString()),
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
            "verified" or "approved" => "Đã xác minh",
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

    private static string NormalizeEmail(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static string NormalizeStaffRole(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "staff" or "cashier" or "receptionist" or "staff_manager" => "staff",
            "admin" => "admin",
            "doctor" => "doctor",
            "patient" => "patient",
            _ => string.Empty
        };
    }

    private static string NormalizeStaffStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "inactive" or "disabled" or "blocked" => "inactive",
            _ => "active"
        };
    }

    private static string NormalizeDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        return new string(value.Where(char.IsDigit).ToArray());
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
            "cancel_requested" or "cancelrequested" => AppointmentStatus.CancelRequested,
            "cancelled" or "canceled" => AppointmentStatus.Cancelled,
            "no_show" or "noshow" => AppointmentStatus.NoShow,
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

public sealed class UserUniqueCheckResult
{
    public bool EmailExists { get; set; }
    public bool PhoneExists { get; set; }
    public bool CccdExists { get; set; }
}

