using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyAnTrua.Models.ViewModels;

namespace QuanLyAnTrua.Helpers
{
    /// <summary>
    /// Helper xử lý các nghiệp vụ liên quan đến nợ.
    /// </summary>
    public static class DebtHelper
    {
        private const decimal Tolerance = 0.01m;

        /// <summary>
        /// Khấu trừ các khoản nợ hai chiều giữa cùng một cặp người dùng để chỉ giữ lại nợ thuần.
        /// </summary>
        /// <param name="debtDetails">Danh sách chi tiết nợ (tham chiếu).</param>
        public static void NetMutualDebts(List<DebtDetail> debtDetails)
        {
            if (debtDetails == null || debtDetails.Count == 0)
            {
                return;
            }

            var debtLookup = debtDetails
                .GroupBy(d => (d.DebtorId, d.CreditorId))
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(d => d.ExpenseDate)
                          .ThenBy(d => d.ExpenseId)
                          .ToList());

            var processedPairs = new HashSet<(int, int)>();

            foreach (var kvp in debtLookup)
            {
                var key = kvp.Key;

                if (processedPairs.Contains(key))
                {
                    continue;
                }

                var reverseKey = (key.Item2, key.Item1);
                if (!debtLookup.TryGetValue(reverseKey, out var reverseList))
                {
                    continue;
                }

                var forwardList = kvp.Value;

                var forwardTotal = forwardList.Sum(d => Math.Max(0, d.RemainingAmount));
                var reverseTotal = reverseList.Sum(d => Math.Max(0, d.RemainingAmount));

                var amountToNet = Math.Min(forwardTotal, reverseTotal);
                if (amountToNet <= 0)
                {
                    continue;
                }

                DeductAmount(forwardList, amountToNet);
                DeductAmount(reverseList, amountToNet);

                processedPairs.Add(key);
                processedPairs.Add(reverseKey);
            }

            NormalizeResiduals(debtDetails);
        }

        private static void DeductAmount(List<DebtDetail> details, decimal amountToDeduct)
        {
            var remaining = amountToDeduct;

            foreach (var debt in details)
            {
                if (remaining <= 0)
                {
                    break;
                }

                if (debt.RemainingAmount <= 0)
                {
                    continue;
                }

                var deduction = Math.Min(debt.RemainingAmount, remaining);
                debt.RemainingAmount -= deduction;
                remaining -= deduction;
            }
        }

        private static void NormalizeResiduals(IEnumerable<DebtDetail> debtDetails)
        {
            foreach (var debt in debtDetails)
            {
                if (Math.Abs(debt.RemainingAmount) <= Tolerance)
                {
                    debt.RemainingAmount = 0;
                }
            }
        }
    }
}

