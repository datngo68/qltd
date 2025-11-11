# Hướng Dẫn Phát Triển - Quản Lý Ăn Trưa (QLTD)
## Phản hồi bằng tiếng Việt xưng hô bạn tôi
File này chứa các hướng dẫn và quy ước quan trọng để phát triển hiệu quả trên project Quản Lý Ăn Trưa.

## 📋 Tổng Quan Project

**Quản Lý Ăn Trưa** là một hệ thống web ASP.NET Core MVC để quản lý chi phí ăn trưa theo nhóm, hỗ trợ theo dõi chi tiêu, thanh toán và báo cáo tự động.

### Công Nghệ Chính
- **.NET 8.0** - Framework chính
- **ASP.NET Core MVC** - Web framework
- **Entity Framework Core 9.0** - ORM
- **SQLite** - Database
- **BCrypt.Net** - Mã hóa mật khẩu
- **Serilog** - Logging
- **QRCoder** - Tạo QR code
- **QuestPDF** - Xuất PDF
- **ClosedXML** - Xử lý Excel

## 📁 Cấu Trúc Thư Mục

```
QuanLyAnTrua/
├── Controllers/          # Các controller xử lý HTTP requests
│   ├── AccountController.cs      # Authentication & user profile
│   ├── ExpensesController.cs      # Quản lý chi tiêu
│   ├── PaymentsController.cs     # Quản lý thanh toán
│   ├── GroupsController.cs       # Quản lý nhóm
│   ├── ReportsController.cs      # Báo cáo
│   └── ...
├── Data/
│   └── ApplicationDbContext.cs   # DbContext và database configuration
├── Helpers/              # Các helper classes tái sử dụng
│   ├── AuthorizeAttribute.cs     # Custom authorization
│   ├── AllowAnonymousAttribute.cs
│   ├── PasswordHelper.cs          # BCrypt password hashing
│   ├── SessionHelper.cs           # Session management utilities
│   ├── TokenHelper.cs             # Token generation
│   ├── QRCodeHelper.cs            # QR code generation
│   ├── IdEncoderHelper.cs         # ID encoding/decoding
│   └── CassoWebhookHelper.cs      # Casso webhook utilities
├── Models/               # Data models và ViewModels
│   ├── User.cs
│   ├── Group.cs
│   ├── Expense.cs
│   ├── ExpenseParticipant.cs
│   ├── MonthlyPayment.cs
│   ├── SharedReport.cs
│   └── ViewModels/       # ViewModels cho các view phức tạp
├── Migrations/           # Entity Framework migrations
├── Views/                # Razor views
│   ├── Account/
│   ├── Expenses/
│   ├── Payments/
│   ├── Groups/
│   ├── Reports/
│   └── Shared/
├── ViewComponents/       # View components (reusable UI components)
├── wwwroot/              # Static files (CSS, JS, images, libs)
└── Program.cs            # Entry point và application configuration
```

## 🔐 Authentication & Authorization

### Session-Based Authentication
- Hệ thống sử dụng **Session** để quản lý authentication (không dùng JWT hay Identity)
- Session timeout: **30 ngày**
- Session keys:
  - `UserId` (int?)
  - `Username` (string)
  - `FullName` (string)
  - `Role` (string)
  - `GroupId` (int?)

### Custom Authorization
- Sử dụng custom `[Authorize]` attribute từ `QuanLyAnTrua.Helpers`
- Sử dụng `[AllowAnonymous]` để bypass authorization cho các action cụ thể
- **KHÔNG** sử dụng `[Microsoft.AspNetCore.Authorization.Authorize]`

```csharp
using QuanLyAnTrua.Helpers;

[Authorize]  // Bắt buộc đăng nhập
public class ExpensesController : Controller
{
    [AllowAnonymous]  // Cho phép truy cập không cần đăng nhập
    public IActionResult PublicView() { }
}
```

### Phân Quyền (Roles)
- **SuperAdmin**: Toàn quyền, không thuộc group nào, có thể xem tất cả groups
- **Admin**: Quản lý group của mình, có thể tạo/sửa/xóa users trong group
- **User**: Chỉ xem và thao tác với dữ liệu của group mình

### SessionHelper Utilities
Luôn sử dụng `SessionHelper` để kiểm tra user và role:

```csharp
using QuanLyAnTrua.Helpers;

var userId = SessionHelper.GetUserId(HttpContext);
var role = SessionHelper.GetRole(HttpContext);
var groupId = SessionHelper.GetGroupId(HttpContext);
var isSuperAdmin = SessionHelper.IsSuperAdmin(HttpContext);
var isAdmin = SessionHelper.IsAdmin(HttpContext);
```

## 💾 Database & Entity Framework

### DbContext
- Sử dụng `ApplicationDbContext` từ `QuanLyAnTrua.Data`
- Inject vào controller qua constructor:

