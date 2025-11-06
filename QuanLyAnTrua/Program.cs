using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuanLyAnTrua.Data;
using QuanLyAnTrua.Helpers;
using QuanLyAnTrua.Models;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình Serilog - đọc từ appsettings.json
// Log file sẽ được lưu trong thư mục logs/ của ứng dụng
var logPath = Path.Combine(builder.Environment.ContentRootPath, "logs", "app-.log");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("QuanLyAnTrua.Controllers.CassoWebhookController", LogEventLevel.Information)
    .WriteTo.File(
        path: logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

// Sử dụng Serilog
builder.Host.UseSerilog();

// Khởi tạo IdEncoderHelper với prefix từ configuration
IdEncoderHelper.Initialize(builder.Configuration);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(30); // 30 ngày
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.Name = ".QuanLyAnTrua.Session";
    options.Cookie.MaxAge = TimeSpan.FromDays(30); // Cookie tồn tại 30 ngày
});

// Configure SQLite Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=QuanLyAnTrua.db"));

var app = builder.Build();

// Use session
app.UseSession();

// Apply migrations and seed database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();

    // Apply migrations (quan trọng khi publish)
    try
    {
        await context.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        // Log error nhưng không crash app
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }

    // Seed SuperAdmin user if not exists (check by name)
    var superAdminExists = await context.Users.AnyAsync(u => u.Name == "Ngô Tiến Đạt" && u.Role == "SuperAdmin");
    if (!superAdminExists)
    {
        // Check if user exists but with different role, update it
        var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Name == "Ngô Tiến Đạt");
        if (existingUser != null)
        {
            existingUser.Role = "SuperAdmin";
            existingUser.GroupId = null; // SuperAdmin không thuộc group nào
            existingUser.Username = "ngotiendat";
            if (string.IsNullOrEmpty(existingUser.PasswordHash))
            {
                existingUser.PasswordHash = PasswordHelper.HashPassword("123456");
            }
            context.Users.Update(existingUser);
        }
        else
        {
            var superAdminUser = new User
            {
                Name = "Ngô Tiến Đạt",
                Username = "ngotiendat",
                PasswordHash = PasswordHelper.HashPassword("123456"),
                Role = "SuperAdmin",
                GroupId = null, // SuperAdmin không thuộc group nào
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            context.Users.Add(superAdminUser);
        }
        await context.SaveChangesAsync();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

try
{
    Log.Information("Starting web application");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
