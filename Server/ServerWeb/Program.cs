using Microsoft.EntityFrameworkCore;
using ServerWeb.Data;
using Microsoft.AspNetCore.Authentication.Cookies; // Thêm dòng này

var builder = WebApplication.CreateBuilder(args);

// 1. Kết nối Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. 🔥 CẤU HÌNH XÁC THỰC COOKIE (Khắc phục lỗi InvalidOperationException)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login"; // Đường dẫn nếu chưa đăng nhập
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.Cookie.Name = "MusicApp_Auth"; // Tên Cookie lưu trên máy khách
        options.ExpireTimeSpan = TimeSpan.FromDays(7); // Ghi nhớ trong 7 ngày
    });

// 3. Add services to the container.
builder.Services.AddControllersWithViews();

// 4. Add Session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Thay cho MapStaticAssets nếu bạn dùng bản .NET cũ hơn, hoặc giữ nguyên nếu dùng .NET 9

app.UseRouting();

// 5. THỨ TỰ MIDDLEWARE (Rất quan trọng)
app.UseSession();         // 1. Session trước
app.UseAuthentication();  // 2. Authentication (Xác thực) - PHẢI TRƯỚC Authorization
app.UseAuthorization();   // 3. Authorization (Phân quyền)

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();