using System.ComponentModel.DataAnnotations;

namespace QuanLyAnTrua.Models.ViewModels
{
    public class SettingsViewModel
    {
        // ConnectionStrings
        [Display(Name = "Connection String")]
        public string DefaultConnection { get; set; } = string.Empty;

        // Casso
        [Display(Name = "Casso Webhook Secret")]
        public string CassoWebhookSecret { get; set; } = string.Empty;

        [Display(Name = "Casso Secure Token")]
        public string CassoSecureToken { get; set; } = string.Empty;

        [Display(Name = "Casso Webhook Version")]
        public string CassoWebhookVersion { get; set; } = string.Empty;

        // Payment
        [Display(Name = "Payment Description Prefix")]
        public string PaymentDescriptionPrefix { get; set; } = string.Empty;

        [Display(Name = "Payment Description Suffix")]
        public string PaymentDescriptionSuffix { get; set; } = string.Empty;

        [Display(Name = "Payment Description Separator")]
        public string PaymentDescriptionSeparator { get; set; } = string.Empty;

        // Telegram
        [Display(Name = "Telegram Bot Token")]
        public string? TelegramBotToken { get; set; }

        [Display(Name = "Telegram Bot Username")]
        public string TelegramBotUsername { get; set; } = string.Empty;

        // Parking Payment
        [Display(Name = "Default License Plate")]
        public string ParkingDefaultLicensePlate { get; set; } = string.Empty;

        [Display(Name = "Motorbike Price")]
        public decimal ParkingMotorbikePrice { get; set; }

        [Display(Name = "Car Price")]
        public decimal ParkingCarPrice { get; set; }

        [Display(Name = "Car Overnight Price")]
        public decimal ParkingCarOvernightPrice { get; set; }

        [Display(Name = "Company Bank Name")]
        public string ParkingCompanyBankName { get; set; } = string.Empty;

        [Display(Name = "Company Account Number")]
        public string ParkingCompanyAccountNumber { get; set; } = string.Empty;

        [Display(Name = "Company Account Holder")]
        public string ParkingCompanyAccountHolder { get; set; } = string.Empty;

        [Display(Name = "Staff Bank Name")]
        public string ParkingStaffBankName { get; set; } = string.Empty;

        [Display(Name = "Staff Account Number")]
        public string ParkingStaffAccountNumber { get; set; } = string.Empty;

        [Display(Name = "Staff Account Holder")]
        public string ParkingStaffAccountHolder { get; set; } = string.Empty;

        // Avatar
        [Display(Name = "Avatar Upload Path")]
        public string AvatarUploadPath { get; set; } = string.Empty;

        [Display(Name = "Avatar Max File Size (bytes)")]
        public int AvatarMaxFileSize { get; set; }

        [Display(Name = "Avatar Allowed Extensions")]
        public string AvatarAllowedExtensions { get; set; } = string.Empty;

        // Telegram Webhook Status
        public bool TelegramWebhookEnabled { get; set; }
        public string? TelegramWebhookUrl { get; set; }
        public string? TelegramWebhookError { get; set; }
    }
}

