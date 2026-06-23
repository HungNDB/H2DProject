using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using H2DProject.Data;
using Microsoft.AspNetCore.Antiforgery;

namespace H2DProject.Controllers;

[Authorize]
public class ToppingController : Controller
{
    private readonly H2DDbContext _db;

    public ToppingController(H2DDbContext db) => _db = db;

    // ── GET /Topping ──────────────────────────────────────────────
    public async Task<IActionResult> Index(string? search, bool? isAvailable)
    {
        var query = _db.Toppings.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Name.Contains(search));

        if (isAvailable.HasValue)
            query = query.Where(t => t.IsAvailable == isAvailable);

        var toppings = await query.OrderBy(t => t.Name).ToListAsync();

        ViewBag.Search      = search;
        ViewBag.IsAvailable = isAvailable;

        return View(toppings);
    }

    // ── GET /Topping/Create ───────────────────────────────────────
    public IActionResult Create() => View(new Topping { IsAvailable = true });

    // ── POST /Topping/Create ──────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Topping model)
    {
        ModelState.Remove("OrderItemToppings");

        if (!ModelState.IsValid) return View(model);

        _db.Toppings.Add(model);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã thêm topping \"{model.Name}\"";
        return RedirectToAction(nameof(Index));
    }

    // ── GET /Topping/Edit/{id} ────────────────────────────────────
    public async Task<IActionResult> Edit(int id)
    {
        var topping = await _db.Toppings.FindAsync(id);
        if (topping == null) return NotFound();
        return View(topping);
    }

    // ── POST /Topping/Edit/{id} ───────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Topping model)
    {
        if (id != model.ToppingId) return BadRequest();

        ModelState.Remove("OrderItemToppings");

        if (!ModelState.IsValid) return View(model);

        var existing = await _db.Toppings.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Name        = model.Name;
        existing.Price       = model.Price;
        existing.IsAvailable = model.IsAvailable;

        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã cập nhật topping \"{existing.Name}\"";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /Topping/Delete ──────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var topping = await _db.Toppings.FindAsync(id);
        if (topping == null) return NotFound();

        var hasOrders = await _db.OrderItemToppings.AnyAsync(t => t.ToppingId == id);
        if (hasOrders)
        {
            TempData["Error"] = $"Không thể xoá \"{topping.Name}\" vì đã có trong đơn hàng. Hãy ẩn topping thay vì xoá.";
            return RedirectToAction(nameof(Index));
        }

        _db.Toppings.Remove(topping);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã xoá topping \"{topping.Name}\"";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /Topping/ToggleAvailable ─────────────────────────────
    [HttpPost]
    public async Task<IActionResult> ToggleAvailable(int id)
    {
        var topping = await _db.Toppings.FindAsync(id);
        if (topping == null) return NotFound();

        topping.IsAvailable = !topping.IsAvailable;
        await _db.SaveChangesAsync();

        return Json(new { isAvailable = topping.IsAvailable });
    }
}
