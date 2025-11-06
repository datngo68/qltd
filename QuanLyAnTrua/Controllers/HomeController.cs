using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyAnTrua.Data;
using QuanLyAnTrua.Helpers;
using QuanLyAnTrua.Models;
using QuanLyAnTrua.Models.ViewModels;

namespace QuanLyAnTrua.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index(int? year, int? month)
    {
        var viewModel = new DashboardViewModel();
        
        // Default to current year if not specified
        var selectedYear = year ?? DateTime.Now.Year;
        var selectedMonth = month ?? DateTime.Now.Month;

        // Get all expenses (filter by month if specified)
        var query = _context.Expenses
            .Include(e => e.Payer)
            .Include(e => e.Participants)
                .ThenInclude(ep => ep.User)
            .AsQueryable();

        // Filter by GroupId
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

        // If month is specified, filter by that month
        if (month.HasValue)
        {
            var monthStart = new DateTime(selectedYear, selectedMonth, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            query = query.Where(e => e.ExpenseDate >= monthStart && e.ExpenseDate <= monthEnd);
        }
        // If only year is specified, filter by that year
        else if (year.HasValue)
        {
            var yearStart = new DateTime(selectedYear, 1, 1);
            var yearEnd = new DateTime(selectedYear, 12, 31);
            query = query.Where(e => e.ExpenseDate >= yearStart && e.ExpenseDate <= yearEnd);
        }

        var allExpenses = await query.ToListAsync();
        
        ViewBag.SelectedYear = selectedYear;
        ViewBag.SelectedMonth = selectedMonth;

        // Calculate totals
        viewModel.TotalExpenses = allExpenses.Sum(e => e.Amount);
        viewModel.TotalTransactions = allExpenses.Count;

        // Find top payer
        var topPayer = allExpenses
            .GroupBy(e => e.PayerId)
            .Select(g => new { PayerId = g.Key, Total = g.Sum(e => e.Amount) })
            .OrderByDescending(x => x.Total)
            .FirstOrDefault();

        if (topPayer != null)
        {
            var payer = await _context.Users.FindAsync(topPayer.PayerId);
            viewModel.TopPayerName = payer?.Name;
            viewModel.TopPayerAmount = topPayer.Total;
        }

        // Calculate weekly statistics (use selected year)
        var startOfYear = new DateTime(selectedYear, 1, 1);
        var endOfYear = new DateTime(selectedYear, 12, 31);

        var weeklyStats = new List<WeeklyStatistic>();
        var currentDate = startOfYear;
        int weekNumber = 1;

        while (currentDate <= endOfYear)
        {
            var weekStart = currentDate;
            var weekEnd = weekStart.AddDays(6);
            if (weekEnd > endOfYear)
                weekEnd = endOfYear;

            var weekExpenses = allExpenses
                .Where(e => e.ExpenseDate >= weekStart && e.ExpenseDate <= weekEnd)
                .ToList();

            if (weekExpenses.Any())
            {
                var weekStat = new WeeklyStatistic
                {
                    Week = weekNumber,
                    Year = selectedYear,
                    StartDate = weekStart,
                    EndDate = weekEnd,
                    TotalAmount = weekExpenses.Sum(e => e.Amount),
                    TransactionCount = weekExpenses.Count
                };

                        // Calculate user debts for the week
                        var userQuery = _context.Users.Where(u => u.IsActive).AsQueryable();
                        if (!SessionHelper.IsSuperAdmin(HttpContext))
                        {
                            var groupId = SessionHelper.GetGroupId(HttpContext);
                            if (groupId.HasValue)
                            {
                                userQuery = userQuery.Where(u => u.GroupId == groupId.Value);
                            }
                            else
                            {
                                userQuery = userQuery.Where(u => false);
                            }
                        }
                        var activeUsers = await userQuery.ToListAsync();
                foreach (var user in activeUsers)
                {
                    var totalAmount = 0m;
                    var paidAsPayer = 0m;

                    foreach (var expense in weekExpenses)
                    {
                        var participantCount = expense.Participants.Count;
                        if (participantCount > 0 && expense.Participants.Any(ep => ep.UserId == user.Id))
                        {
                            totalAmount += expense.Amount / participantCount;
                        }
                        if (expense.PayerId == user.Id)
                        {
                            paidAsPayer += expense.Amount;
                        }
                    }

                    if (totalAmount > 0 || paidAsPayer > 0)
                    {
                        weekStat.UserDebts.Add(new UserDebt
                        {
                            UserId = user.Id,
                            UserName = user.Name,
                            TotalAmount = totalAmount,
                            PaidAsPayer = paidAsPayer
                        });
                    }
                }

                weeklyStats.Add(weekStat);
            }

            currentDate = weekEnd.AddDays(1);
            weekNumber++;
        }

        viewModel.WeeklyStatistics = weeklyStats.OrderByDescending(w => w.Year).ThenByDescending(w => w.Week).Take(12).ToList();

        // Calculate monthly statistics (use selected year)
        var monthlyStats = new List<MonthlyStatistic>();
        for (int m = 1; m <= 12; m++)
        {
            var monthStart = new DateTime(selectedYear, m, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var monthExpenses = allExpenses
                .Where(e => e.ExpenseDate >= monthStart && e.ExpenseDate <= monthEnd)
                .ToList();

            if (monthExpenses.Any())
            {
                var monthStat = new MonthlyStatistic
                {
                    Year = selectedYear,
                    Month = m,
                    TotalAmount = monthExpenses.Sum(e => e.Amount),
                    TransactionCount = monthExpenses.Count
                };

                        // Calculate user debts for the month
                        var userQuery = _context.Users.Where(u => u.IsActive).AsQueryable();
                        var paymentQuery = _context.MonthlyPayments
                            .Where(mp => mp.Year == selectedYear && mp.Month == m)
                            .AsQueryable();
                        
                        if (!SessionHelper.IsSuperAdmin(HttpContext))
                        {
                            var groupId = SessionHelper.GetGroupId(HttpContext);
                            if (groupId.HasValue)
                            {
                                userQuery = userQuery.Where(u => u.GroupId == groupId.Value);
                                paymentQuery = paymentQuery.Where(mp => mp.GroupId == groupId.Value);
                            }
                            else
                            {
                                userQuery = userQuery.Where(u => false);
                                paymentQuery = paymentQuery.Where(mp => false);
                            }
                        }
                        
                        var activeUsers = await userQuery.ToListAsync();
                        var monthPayments = await paymentQuery.ToListAsync();

                foreach (var user in activeUsers)
                {
                    var totalAmount = 0m;
                    var paidAsPayer = 0m;

                    foreach (var expense in monthExpenses)
                    {
                        var participantCount = expense.Participants.Count;
                        if (participantCount > 0 && expense.Participants.Any(ep => ep.UserId == user.Id))
                        {
                            totalAmount += expense.Amount / participantCount;
                        }
                        if (expense.PayerId == user.Id)
                        {
                            paidAsPayer += expense.Amount;
                        }
                    }

                    var paidAmount = monthPayments.Where(p => p.UserId == user.Id).Sum(p => p.PaidAmount);

                    if (totalAmount > 0 || paidAmount > 0 || paidAsPayer > 0)
                    {
                        monthStat.UserDebts.Add(new UserDebt
                        {
                            UserId = user.Id,
                            UserName = user.Name,
                            TotalAmount = totalAmount,
                            PaidAmount = paidAmount,
                            PaidAsPayer = paidAsPayer
                        });
                    }
                }

                monthlyStats.Add(monthStat);
            }
        }

        viewModel.MonthlyStatistics = monthlyStats.OrderByDescending(m => m.Year).ThenByDescending(m => m.Month).ToList();

        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
