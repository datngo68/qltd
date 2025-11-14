using Microsoft.AspNetCore.Mvc.ModelBinding;
using QuanLyAnTrua.Models.ViewModels;

namespace QuanLyAnTrua.Helpers
{
    /// <summary>
    /// Helper class để xử lý các logic liên quan đến form processing của Expenses
    /// </summary>
    public static class ExpenseFormHelper
    {
        /// <summary>
        /// Parse ParticipantAmounts từ Request.Form khi SplitType = Custom
        /// </summary>
        /// <param name="form">Form collection từ request</param>
        /// <returns>Dictionary chứa userId và amount tương ứng</returns>
        public static Dictionary<int, decimal> ParseParticipantAmounts(IFormCollection form)
        {
            var participantAmounts = new Dictionary<int, decimal>();

            foreach (var key in form.Keys)
            {
                if (key.StartsWith("ParticipantAmounts[") && key.EndsWith("]"))
                {
                    var userIdStr = key.Substring("ParticipantAmounts[".Length, key.Length - "ParticipantAmounts[".Length - 1);
                    if (int.TryParse(userIdStr, out int userId))
                    {
                        var amountStr = form[key].ToString();
                        if (decimal.TryParse(amountStr, out decimal amount))
                        {
                            participantAmounts[userId] = amount;
                        }
                    }
                }
            }

            return participantAmounts;
        }

        /// <summary>
        /// Validate custom split amounts
        /// </summary>
        /// <param name="viewModel">ExpenseViewModel cần validate</param>
        /// <param name="modelState">ModelState để thêm errors</param>
        public static void ValidateCustomSplitAmounts(ExpenseViewModel viewModel, ModelStateDictionary modelState)
        {
            if (viewModel.SplitType != SplitType.Custom)
            {
                return;
            }

            if (viewModel.ParticipantAmounts == null || !viewModel.ParticipantAmounts.Any())
            {
                modelState.AddModelError("ParticipantAmounts", "Vui lòng nhập số tiền cho từng người tham gia");
                return;
            }

            // Kiểm tra tất cả participants đều có số tiền
            var missingAmounts = viewModel.ParticipantIds
                ?.Where(id => !viewModel.ParticipantAmounts.ContainsKey(id) || viewModel.ParticipantAmounts[id] <= 0)
                .ToList();

            if (missingAmounts != null && missingAmounts.Any())
            {
                modelState.AddModelError("ParticipantAmounts", "Vui lòng nhập số tiền cho tất cả người tham gia");
                return;
            }

            // Kiểm tra tổng số tiền phải bằng Expense.Amount
            var totalAmount = viewModel.ParticipantAmounts.Values.Sum();
            if (Math.Abs(totalAmount - viewModel.Amount) > 0.01m) // Cho phép sai số làm tròn 0.01
            {
                modelState.AddModelError("ParticipantAmounts", 
                    $"Tổng số tiền của các người tham gia ({totalAmount:N0} đ) phải bằng tổng chi phí ({viewModel.Amount:N0} đ)");
            }
        }

        /// <summary>
        /// Tính amount cho participant dựa vào SplitType
        /// </summary>
        /// <param name="splitType">Loại chia tiền</param>
        /// <param name="participantId">ID của participant</param>
        /// <param name="participantAmounts">Dictionary chứa custom amounts (nếu có)</param>
        /// <returns>Amount cho participant, hoặc null nếu chia đều</returns>
        public static decimal? CalculateParticipantAmount(SplitType splitType, int participantId, Dictionary<int, decimal>? participantAmounts)
        {
            if (splitType == SplitType.Custom && participantAmounts != null && participantAmounts.ContainsKey(participantId))
            {
                return participantAmounts[participantId];
            }

            return null; // Null nghĩa là chia đều
        }
    }
}
