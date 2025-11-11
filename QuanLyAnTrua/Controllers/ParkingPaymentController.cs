using Microsoft.AspNetCore.Mvc;
using QuanLyAnTrua.Helpers;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace QuanLyAnTrua.Controllers
{
    [AllowAnonymous]
    public class ParkingPaymentController : Controller
    {
        private readonly IConfiguration _configuration;
        private static readonly Dictionary<string, List<DateTime>> _requestHistory = new Dictionary<string, List<DateTime>>();
        private static readonly object _lockObject = new object();

        // Giới hạn bảo mật
        private const int MAX_REQUESTS_PER_MINUTE = 10; // Tối đa 10 request/phút
        private const int MAX_MONTHS = 12; // Tối đa 12 tháng
        private const int MAX_LICENSE_PLATE_LENGTH = 20; // Độ dài tối đa biển số xe

        public ParkingPaymentController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Kiểm tra rate limiting
        private bool IsRateLimited()
        {
            var clientId = GetClientId();
            var now = DateTime.Now;

            lock (_lockObject)
            {
                // Xóa các request cũ hơn 1 phút
                if (_requestHistory.ContainsKey(clientId))
                {
                    _requestHistory[clientId].RemoveAll(dt => (now - dt).TotalMinutes > 1);
                }
                else
                {
                    _requestHistory[clientId] = new List<DateTime>();
                }

                // Kiểm tra số lượng request
                var requestCount = _requestHistory[clientId].Count;
                if (requestCount >= MAX_REQUESTS_PER_MINUTE)
                {
                    return true;
                }

                // Thêm request hiện tại
                _requestHistory[clientId].Add(now);
                return false;
            }
        }

        // Lấy client ID (IP address hoặc session ID)
        private string GetClientId()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var sessionId = HttpContext.Session.Id ?? "nosession";
            return $"{ipAddress}_{sessionId}";
        }

        // Validate và sanitize biển số xe
        private string SanitizeLicensePlate(string licensePlate)
        {
            if (string.IsNullOrWhiteSpace(licensePlate))
                return string.Empty;

            // Chỉ cho phép chữ cái, số và dấu gạch ngang
            var sanitized = Regex.Replace(licensePlate.Trim().ToUpper(), @"[^A-Z0-9\-]", "");

            // Giới hạn độ dài
            if (sanitized.Length > MAX_LICENSE_PLATE_LENGTH)
            {
                sanitized = sanitized.Substring(0, MAX_LICENSE_PLATE_LENGTH);
            }

            return sanitized;
        }

        // Lấy giá trị từ configuration với giá trị mặc định
        private string DefaultLicensePlate => _configuration["ParkingPayment:DefaultLicensePlate"] ?? "15D1-41770";

        private decimal PriceMotorbike => decimal.Parse(_configuration["ParkingPayment:Prices:Motorbike"] ?? "70000");
        private decimal PriceCar => decimal.Parse(_configuration["ParkingPayment:Prices:Car"] ?? "700000");
        private decimal PriceCarOvernight => decimal.Parse(_configuration["ParkingPayment:Prices:CarOvernight"] ?? "900000");

        private string CompanyBankName => _configuration["ParkingPayment:CompanyAccount:BankName"] ?? "BIDV";
        private string CompanyAccountNumber => _configuration["ParkingPayment:CompanyAccount:AccountNumber"] ?? "8601276666";
        private string CompanyAccountHolder => _configuration["ParkingPayment:CompanyAccount:AccountHolder"] ?? "Công ty TNHH MTV Dịch vụ Xây dựng Thịnh Thái";

        private string StaffBankName => _configuration["ParkingPayment:StaffAccount:BankName"] ?? "MBBank";
        private string StaffAccountNumber => _configuration["ParkingPayment:StaffAccount:AccountNumber"] ?? "0936.187.187";
        private string StaffAccountHolder => _configuration["ParkingPayment:StaffAccount:AccountHolder"] ?? "Nguyễn Thanh Thảo";

        public IActionResult Index()
        {
            ViewBag.CurrentMonth = DateTime.Now.Month;
            ViewBag.CurrentYear = DateTime.Now.Year;
            ViewBag.DefaultLicensePlate = DefaultLicensePlate;
            ViewBag.PriceMotorbike = PriceMotorbike;
            ViewBag.PriceCar = PriceCar;
            ViewBag.PriceCarOvernight = PriceCarOvernight;

            // Thông tin tài khoản công ty
            ViewBag.CompanyBankName = CompanyBankName;
            ViewBag.CompanyAccountNumber = CompanyAccountNumber;
            ViewBag.CompanyAccountHolder = CompanyAccountHolder;

            // Thông tin tài khoản nhân viên
            ViewBag.StaffBankName = StaffBankName;
            ViewBag.StaffAccountNumber = StaffAccountNumber;
            ViewBag.StaffAccountHolder = StaffAccountHolder;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GenerateQR(string licensePlate, string vehicleType, int fromMonth, int fromYear,
            int toMonth, int toYear, string accountType, decimal? customAmount = null)
        {
            // Rate limiting
            if (IsRateLimited())
            {
                TempData["ErrorMessage"] = "Bạn đã gửi quá nhiều yêu cầu. Vui lòng đợi một chút và thử lại.";
                return RedirectToAction("Index");
            }

            // Sanitize và validate biển số xe
            licensePlate = SanitizeLicensePlate(licensePlate);
            if (string.IsNullOrWhiteSpace(licensePlate))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập biển số xe hợp lệ";
                return RedirectToAction("Index");
            }

            // Validate loại xe
            var validVehicleTypes = new[] { "motorbike", "car", "car_overnight" };
            if (string.IsNullOrWhiteSpace(vehicleType) || !validVehicleTypes.Contains(vehicleType))
            {
                TempData["ErrorMessage"] = "Loại xe không hợp lệ";
                return RedirectToAction("Index");
            }

            // Validate account type
            if (string.IsNullOrWhiteSpace(accountType) || (accountType != "company" && accountType != "staff"))
            {
                TempData["ErrorMessage"] = "Loại tài khoản không hợp lệ";
                return RedirectToAction("Index");
            }

            // Validate tháng/năm
            if (fromMonth < 1 || fromMonth > 12 || toMonth < 1 || toMonth > 12)
            {
                TempData["ErrorMessage"] = "Tháng không hợp lệ";
                return RedirectToAction("Index");
            }

            if (fromYear < 2020 || fromYear > 2100 || toYear < 2020 || toYear > 2100)
            {
                TempData["ErrorMessage"] = "Năm không hợp lệ";
                return RedirectToAction("Index");
            }

            // Validate từ tháng đến tháng
            var fromDate = new DateTime(fromYear, fromMonth, 1);
            var toDate = new DateTime(toYear, toMonth, 1);
            if (toDate < fromDate)
            {
                TempData["ErrorMessage"] = "Tháng kết thúc phải sau hoặc bằng tháng bắt đầu";
                return RedirectToAction("Index");
            }

            // Tính số tháng
            int totalMonths = ((toYear - fromYear) * 12) + (toMonth - fromMonth) + 1;
            if (totalMonths <= 0)
            {
                TempData["ErrorMessage"] = "Số tháng không hợp lệ";
                return RedirectToAction("Index");
            }

            // Giới hạn số tháng tối đa
            if (totalMonths > MAX_MONTHS)
            {
                TempData["ErrorMessage"] = $"Chỉ có thể thanh toán tối đa {MAX_MONTHS} tháng một lần";
                return RedirectToAction("Index");
            }

            // Xác định giá theo loại xe
            decimal pricePerMonth = 0;
            string vehicleTypeName = "";
            if (vehicleType == "car_overnight")
            {
                pricePerMonth = PriceCarOvernight;
                vehicleTypeName = "ô tô qua đêm";
            }
            else if (vehicleType == "car")
            {
                pricePerMonth = PriceCar;
                vehicleTypeName = "ô tô";
            }
            else // motorbike (mặc định)
            {
                pricePerMonth = PriceMotorbike;
                vehicleTypeName = "xe máy";
            }

            // Tính tổng tiền
            decimal totalAmount = customAmount ?? (pricePerMonth * totalMonths);

            // Validate số tiền tùy chỉnh (nếu có)
            if (customAmount.HasValue)
            {
                if (customAmount.Value < 0 || customAmount.Value > 100000000) // Tối đa 100 triệu
                {
                    TempData["ErrorMessage"] = "Số tiền không hợp lệ";
                    return RedirectToAction("Index");
                }
            }

            // Chọn tài khoản
            string bankName, accountNumber, accountHolder;
            if (accountType == "staff")
            {
                bankName = StaffBankName;
                accountNumber = StaffAccountNumber;
                accountHolder = StaffAccountHolder;
            }
            else
            {
                bankName = CompanyBankName;
                accountNumber = CompanyAccountNumber;
                accountHolder = CompanyAccountHolder;
            }

            // Verify BIN mapping (để đảm bảo QR code được tạo đúng)
            // BIDV -> BIN: 970418, MBBank -> BIN: 970422
            var bankBin = QRCodeHelper.GetBankBin(bankName);

            // Kiểm tra BIN có đúng không
            bool isValidBin = false;
            if (accountType == "staff")
            {
                // MBBank phải có BIN 970422
                isValidBin = bankBin == "970422";
            }
            else
            {
                // BIDV phải có BIN 970418
                isValidBin = bankBin == "970418";
            }

            if (!isValidBin)
            {
                // Nếu BIN không đúng, có thể log error hoặc throw exception
                TempData["ErrorMessage"] = $"Lỗi: Không thể map BIN cho ngân hàng {bankName}. BIN hiện tại: {bankBin}";
                return RedirectToAction("Index");
            }

            // Tạo nội dung chuyển khoản
            string description;
            if (totalMonths == 1)
            {
                description = $"{licensePlate.Trim().ToUpper()} thanh toán phí xe tháng {fromMonth:D2}/{fromYear}";
            }
            else
            {
                description = $"{licensePlate.Trim().ToUpper()} thanh toán phí xe tháng {fromMonth:D2}/{fromYear} đến {toMonth:D2}/{toYear}";
            }

            // Tạo QR code (QRCodeHelper sẽ tự động map BIN từ tên ngân hàng)
            var qrBase64 = QRCodeHelper.GeneratePaymentQRCodeBase64(
                bankName,
                accountNumber,
                accountHolder,
                totalAmount,
                description
            );

            ViewBag.QRCodeBase64 = qrBase64;
            ViewBag.LicensePlate = licensePlate.Trim().ToUpper();
            ViewBag.VehicleType = vehicleType;
            ViewBag.VehicleTypeName = vehicleTypeName;
            ViewBag.FromMonth = fromMonth;
            ViewBag.FromYear = fromYear;
            ViewBag.ToMonth = toMonth;
            ViewBag.ToYear = toYear;
            ViewBag.TotalMonths = totalMonths;
            ViewBag.PricePerMonth = pricePerMonth;
            ViewBag.TotalAmount = totalAmount;
            ViewBag.AccountType = accountType;
            ViewBag.BankName = bankName;
            ViewBag.AccountNumber = accountNumber;
            ViewBag.AccountHolder = accountHolder;
            ViewBag.Description = description;

            return View("Result");
        }
    }
}