```csharp
private readonly ApplicationDbContext _context;

public ExpensesController(ApplicationDbContext context)
{
    _context = context;
}
```

### Migrations
- Luôn tạo migration khi thay đổi model:
  ```bash
  dotnet ef migrations add MigrationName --project QuanLyAnTrua
  ```
- Apply migrations tự động trong `Program.cs` khi app start
- **KHÔNG** chạy `dotnet ef database update` thủ công trong production

### Relationships
- Sử dụng `Include()` và `ThenInclude()` để eager load navigation properties
- Luôn kiểm tra null khi truy cập navigation properties
- Foreign keys sử dụng `DeleteBehavior.Restrict` để tránh cascade delete không mong muốn

```csharp
var expenses = await _context.Expenses
    .Include(e => e.Payer)
    .Include(e => e.Participants)
        .ThenInclude(ep => ep.User)
    .Where(e => e.GroupId == groupId)
    .ToListAsync();
```

## 🎯 Quy Ước Coding

### Naming Conventions
- **Controllers**: `[Entity]Controller.cs` (ví dụ: `ExpensesController.cs`)
- **Models**: PascalCase, singular (ví dụ: `User`, `Expense`)
- **ViewModels**: Đặt trong `Models/ViewModels/`, kết thúc bằng `ViewModel` (ví dụ: `ExpenseViewModel`)
- **Helpers**: Static classes hoặc extension methods, kết thúc bằng `Helper` (ví dụ: `PasswordHelper`)
- **Views**: Tên view khớp với action name

### Controller Patterns
1. **Luôn** inject `ApplicationDbContext` qua constructor
2. **Luôn** kiểm tra authorization và group access trước khi truy cập dữ liệu
3. **Luôn** filter theo `GroupId` trừ khi là SuperAdmin
4. Sử dụng `async/await` cho tất cả database operations
5. Sử dụng `TempData` để hiển thị success/error messages

```csharp
[Authorize]
public class ExpensesController : Controller
{
    private readonly ApplicationDbContext _context;

    public ExpensesController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var query = _context.Expenses.AsQueryable();
        
        // Filter theo group (trừ SuperAdmin)
        if (!SessionHelper.IsSuperAdmin(HttpContext))
        {
            var groupId = SessionHelper.GetGroupId(HttpContext);
            if (groupId.HasValue)
            {
                query = query.Where(e => e.GroupId == groupId.Value);
            }
        }
        
        var expenses = await query.ToListAsync();
        return View(expenses);
    }
}
```

### Model Patterns
- Sử dụng Data Annotations cho validation và display names
- Navigation properties phải là `virtual` để hỗ trợ lazy loading (nếu cần)
- Sử dụng `[Display(Name = "...")]` cho tất cả properties hiển thị trong views

```csharp
public class Expense
{
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Tên chi tiêu là bắt buộc")]
    [Display(Name = "Tên chi tiêu")]
    public string Name { get; set; } = string.Empty;
    
    [Display(Name = "Người chi")]
    public int PayerId { get; set; }
    
    [ForeignKey("PayerId")]
    public virtual User? Payer { get; set; }
}
```

### View Patterns
- Sử dụng Razor syntax (`@model`, `@Html.DisplayFor`, etc.)
- Sử dụng Bootstrap 5 cho UI
- Sử dụng `TempData` để hiển thị messages:
  ```csharp
  @if (TempData["SuccessMessage"] != null)
  {
      <div class="alert alert-success">@TempData["SuccessMessage"]</div>
  }
  ```

## 🔧 Helper Classes

### PasswordHelper
- Sử dụng BCrypt để hash/verify passwords
- **KHÔNG BAO GIỜ** lưu plain text password

```csharp
using QuanLyAnTrua.Helpers;

// Hash password
var hashedPassword = PasswordHelper.HashPassword("plainPassword");

// Verify password
if (PasswordHelper.VerifyPassword(inputPassword, storedHash))
{
    // Login success
}
```

### SessionHelper
- Luôn sử dụng `SessionHelper` thay vì truy cập trực tiếp `HttpContext.Session`
- Cung cấp các methods tiện ích: `GetUserId()`, `GetRole()`, `GetGroupId()`, `IsSuperAdmin()`, `IsAdmin()`

### IdEncoderHelper
- Sử dụng để encode/decode IDs khi cần ẩn ID thực trong URL
- Được khởi tạo trong `Program.cs` với prefix từ configuration

### QRCodeHelper
- Tạo QR code cho các link chia sẻ
- Hỗ trợ các ngân hàng Việt Nam phổ biến

## 📝 Logging

- Sử dụng **Serilog** cho logging
- Log files được lưu trong thư mục `logs/` với rolling interval theo ngày
- Sử dụng `Log.Information()`, `Log.Warning()`, `Log.Error()` trong code
- Log level được cấu hình trong `appsettings.json`

