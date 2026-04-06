using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using H2DProject.Data;
using H2DProject.Hubs;
using H2DProject.Models;
using H2DProject.Models.ViewModels;
using H2DProject.Services;

namespace H2DProject.Controllers;

[Authorize]
public class OrderController : Controller
{
    private readonly H2DDbContext  _db;
    private readonly OrderService  _orderService;
    private readonly IHubContext<OrderHub> _hub;
    private readonly PrintService _printService;

    public OrderController(H2DDbContext db, OrderService orderService,
                           IHubContext<OrderHub> hub, PrintService printService)
    {
        _db = db;
        _orderService = orderService;
        _hub = hub;
        _printService = printService;
    }
    // ── GET /Order ── POS chính ──────────────
    public async Task<IActionResult> Index()
    {
        ViewBag.Products = await _db.Products
            .Include(p => p.Category)
            .Where(p => (bool)p.IsAvailable)
            .OrderBy(p => p.Category.DisplayOrder)
            .ThenBy(p => p.Name)
            .ToListAsync();

        ViewBag.Tables = await _db.Tables
            .OrderBy(t => t.TableId)
            .ToListAsync();

        ViewBag.Categories = await _db.Categories
            .Where(c => (bool)c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        ViewBag.Toppings = await _db.Toppings
        .Where(t => t.IsAvailable == true)
        .OrderBy(t => t.Name)
        .ToListAsync();

        return View();
    }

    // ── POST /Order/Create ───────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] string orderJson)
    {
        var model = System.Text.Json.JsonSerializer.Deserialize<OrderViewModel>(
            orderJson,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (model == null) return BadRequest("Invalid data");

        try
        {
            var staffClaim = User.FindFirst("StaffId")?.Value;
            if (staffClaim == null) return Unauthorized();

            var staffId = int.Parse(staffClaim);
            var order = await _orderService.CreateOrderAsync(model, staffId);

            var full = await _db.Orders
                .Include(o => o.Table)
                .Include(o => o.Staff)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
                .FirstAsync(o => o.OrderId == order.OrderId);

            var notification = new OrderNotification
            {
                OrderId = full.OrderId,
                TableName = full.Table?.TableNumber ?? "",
                StaffName = full.Staff?.FullName ?? "",
                Status = full.Status ?? "",
                TotalAmount = (decimal)(double)(full.TotalAmount ?? 0),
                CreatedAt = full.CreatedAt ?? DateTime.Now,
                Note = full.Note,
                Items = full.OrderItems.Select(oi => new OrderItemNotification
                {
                    ProductName = oi.Product?.Name ?? "",
                    Quantity = oi.Quantity,
                    Note = oi.Note
                }).ToList()
            };

            await _hub.Clients.Group("kitchen").SendAsync("NewOrder", notification);
            await _hub.Clients.Group("manager").SendAsync("NewOrder", notification);

            return Json(new { orderId = order.OrderId, total = (double)(full.TotalAmount ?? 0) });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    // ── POST /Order/UpdateStatus ─────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus([FromForm] int orderId, [FromForm] string status)
    {
        try
        {
            var order = await _orderService.UpdateStatusAsync(orderId, status);
            if (order == null) return NotFound();

            var update = new OrderStatusUpdate
            {
                OrderId = orderId,
                NewStatus = status,
                TableName = order.Table?.TableNumber ?? "",
                UpdatedAt = DateTime.Now
            };

            await _hub.Clients.Group("pos").SendAsync("OrderStatusChanged", update);
            await _hub.Clients.Group("manager").SendAsync("OrderStatusChanged", update);

            return Json(new { success = true, newStatus = status });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    // ── GET /Order/Checkout/{id} ─────────────
    public async Task<IActionResult> Checkout(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null) return NotFound();
        return View(order);
    }

    // ── POST /Order/Checkout ─────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("Checkout")]
    public async Task<IActionResult> CheckoutConfirm(int orderId, string paymentMethod)
    {
        var order = await _orderService.CompleteOrderAsync(orderId, paymentMethod);
        if (order == null) return NotFound();

        var update = new OrderStatusUpdate
        {
            OrderId   = orderId,
            NewStatus = "Completed",
            TableName = order.Table.TableNumber
        };

        await _hub.Clients.Group("kitchen").SendAsync("OrderStatusChanged", update);
        await _hub.Clients.Group("manager").SendAsync("OrderStatusChanged", update);

        return RedirectToAction("Receipt", new { id = orderId });
    }

    // ── GET /Order/Receipt/{id} ──────────────
    public async Task<IActionResult> Receipt(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Staff)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null) return NotFound();
        return View(order);
    }

    // ── GET /Order/History ───────────────────
    public async Task<IActionResult> History(DateTime? from, DateTime? to, string? status)
    {
        var query = _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Staff)
            .AsQueryable();

        var fromDate = from ?? DateTime.Today;
        var toDate   = to   ?? DateTime.Today;

        query = query.Where(o => o.CreatedAt >= fromDate
                              && o.CreatedAt <  toDate.AddDays(1));

        if (!string.IsNullOrEmpty(status))
            query = query.Where(o => o.Status == status);

        ViewBag.From   = fromDate.ToString("yyyy-MM-dd");
        ViewBag.To     = toDate.ToString("yyyy-MM-dd");
        ViewBag.Status = status;
        ViewBag.Revenue = await query
            .Where(o => o.Status == "Completed")
            .SumAsync(o => o.TotalAmount);

        var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
        return View(orders);
    }

    [HttpGet]
    [AllowAnonymous]
    [Produces("application/json")]
    public async Task<IActionResult> Print(int id)
    {
        try
        {
            var order = await _db.Orders
                .Include(o => o.Table)
                .Include(o => o.Staff)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.OrderItemToppings)
                        .ThenInclude(t => t.Topping)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null) return NotFound();

            await _printService.PrintReceiptAsync(order);

            // Dùng Content thay vì Ok()
            return Content("{\"success\":true}", "application/json");
        }
        catch (Exception ex)
        {
            return Content($"{{\"error\":\"{ex.Message}\"}}", "application/json");
        }
    }
}
