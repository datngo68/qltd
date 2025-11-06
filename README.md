# Quản Lý Ăn Trưa (QLTD)

Hệ thống quản lý chi phí ăn trưa theo nhóm, hỗ trợ theo dõi chi tiêu, thanh toán và báo cáo tự động.

## 📋 Tính năng

- **Quản lý nhóm**: Tạo và quản lý các nhóm ăn trưa
- **Quản lý chi tiêu**: Ghi nhận và theo dõi các khoản chi tiêu trong nhóm
- **Quản lý thanh toán**: Theo dõi các khoản thanh toán giữa các thành viên
- **Báo cáo**: Xem báo cáo chi tiết về chi tiêu và thanh toán
- **Chia sẻ báo cáo**: Tạo link chia sẻ công khai cho báo cáo
- **Quản lý người dùng**: Phân quyền SuperAdmin, Admin, User
- **Xuất PDF**: Xuất báo cáo ra file PDF
- **QR Code**: Tạo QR code cho link chia sẻ

## 🛠️ Công nghệ sử dụng

- **.NET 8.0**: Framework chính
- **ASP.NET Core MVC**: Web framework
- **Entity Framework Core**: ORM
- **SQLite**: Database
- **BCrypt.Net**: Mã hóa mật khẩu
- **QRCoder**: Tạo QR code
- **QuestPDF**: Xuất PDF
- **ClosedXML**: Xử lý Excel
- **Bootstrap 5**: UI framework

## 📦 Cài đặt

### Yêu cầu

- .NET 8.0 SDK hoặc cao hơn
- Visual Studio 2022 hoặc VS Code (khuyến nghị)

### Các bước cài đặt

1. **Clone repository**
```bash
git clone https://github.com/datngo68/qltd.git
cd qltd
```

2. **Khôi phục packages**
```bash
dotnet restore
```

3. **Tạo file cấu hình**
Tạo file `appsettings.Development.json` trong thư mục `QuanLyAnTrua/` với nội dung:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=QuanLyAnTrua.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

4. **Chạy migrations**
```bash
cd QuanLyAnTrua
dotnet ef database update
```

5. **Chạy ứng dụng**
```bash
dotnet run
```

Ứng dụng sẽ chạy tại `https://localhost:5001` hoặc `http://localhost:5000`

## 👤 Tài khoản mặc định

Sau khi chạy migrations, hệ thống sẽ tự động tạo tài khoản SuperAdmin:

- **Username**: `ngotiendat`
- **Password**: `123456`

**⚠️ Lưu ý**: Vui lòng đổi mật khẩu ngay sau lần đăng nhập đầu tiên!

## 📁 Cấu trúc project

```
QuanLyAnTrua/
├── Controllers/          # Các controller xử lý request
├── Data/                 # DbContext và database configuration
├── Helpers/              # Các helper classes (Password, QRCode, Session, Token)
├── Migrations/           # Entity Framework migrations
├── Models/               # Data models và ViewModels
├── Views/                # Razor views
├── ViewComponents/       # View components
├── wwwroot/              # Static files (CSS, JS, images)
└── Program.cs            # Entry point và configuration
```

## 🔐 Bảo mật

- Mật khẩu được mã hóa bằng BCrypt
- Session-based authentication
- Phân quyền theo role (SuperAdmin, Admin, User)
- SQL injection protection với Entity Framework Core

## 📝 License

Dự án này được phát triển bởi Ngô Tiến Đạt.

## 👨‍💻 Tác giả

**Ngô Tiến Đạt**

- GitHub: [@datngo68](https://github.com/datngo68)

## 🤝 Đóng góp

Mọi đóng góp đều được chào đón! Vui lòng tạo Pull Request hoặc mở Issue để thảo luận.

## 📞 Liên hệ

Nếu có bất kỳ câu hỏi nào, vui lòng mở Issue trên GitHub.

