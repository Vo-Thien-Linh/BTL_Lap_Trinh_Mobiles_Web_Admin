using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;

namespace Web_Admin_Booking_App.Services;

public sealed class FirebaseAdminUserService
{
    private readonly FirestoreDb _firestoreDb;
    private readonly FirebaseSettings _settings;

    public FirebaseAdminUserService(
        FirestoreDb firestoreDb,
        IOptions<FirebaseSettings> options)
    {
        _firestoreDb = firestoreDb;
        _settings = options.Value;
    }

    public async Task<AdminUserInfo?> GetAdminUserAsync(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            return null;
        }

        IEnumerable<string> collections =
            _settings.UserCollections != null && _settings.UserCollections.Any()
                ? _settings.UserCollections
                : new[] { "users" };

        foreach (var collectionName in collections)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                continue;
            }

            var docRef = _firestoreDb.Collection(collectionName).Document(uid);
            var snapshot = await docRef.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                continue;
            }

            var user = MapAdminUser(snapshot);
            user.Uid = uid;

            return user;
        }

        return null;
    }

    private static AdminUserInfo MapAdminUser(DocumentSnapshot snapshot)
    {
        var data = snapshot.ToDictionary();

        static string GetString(Dictionary<string, object> data, string fieldName)
        {
            if (!data.TryGetValue(fieldName, out var value))
            {
                return string.Empty;
            }

            return value?.ToString() ?? string.Empty;
        }

        return new AdminUserInfo
        {
            Uid = snapshot.Id,
            Email = GetString(data, "email"),
            FullName = GetString(data, "fullName"),
            Role = GetString(data, "role"),
            Status = GetString(data, "status")
        };
    }
}

public sealed class AdminUserInfo
{
    public string Uid { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
