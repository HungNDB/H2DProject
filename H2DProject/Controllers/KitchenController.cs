using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using H2DProject.Data;

namespace H2DProject.Controllers;

[Authorize]
public class KitchenController : Controller
{
    private readonly H2DDbContext _db;

    public KitchenController(H2DDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Màn hình bếp";

        var today = DateTime.Today;
        var orders = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Staff)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.OrderItemToppings)
                .ThenInclude(t => t.Topping)
            .Where(o => o.CreatedAt >= today && o.Status != "Cancelled")
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return View(orders);
    }
}
