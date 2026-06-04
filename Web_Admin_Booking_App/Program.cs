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
builder.Services.Configure<PayOsSettings>(
    builder.Configuration.GetSection(PayOsSettings.SectionName)
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
builder.Services.AddHttpClient<PayOsService>();
builder.Services.AddMemoryCache();

// Đăng ký service xử lý admin user và dữ liệu Firestore
builder.Services.AddScoped<FirebaseAdminUserService>();
builder.Services.AddScoped<CodeGeneratorService>();
builder.Services.AddScoped<FirestoreAdminDataService>();
builder.Services.AddScoped<PatientProfileResolver>();

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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
    options.AddPolicy("StaffOnly", policy => policy.RequireRole("staff"));
    options.AddPolicy("AdminOrStaff", policy => policy.RequireRole("admin", "staff"));
    options.AddPolicy("DoctorOnly", policy => policy.RequireRole("doctor"));
});

// Bắt buộc đăng nhập toàn bộ Web Admin/Staff, phân quyền cụ thể bằng policy ở controller/action.
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.Filters.Add(new AuthorizeFilter(policy));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentFlutter", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost",
                "http://127.0.0.1",
                "http://localhost:3000",
                "http://localhost:5000",
                "http://localhost:5071",
                "http://localhost:5173",
                "http://localhost:8080",
                "http://127.0.0.1:3000",
                "http://127.0.0.1:5000",
                "http://127.0.0.1:5071",
                "http://127.0.0.1:5173",
                "http://127.0.0.1:8080")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
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

if (app.Environment.IsDevelopment())
{
    app.UseCors("DevelopmentFlutter");
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();
