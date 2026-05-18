using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;

namespace Web_Admin_Booking_App.Services;

public sealed class FirestoreDbFactory
{
    private readonly FirebaseSettings _settings;
    private readonly IWebHostEnvironment _environment;

    public FirestoreDbFactory(
        IOptions<FirebaseSettings> options,
        IWebHostEnvironment environment)
    {
        _settings = options.Value;
        _environment = environment;
    }

    public FirestoreDb CreateFirestoreDb()
    {
        if (string.IsNullOrWhiteSpace(_settings.ProjectId) ||
            _settings.ProjectId == "YOUR_FIREBASE_PROJECT_ID")
        {
            throw new InvalidOperationException(
                "Thiếu Firebase:ProjectId trong appsettings.json hoặc User Secrets.");
        }

        var builder = new FirestoreDbBuilder
        {
            ProjectId = _settings.ProjectId,
        };

        if (!string.IsNullOrWhiteSpace(_settings.ServiceAccountJson))
        {
            builder.Credential = GoogleCredential.FromJson(_settings.ServiceAccountJson);
        }
        else if (!string.IsNullOrWhiteSpace(_settings.ServiceAccountPath))
        {
            var path = _settings.ServiceAccountPath;

            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(_environment.ContentRootPath, path);
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Không tìm thấy file service account Firebase. Hãy tải service account JSON từ Firebase Console và đặt đúng đường dẫn Firebase:ServiceAccountPath.",
                    path);
            }

            builder.Credential = GoogleCredential.FromFile(path);
        }

        return builder.Build();
    }
}
