using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Web_Admin_Booking_App.Services;

public sealed class FirebaseAuthRestService
{
    private readonly HttpClient _httpClient;
    private readonly FirebaseSettings _settings;

    public FirebaseAuthRestService(
        HttpClient httpClient,
        IOptions<FirebaseSettings> options)
    {
        _httpClient = httpClient;
        _settings = options.Value;
    }

    public async Task<FirebaseSignInResult> SignInWithPasswordAsync(
        string email,
        string password)
    {
        ValidateWebApiKey();

        var url =
            $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_settings.WebApiKey}";

        var payload = new
        {
            email,
            password,
            returnSecureToken = true
        };

        var response = await _httpClient.PostAsJsonAsync(url, payload);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(GetFirebaseErrorMessage(responseText));
        }

        return ParseAuthResult(responseText, "Không đọc được thông tin đăng nhập Firebase.");
    }

    public async Task<FirebaseSignInResult> CreateUserWithPasswordAsync(
        string email,
        string password)
    {
        ValidateWebApiKey();

        var url =
            $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={_settings.WebApiKey}";

        var payload = new
        {
            email,
            password,
            returnSecureToken = true
        };

        var response = await _httpClient.PostAsJsonAsync(url, payload);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(GetFirebaseErrorMessage(responseText));
        }

        return ParseAuthResult(responseText, "Không đọc được UID tài khoản Firebase Auth vừa tạo.");
    }

    public async Task SendPasswordResetEmailAsync(string email)
    {
        ValidateWebApiKey();

        var url =
            $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={_settings.WebApiKey}";

        var payload = new
        {
            requestType = "PASSWORD_RESET",
            email
        };

        var response = await _httpClient.PostAsJsonAsync(url, payload);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(GetFirebaseErrorMessage(responseText));
        }
    }

    private void ValidateWebApiKey()
    {
        if (string.IsNullOrWhiteSpace(_settings.WebApiKey) ||
            _settings.WebApiKey == "YOUR_FIREBASE_WEB_API_KEY")
        {
            throw new InvalidOperationException("Thiếu Firebase:WebApiKey trong appsettings.json.");
        }
    }

    private static FirebaseSignInResult ParseAuthResult(string responseText, string errorMessage)
    {
        var result = JsonSerializer.Deserialize<FirebaseSignInResult>(
            responseText,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (result == null || string.IsNullOrWhiteSpace(result.LocalId))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return result;
    }

    private static string GetFirebaseErrorMessage(string responseText)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseText);
            var message = doc.RootElement
                .GetProperty("error")
                .GetProperty("message")
                .GetString();

            return message switch
            {
                "EMAIL_EXISTS" => "Email này đã tồn tại trên Firebase Auth.",
                "EMAIL_NOT_FOUND" => "Email không tồn tại.",
                "INVALID_EMAIL" => "Email không đúng định dạng.",
                "INVALID_PASSWORD" => "Mật khẩu không đúng.",
                "INVALID_LOGIN_CREDENTIALS" => "Email hoặc mật khẩu không đúng.",
                "USER_DISABLED" => "Tài khoản đã bị vô hiệu hóa.",
                "TOO_MANY_ATTEMPTS_TRY_LATER" => "Bạn thử quá nhiều lần. Vui lòng thử lại sau.",
                "WEAK_PASSWORD : Password should be at least 6 characters" => "Mật khẩu phải có ít nhất 6 ký tự.",
                _ => $"Firebase Auth error: {message}"
            };
        }
        catch
        {
            return "Không thể kết nối Firebase Auth. Vui lòng kiểm tra cấu hình.";
        }
    }
}

public sealed class FirebaseSignInResult
{
    public string LocalId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string IdToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string ExpiresIn { get; set; } = string.Empty;
}
