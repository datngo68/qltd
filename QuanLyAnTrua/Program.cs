using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuanLyAnTrua.Data;
using QuanLyAnTrua.Helpers;
using QuanLyAnTrua.Models;

var builder = WebApplication.CreateBuilder(args);

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

app.Run();
