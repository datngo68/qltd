using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace QuanLyAnTrua.Helpers
{
    public static class QRCodeHelper
    {
        // Danh sách ngân hàng có hỗ trợ QR code
        public static List<BankInfo> GetSupportedBanks()
        {
            return new List<BankInfo>
            {
                new BankInfo { Name = "Vietcombank", ShortName = "VCB", BIN = "970436" },
                new BankInfo { Name = "Vietinbank", ShortName = "Vietin", BIN = "970415" },
                new BankInfo { Name = "BIDV", ShortName = "BIDV", BIN = "970418" },
                new BankInfo { Name = "Agribank", ShortName = "Agri", BIN = "970405" },
                new BankInfo { Name = "Techcombank", ShortName = "TCB", BIN = "970407" },
                new BankInfo { Name = "MBBank", ShortName = "MB", BIN = "970422" },
                new BankInfo { Name = "ACB", ShortName = "ACB", BIN = "970416" },
                new BankInfo { Name = "TPBank", ShortName = "TPBank", BIN = "970423" },
                new BankInfo { Name = "VIB", ShortName = "VIB", BIN = "970441" },
                new BankInfo { Name = "VPBank", ShortName = "VPBank", BIN = "970432" },
                new BankInfo { Name = "SHB", ShortName = "SHB", BIN = "970443" },
                new BankInfo { Name = "HDBank", ShortName = "HDBank", BIN = "970437" },
                new BankInfo { Name = "Sacombank", ShortName = "STB", BIN = "970403" },
                new BankInfo { Name = "Eximbank", ShortName = "Eximbank", BIN = "970431" },
                new BankInfo { Name = "MSB", ShortName = "MSB", BIN = "970426" },
                new BankInfo { Name = "OceanBank", ShortName = "OceanBank", BIN = "970414" },
                new BankInfo { Name = "PVcombank", ShortName = "PVBank", BIN = "970412" },
                new BankInfo { Name = "Nam A Bank", ShortName = "NAB", BIN = "970428" },
                new BankInfo { Name = "VID Public", ShortName = "VID", BIN = "970439" },
                new BankInfo { Name = "Bắc Á Bank", ShortName = "NASBank", BIN = "970409" },
                new BankInfo { Name = "SCB", ShortName = "SCB", BIN = "970429" },
                new BankInfo { Name = "Đông Á Bank", ShortName = "DAB", BIN = "970406" },
                new BankInfo { Name = "GPBank", ShortName = "GPBank", BIN = "970408" },
                new BankInfo { Name = "Hong Leong Bank", ShortName = "HLO", BIN = "970442" },
                new BankInfo { Name = "Indovina Bank", ShortName = "IVB", BIN = "970434" },
                new BankInfo { Name = "Kiên Long Bank", ShortName = "Kienlongbank", BIN = "970452" },
                new BankInfo { Name = "Liên Việt Post Bank", ShortName = "LPB", BIN = "970449" },
                new BankInfo { Name = "NCB", ShortName = "NCB", BIN = "970419" },
                new BankInfo { Name = "OCB", ShortName = "OCB", BIN = "970448" },
                new BankInfo { Name = "PG Bank", ShortName = "PG Bank", BIN = "970430" },
                new BankInfo { Name = "SeABank", ShortName = "SeABank", BIN = "970440" },
                new BankInfo { Name = "Shinhan Bank", ShortName = "Shinhan", BIN = "970424" },
                new BankInfo { Name = "UOB", ShortName = "UOB", BIN = "970458" },
                new BankInfo { Name = "Vietbank", ShortName = "Vietbank", BIN = "970433" },
                new BankInfo { Name = "VietA Bank", ShortName = "VAB", BIN = "970427" },
                new BankInfo { Name = "VietCapital Bank", ShortName = "VietCapital", BIN = "970454" },
                new BankInfo { Name = "VRB", ShortName = "VRB", BIN = "970421" },
                new BankInfo { Name = "Woori Bank", ShortName = "Woori", BIN = "970457" },
                new BankInfo { Name = "ABBank", ShortName = "ABBank", BIN = "970425" },
                new BankInfo { Name = "Baoviet Bank", ShortName = "Baoviet", BIN = "970438" },
                new BankInfo { Name = "CIMB Bank", ShortName = "CIMB", BIN = "422589" }
            };
        }
        // ===== Helpers =====
        private static string RemoveDiacritics(string? input, int? maxLen = null)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            var normalized = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var c in normalized)
            {
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            // Loại ký tự không nằm trong ASCII in được theo EMV (A–Z, 0–9, space & ký tự cơ bản)
            var ascii = Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), @"[^\u0020-\u007E]", "");
            ascii = ascii.Trim();
            if (maxLen.HasValue && ascii.Length > maxLen.Value)
                ascii = ascii.Substring(0, maxLen.Value);
            return ascii;
        }

        private static string ReturnNumber(int total)
        {
            return total < 10 ? $"0{total}" : total.ToString();
        }

        // ===== Bank BIN map từ danh sách ngân hàng =====
        public static string GetBankBin(string bankName)
        {
            if (string.IsNullOrWhiteSpace(bankName)) return "970436"; // VCB default

            var b = bankName.ToLowerInvariant();

            // Mapping từ danh sách ngân hàng
            if (b.Contains("vietcombank") || b.Contains("vcb") || b.Contains("ngoại thương")) return "970436";
            if (b.Contains("vietin") || b.Contains("công thương")) return "970415";
            if (b.Contains("bidv") || b.Contains("đầu tư") || b.Contains("phát triển")) return "970418";
            if (b.Contains("agribank") || b.Contains("agri") || b.Contains("nông nghiệp")) return "970405";
            if (b.Contains("techcombank") || b.Contains("tcb") || b.Contains("kỹ thương")) return "970407";
            if (b.Contains("mbbank") || b.Contains("mb") || b.Contains("quân đội") || b.Contains("military")) return "970422";
            if (b.Contains("acb") || b.Contains("á châu")) return "970416";
            if (b.Contains("tpbank")) return "970423";
            if (b.Contains("vib") || b.Contains("quốc tế")) return "970441";
            if (b.Contains("vpbank") || b.Contains("vp") || b.Contains("thịnh vượng")) return "970432";
            if (b.Contains("shb") || b.Contains("sài gòn hà nội")) return "970443";
            if (b.Contains("hdbank") || b.Contains("hd") || b.Contains("phát triển")) return "970437";
            if (b.Contains("sacombank") || b.Contains("stb") || b.Contains("sài gòn thương tín")) return "970403";
            if (b.Contains("eximbank") || b.Contains("exim") || b.Contains("xuất nhập khẩu")) return "970431";
            if (b.Contains("msb") || b.Contains("hàng hải") || b.Contains("maritime")) return "970426";
            if (b.Contains("oceanbank") || b.Contains("đại dương")) return "970414";
            if (b.Contains("pvcombank") || b.Contains("pvbank") || b.Contains("pv") || b.Contains("đại chúng")) return "970412";
            if (b.Contains("namabank") || b.Contains("nam a") || b.Contains("nam á")) return "970428";
            if (b.Contains("vid") || b.Contains("public")) return "970439";
            if (b.Contains("bacabank") || b.Contains("baca") || b.Contains("bắc á") || b.Contains("nasbank")) return "970409";
            if (b.Contains("scb") || b.Contains("sài gòn") || b.Contains("sai gon")) return "970429";
            if (b.Contains("dong a") || b.Contains("đông á")) return "970406";
            if (b.Contains("gpbank") || b.Contains("dầu khí")) return "970408";
            if (b.Contains("hong leong") || b.Contains("hlo")) return "970442";
            if (b.Contains("indovina") || b.Contains("ivb")) return "970434";
            if (b.Contains("kienlong") || b.Contains("kiên long")) return "970452";
            if (b.Contains("lienviet") || b.Contains("liên việt") || b.Contains("bưu điện")) return "970449";
            if (b.Contains("nam a") || b.Contains("nam á")) return "970428";
            if (b.Contains("ncb") || b.Contains("quốc dân")) return "970419";
            if (b.Contains("ocb") || b.Contains("phương đông") || b.Contains("orient")) return "970448";
            if (b.Contains("pgbank") || b.Contains("petrolimex")) return "970430";
            if (b.Contains("seabank") || b.Contains("đông nam á")) return "970440";
            if (b.Contains("shinhan")) return "970424";
            if (b.Contains("uob")) return "970458";
            if (b.Contains("vietbank") || b.Contains("việt bank") || b.Contains("thương tín")) return "970433";
            if (b.Contains("vietabank") || b.Contains("việt á")) return "970427";
            if (b.Contains("vietcapital") || b.Contains("bản việt")) return "970454";
            if (b.Contains("vrb") || b.Contains("việt nga") || b.Contains("liên doanh")) return "970421";
            if (b.Contains("woori") || b.Contains("whhn")) return "970457";
            if (b.Contains("abbank") || b.Contains("an bình")) return "970425";
            if (b.Contains("baoviet")) return "970438";
            if (b.Contains("cimb")) return "422589";
            if (b.Contains("vietcombank") || b.Contains("vcb")) return "970436"; // Để chắc chắn

            return "970436"; // default VCB
        }

        // ===== Map tên ngân hàng sang ShortName để match với appId từ VietQR API =====
        public static string GetBankShortName(string bankName)
        {
            if (string.IsNullOrWhiteSpace(bankName)) return "VCB"; // VCB default

            var banks = GetSupportedBanks();
            var b = bankName.ToLowerInvariant();

            // Tìm ngân hàng khớp với tên
            var matchedBank = banks.FirstOrDefault(bank =>
                b.Contains(bank.Name.ToLowerInvariant()) ||
                b.Contains(bank.ShortName.ToLowerInvariant())
            );

            if (matchedBank != null)
            {
                return matchedBank.ShortName;
            }

            // Fallback: mapping thủ công nếu không tìm thấy
            if (b.Contains("vietcombank") || b.Contains("vcb") || b.Contains("ngoại thương")) return "VCB";
            if (b.Contains("vietin") || b.Contains("công thương")) return "Vietin";
            if (b.Contains("bidv") || b.Contains("đầu tư") || b.Contains("phát triển")) return "BIDV";
            if (b.Contains("agribank") || b.Contains("agri") || b.Contains("nông nghiệp")) return "Agri";
            if (b.Contains("techcombank") || b.Contains("tcb") || b.Contains("kỹ thương")) return "TCB";
            if (b.Contains("mbbank") || b.Contains("mb") || b.Contains("quân đội") || b.Contains("military")) return "MB";
            if (b.Contains("acb") || b.Contains("á châu")) return "ACB";
            if (b.Contains("tpbank")) return "TPBank";
            if (b.Contains("vib") || b.Contains("quốc tế")) return "VIB";
            if (b.Contains("vpbank") || b.Contains("vp") || b.Contains("thịnh vượng")) return "VPBank";
            if (b.Contains("shb") || b.Contains("sài gòn hà nội")) return "SHB";
            if (b.Contains("hdbank") || b.Contains("hd")) return "HDBank";
            if (b.Contains("sacombank") || b.Contains("stb") || b.Contains("sài gòn thương tín")) return "STB";
            if (b.Contains("eximbank") || b.Contains("exim") || b.Contains("xuất nhập khẩu")) return "Eximbank";
            if (b.Contains("msb") || b.Contains("hàng hải") || b.Contains("maritime")) return "MSB";
            if (b.Contains("oceanbank") || b.Contains("đại dương")) return "OceanBank";
            if (b.Contains("pvcombank") || b.Contains("pvbank") || b.Contains("pv") || b.Contains("đại chúng")) return "PVBank";
            if (b.Contains("namabank") || b.Contains("nam a") || b.Contains("nam á")) return "NAB";
            if (b.Contains("vid") || b.Contains("public")) return "VID";
            if (b.Contains("bacabank") || b.Contains("baca") || b.Contains("bắc á") || b.Contains("nasbank")) return "NASBank";
            if (b.Contains("scb") || b.Contains("sài gòn") || b.Contains("sai gon")) return "SCB";
            if (b.Contains("dong a") || b.Contains("đông á")) return "DAB";
            if (b.Contains("gpbank") || b.Contains("dầu khí")) return "GPBank";
            if (b.Contains("hong leong") || b.Contains("hlo")) return "HLO";
            if (b.Contains("indovina") || b.Contains("ivb")) return "IVB";
            if (b.Contains("kienlong") || b.Contains("kiên long")) return "Kienlongbank";
            if (b.Contains("lienviet") || b.Contains("liên việt") || b.Contains("bưu điện")) return "LPB";
            if (b.Contains("ncb") || b.Contains("quốc dân")) return "NCB";
            if (b.Contains("ocb") || b.Contains("phương đông") || b.Contains("orient")) return "OCB";
            if (b.Contains("pgbank") || b.Contains("petrolimex")) return "PG Bank";
            if (b.Contains("seabank") || b.Contains("đông nam á")) return "SeABank";
            if (b.Contains("shinhan")) return "Shinhan";
            if (b.Contains("uob")) return "UOB";
            if (b.Contains("vietbank") || b.Contains("việt bank") || b.Contains("thương tín")) return "Vietbank";
            if (b.Contains("vietabank") || b.Contains("việt á")) return "VAB";
            if (b.Contains("vietcapital") || b.Contains("bản việt")) return "VietCapital";
            if (b.Contains("vrb") || b.Contains("việt nga") || b.Contains("liên doanh")) return "VRB";
            if (b.Contains("woori") || b.Contains("whhn")) return "Woori";
            if (b.Contains("abbank") || b.Contains("an bình")) return "ABBank";
            if (b.Contains("baoviet")) return "Baoviet";
            if (b.Contains("cimb")) return "CIMB";

            return "VCB"; // default VCB
        }

        // ===== EMV QR (VietQR) builder theo format mẫu =====
        private static string GenerateEMVQRCode(string bankBin, string bankAccount, string accountHolderName, decimal amount, string? description = null)
        {
            string qr = "000201010212";

            // Tag 38: Merchant Account Information
            string prefix_38 = "38";
            string prefix_3800 = "00";
            string prefix_3800_content = "A000000727"; // GUID theo format mẫu
            prefix_3800 = $"{prefix_3800}{ReturnNumber(prefix_3800_content.Length)}{prefix_3800_content}";

            string prefix_3801 = "01";
            string prefix_3801_00 = "00";
            string prefix_3801_00_content = bankBin; // BIN của Bank
            prefix_3801_00 = $"{prefix_3801_00}{ReturnNumber(prefix_3801_00_content.Length)}{prefix_3801_00_content}";

            string prefix_3801_01 = "01";
            string prefix_3801_01_content = bankAccount.Trim();
            prefix_3801_01 = $"{prefix_3801_01}{ReturnNumber(prefix_3801_01_content.Length)}{prefix_3801_01_content}";

            prefix_3801 = $"{prefix_3801}{ReturnNumber(prefix_3801_00.Length + prefix_3801_01.Length)}{prefix_3801_00}{prefix_3801_01}";

            string prefix_3802 = "0208QRIBFTTA";
            prefix_38 = $"{prefix_38}{ReturnNumber(prefix_3800.Length + prefix_3801.Length + prefix_3802.Length)}{prefix_3800}{prefix_3801}{prefix_3802}";

            // Tag 53: Currency (704 = VND)
            string prefix_53 = "5303704";

            // Tag 54: Amount
            // Làm tròn lên (round up) để đảm bảo số tiền trong QR code khớp với số tiền khi thanh toán
            int amountInt = (int)Math.Ceiling(amount);
            string prefix_54 = $"54{ReturnNumber(amountInt.ToString().Length)}{amountInt}";

            // Tag 58: Country code
            string prefix_58 = "5802VN";

            // Tag 62: Additional Data Field Template
            string prefix_62 = "62";
            string prefix_62_08 = "08";
            string prefix_62_08_content = RemoveDiacritics(description ?? "", 50);
            if (string.IsNullOrWhiteSpace(prefix_62_08_content))
            {
                prefix_62_08_content = "";
            }
            prefix_62_08 = $"{prefix_62_08}{ReturnNumber(prefix_62_08_content.Length)}{prefix_62_08_content}";
            prefix_62 = $"{prefix_62}{ReturnNumber(prefix_62_08.Length)}{prefix_62_08}";

            qr = $"{qr}{prefix_38}{prefix_53}{prefix_54}{prefix_58}{prefix_62}6304";

            // Calculate CRC16
            byte[] data = Encoding.ASCII.GetBytes(qr);
            ushort crc16 = CalculateCRC16(data);
            string crc16String = crc16.ToString("X4");
            string last4Chars = crc16String.Substring(crc16String.Length - 4);
            qr = qr + last4Chars;

            return qr;
        }

        // CRC16-CCITT (poly 0x1021, init 0xFFFF) - theo format mẫu
        private static ushort CalculateCRC16(byte[] data)
        {
            ushort crc = 0xFFFF;
            ushort polynomial = 0x1021;

            for (int i = 0; i < data.Length; i++)
            {
                crc ^= (ushort)(data[i] << 8);
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x8000) != 0)
                    {
                        crc = (ushort)((crc << 1) ^ polynomial);
                    }
                    else
                    {
                        crc <<= 1;
                    }
                }
            }

            return crc;
        }

        // ===== Public APIs =====
        public static byte[] GeneratePaymentQRCode(string bankName, string bankAccount, string accountHolderName, decimal amount, string? description = null)
        {
            var bankBin = GetBankBin(bankName);
            var qrText = GenerateEMVQRCode(bankBin, bankAccount, accountHolderName ?? "", amount, description);

            using var qrGenerator = new QRCodeGenerator();
            // EMV khuyến nghị ECC M hoặc L; dùng M là ổn
            var qrCodeData = qrGenerator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new QRCode(qrCodeData);
            using var qrBitmap = qrCode.GetGraphic(20);

            using var ms = new MemoryStream();
            qrBitmap.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }

        public static string GeneratePaymentQRCodeBase64(string bankName, string bankAccount, string accountHolderName, decimal amount, string? description = null)
        {
            var qrBytes = GeneratePaymentQRCode(bankName, bankAccount, accountHolderName, amount, description);
            return Convert.ToBase64String(qrBytes);
        }

        // (Tuỳ chọn) QR chứa text info – không dùng cho VietQR thanh toán
        public static string GenerateBankInfoQRCodeBase64(string bankName, string bankAccount, string accountHolderName)
        {
            var qrText = $"STK:{bankAccount}\nTEN:{RemoveDiacritics(accountHolderName, 30).ToUpperInvariant()}\nNH:{RemoveDiacritics(bankName, 30).ToUpperInvariant()}";
            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCode(qrCodeData);
            using var qrBitmap = qrCode.GetGraphic(20);

            using var ms = new MemoryStream();
            qrBitmap.Save(ms, ImageFormat.Png);
            var qrBytes = ms.ToArray();
            return Convert.ToBase64String(qrBytes);
        }
    }

    // Model cho thông tin ngân hàng
    public class BankInfo
    {
        public string Name { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public string BIN { get; set; } = string.Empty;
    }
}
