using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using H2DProject.Data;

namespace H2DProject.Controllers;

[Authorize]
public class SalesController : Controller
{
    private readonly H2DDbContext _db;

    public SalesController(H2DDbContext db)
    {
        _db = db;
    }

    // GET /Sales  hoặc  /Sales?from=2025-01-01&to=2025-12-31
    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var fromDate = from ?? DateTime.Today;
        var toDate = to ?? DateTime.Today;

        // Tổng doanh thu trong khoảng ngày (chỉ đơn Completed)
        var revenue = await _db.Orders
            .Where(o => o.Status == "Completed"
                     && o.CreatedAt >= fromDate
                     && o.CreatedAt < toDate.AddDays(1))
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

        // Tổng số đơn hoàn thành
        var orderCount = await _db.Orders
            .CountAsync(o => o.Status == "Completed"
                          && o.CreatedAt >= fromDate
                          && o.CreatedAt < toDate.AddDays(1));

        // Thống kê từng sản phẩm
        var items = await _db.OrderItems
            .Include(oi => oi.Product).ThenInclude(p => p.Category)
            .Include(oi => oi.Order)
            .Where(oi => oi.Order.Status == "Completed"
                      && oi.Order.CreatedAt >= fromDate
                      && oi.Order.CreatedAt < toDate.AddDays(1))
            .GroupBy(oi => new
            {
                oi.ProductId,
                oi.Product.Name,
                CategoryName = oi.Product.Category.Name,
                oi.Product.ImageUrl,
                oi.Product.Price
            })
            .Select(g => new ProductSalesRow
            {
                ProductId = g.Key.ProductId ?? 0,
                ProductName = g.Key.Name,
                CategoryName = g.Key.CategoryName,
                ImageUrl = g.Key.ImageUrl,
                UnitPrice = g.Key.Price,
                QtySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.Quantity * x.UnitPrice)
            })
            .OrderByDescending(x => x.QtySold)
            .ToListAsync();

        ViewBag.FromDate = fromDate.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate.ToString("yyyy-MM-dd");
        ViewBag.Revenue = revenue;
        ViewBag.OrderCount = orderCount;

        return View(items);
    }
}

// ── ViewModel ─────────────────────────────────────────────────────────────────
public class ProductSalesRow
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int QtySold { get; set; }
    public decimal Revenue { get; set; }
}