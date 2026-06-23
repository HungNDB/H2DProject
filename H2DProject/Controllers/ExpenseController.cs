using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using H2DProject.Data;

namespace H2DProject.Controllers;

[Authorize]
public class ExpenseController : Controller
{
    private readonly H2DDbContext _db;

    public ExpenseController(H2DDbContext db) => _db = db;

    // ── GET /Expense ─────────────────────────────────────
    public async Task<IActionResult> Index(
        DateOnly? from, DateOnly? to,
        int? categoryId, string? q)
    {
        var fromDate = from ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-29));
        var toDate   = to   ?? DateOnly.FromDateTime(DateTime.Today);

        var query = _db.Expenses
            .Include(e => e.Category)
            .Include(e => e.Staff)
            .Where(e => e.ExpenseDate >= fromDate && e.ExpenseDate <= toDate);

        if (categoryId.HasValue)
            query = query.Where(e => e.CategoryId == categoryId);

        if (!string.IsNullOrEmpty(q))
            query = query.Where(e => e.Name.Contains(q));

        var expenses = await query.OrderByDescending(e => e.ExpenseDate)
                                  .ThenByDescending(e => e.CreatedAt)
                                  .ToListAsync();

        ViewBag.From       = fromDate.ToString("yyyy-MM-dd");
        ViewBag.To         = toDate.ToString("yyyy-MM-dd");
        ViewBag.CategoryId = categoryId;
        ViewBag.Q          = q;
        ViewBag.Categories = await _db.ExpenseCategories
                                      .Where(c => c.IsActive)
                                      .OrderBy(c => c.DisplayOrder)
                                      .ToListAsync();

        // Tổng theo danh mục trong kỳ
        ViewBag.TotalAmount  = expenses.Sum(e => e.TotalAmount);
        ViewBag.CategorySums = expenses
            .GroupBy(e => e.Category.Name)
            .Select(g => new { Name = g.Key, Icon = g.First().Category.Icon, Total = g.Sum(e => e.TotalAmount) })
            .OrderByDescending(x => x.Total)
            .ToList();

        return View(expenses);
    }

    // ── POST /Expense/Create ─────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int categoryId, string name,
        decimal? quantity, string? unit,
        decimal unitPrice, string? note,
        DateOnly expenseDate)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Tên không được để trống");

        var totalAmount = quantity.HasValue ? quantity.Value * unitPrice : unitPrice;

        var staffClaim = User.FindFirst("StaffId")?.Value;
        int? staffId   = staffClaim != null ? int.Parse(staffClaim) : null;

        var expense = new Expense
        {
            CategoryId  = categoryId,
            Name        = name.Trim(),
            Quantity    = quantity,
            Unit        = unit?.Trim(),
            UnitPrice   = unitPrice,
            TotalAmount = totalAmount,
            Note        = note?.Trim(),
            ExpenseDate = expenseDate,
            StaffId     = staffId
        };

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();

        return Json(new
        {
            success     = true,
            expenseId   = expense.ExpenseId,
            totalAmount = expense.TotalAmount,
            name        = expense.Name
        });
    }

    // ── POST /Expense/Delete ─────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var expense = await _db.Expenses.FindAsync(id);
        if (expense == null) return NotFound();

        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync();

        return Json(new { success = true });
    }

    // ── GET /Expense/Summary ─────────────────────────────
    // API trả JSON cho chart
    [HttpGet]
    public async Task<IActionResult> Summary(int days = 30)
    {
        var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-days + 1));
        var to   = DateOnly.FromDateTime(DateTime.Today);

        var data = await _db.Expenses
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to)
            .GroupBy(e => e.ExpenseDate)
            .Select(g => new { date = g.Key.ToString("dd/MM"), total = g.Sum(e => e.TotalAmount) })
            .OrderBy(x => x.date)
            .ToListAsync();

        return Json(data);
    }
}
