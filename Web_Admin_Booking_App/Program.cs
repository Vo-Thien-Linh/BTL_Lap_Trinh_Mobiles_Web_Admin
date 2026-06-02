using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Web_Admin_Booking_App.Services;

var builder = WebApplication.CreateBuilder(args);

// Debug kiểm tra appsettings.json có được đọc đúng không
var firebaseApiKey = builder.Configuration["Firebase:WebApiKey"];
Console.WriteLine("Firebase WebApiKey loaded: " +
    (string.IsNullOrWhiteSpace(firebaseApiKey)
        ? "EMPTY"
        : firebaseApiKey.Substring(0, Math.Min(8, firebaseApiKey.Length)) + "..."));

// Đọc cấu hình Firebase từ appsettings.json
builder.Services.Configure<FirebaseSettings>(
    builder.Configuration.GetSection(FirebaseSettings.SectionName)
);

// Đăng ký Firestore
builder.Services.AddSingleton<FirestoreDbFactory>();

builder.Services.AddSingleton<FirestoreDb>(sp =>
{
    var factory = sp.GetRequiredService<FirestoreDbFactory>();
    return factory.CreateFirestoreDb();
});

// Đăng ký service Firebase Auth REST API
builder.Services.AddHttpClient<FirebaseAuthRestService>();
builder.Services.AddMemoryCache();

// Đăng ký service xử lý admin user và dữ liệu Firestore
builder.Services.AddScoped<FirebaseAdminUserService>();
builder.Services.AddScoped<FirestoreAdminDataService>();

// Cấu hình đăng nhập bằng Cookie
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// Bắt buộc toàn bộ Web Admin phải đăng nhập quyền admin,
// trừ những action có [AllowAnonymous]
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole("admin")
        .Build();

    options.Filters.Add(new AuthorizeFilter(policy));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Khi chạy local, để tránh lỗi https redirect thì tạm thời không bật dòng này.
// app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();
