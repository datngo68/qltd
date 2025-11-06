using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyAnTrua.Data;
using QuanLyAnTrua.Helpers;

namespace QuanLyAnTrua.ViewComponents;

public class PendingPaymentsCountViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;

    public PendingPaymentsCountViewComponent(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var currentYear = DateTime.Now.Year;
        var currentMonth = DateTime.Now.Month;
        var currentUserId = SessionHelper.GetUserId(HttpContext);

        var query = _context.MonthlyPayments
            .Where(mp => mp.Status == "Pending" && mp.Year == currentYear && mp.Month == currentMonth)
            .AsQueryable();

        // Filter by permissions
        if (SessionHelper.IsSuperAdmin(HttpContext))
        {
            // SuperAdmin thấy tất cả
        }
        else if (SessionHelper.IsAdmin(HttpContext))
        {
            // Admin chỉ thấy payments trong nhóm của mình
            var groupId = SessionHelper.GetGroupId(HttpContext);
            if (groupId.HasValue)
            {
                query = query.Where(mp => mp.GroupId == groupId.Value);
            }
            else
            {
                query = query.Where(mp => false);
            }
        }
        else if (SessionHelper.IsUser(HttpContext) && currentUserId.HasValue)
        {
            // User thường chỉ thấy payments mà họ là creditor (người được nợ)
            // Lọc theo CreditorId: nếu payment có CreditorId thì dùng CreditorId, nếu không thì fallback về expense
            var startDate = new DateTime(currentYear, currentMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Lấy tất cả payments trong tháng đó
            var allPayments = await query.ToListAsync();

            // Lọc payments: chỉ lấy những payment mà user là creditor
            var validPaymentIds = new List<int>();
            foreach (var payment in allPayments)
            {
                bool isForCurrentUser = false;

                // Nếu payment có CreditorId, kiểm tra xem có phải user hiện tại không
                if (payment.CreditorId.HasValue)
                {
                    isForCurrentUser = payment.CreditorId.Value == currentUserId.Value;
                }
                else
                {
                    // Nếu không có CreditorId, fallback về kiểm tra expense (cho các payment cũ)
                    var isCreditor = await _context.Expenses
                        .Include(e => e.Participants)
                        .AnyAsync(e => e.PayerId == currentUserId.Value &&
                                      e.Participants.Any(p => p.UserId == payment.UserId && p.UserId != currentUserId.Value) &&
                                      e.ExpenseDate >= startDate && e.ExpenseDate <= endDate);

                    if (isCreditor)
                    {
                        isForCurrentUser = true;
                    }
                }

                if (isForCurrentUser)
                {
                    validPaymentIds.Add(payment.Id);
                }
            }

            if (validPaymentIds.Any())
            {
                query = query.Where(mp => validPaymentIds.Contains(mp.Id));
            }
            else
            {
                query = query.Where(mp => false);
            }
        }
        else
        {
            query = query.Where(mp => false);
        }

        var count = await query.CountAsync();

        return View(count);
    }
}