```csharp
using Serilog;

Log.Information("User {UserId} created expense {ExpenseId}", userId, expenseId);
Log.Error(ex, "Error creating expense");
```

## 🔄 Multi-Tenant Pattern

- Mỗi user thuộc một `Group` (trừ SuperAdmin)
- SuperAdmin có `GroupId = null`
- **Luôn** filter dữ liệu theo `GroupId` trừ khi là SuperAdmin
- SuperAdmin có thể xem và quản lý tất cả groups

```csharp
// Pattern kiểm tra group access
if (SessionHelper.IsSuperAdmin(HttpContext))
{
    // SuperAdmin: có thể xem tất cả hoặc filter theo groupId
    if (groupId.HasValue)
    {
        query = query.Where(e => e.GroupId == groupId.Value);
    }
}
else
{
    // User/Admin: chỉ xem dữ liệu của group mình
    var currentGroupId = SessionHelper.GetGroupId(HttpContext);
    if (currentGroupId.HasValue)
    {
        query = query.Where(e => e.GroupId == currentGroupId.Value);
    }
    else
    {
        // User không có group, không thấy gì
        query = query.Where(e => false);
    }
}
```

## ⚠️ Các Điểm Quan Trọng

### 1. Security
- **KHÔNG BAO GIỜ** expose plain text passwords
- **Luôn** validate input từ user
- **Luôn** kiểm tra authorization trước khi truy cập dữ liệu
- **Luôn** kiểm tra user thuộc group nào trước khi hiển thị/sửa dữ liệu
- Sử dụng `[ValidateAntiForgeryToken]` cho tất cả POST actions

### 2. Error Handling
- Sử dụng try-catch cho database operations
- Log errors với Serilog
- Hiển thị user-friendly error messages (không expose technical details)

### 3. Performance
- Sử dụng `AsQueryable()` để build queries động
- Sử dụng `Include()` và `ThenInclude()` để eager load thay vì N+1 queries
- Sử dụng `async/await` cho tất cả I/O operations

### 4. Configuration
- Cấu hình trong `appsettings.json` và `appsettings.Development.json`
- Không hardcode connection strings, API keys, etc.
- Sử dụng `IConfiguration` để đọc configuration

### 5. Migrations
- **KHÔNG** xóa migrations đã apply vào production
- **Luôn** test migrations trên development trước
- Migrations được apply tự động trong `Program.cs`

## 🚀 Best Practices

1. **Separation of Concerns**: Controllers chỉ xử lý HTTP, business logic nên đặt trong Services (nếu có) hoặc Helpers
2. **DRY Principle**: Tái sử dụng code qua Helpers và ViewComponents
3. **Consistent Error Messages**: Sử dụng tiếng Việt cho tất cả messages hiển thị cho user
4. **Code Comments**: Comment bằng tiếng Việt cho các logic phức tạp
5. **Async All The Way**: Sử dụng async/await từ controller đến database

## 📚 Tài Liệu Tham Khảo

- [ASP.NET Core MVC Documentation](https://docs.microsoft.com/aspnet/core/mvc)
- [Entity Framework Core Documentation](https://docs.microsoft.com/ef/core)
- [Serilog Documentation](https://serilog.net/)

## 🎓 Ví Dụ Hoàn Chỉnh

### Tạo Controller Mới

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyAnTrua.Data;
using QuanLyAnTrua.Helpers;
using QuanLyAnTrua.Models;

namespace QuanLyAnTrua.Controllers
{
    [Authorize]
    public class MyController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MyController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = SessionHelper.GetUserId(HttpContext);
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var query = _context.MyEntities.AsQueryable();

            // Filter theo group
            if (!SessionHelper.IsSuperAdmin(HttpContext))
            {
                var groupId = SessionHelper.GetGroupId(HttpContext);
                if (groupId.HasValue)
                {
                    query = query.Where(e => e.GroupId == groupId.Value);
                }
                else
                {
                    query = query.Where(e => false);
                }
            }

            var entities = await query.ToListAsync();
            return View(entities);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MyEntity entity)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var userId = SessionHelper.GetUserId(HttpContext);
                    var groupId = SessionHelper.GetGroupId(HttpContext);
                    
                    entity.CreatedBy = userId;
                    entity.GroupId = groupId;
                    entity.CreatedAt = DateTime.Now;

                    _context.Add(entity);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Tạo thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error creating entity");
                    ModelState.AddModelError("", "Có lỗi xảy ra khi tạo mới");
                }
            }

            return View(entity);
        }
    }
}
```

---

**Lưu ý**: File này sẽ được cập nhật khi có thay đổi về architecture hoặc best practices. Luôn tham khảo file này trước khi code!
