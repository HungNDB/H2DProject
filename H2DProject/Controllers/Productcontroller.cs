using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using H2DProject.Data;

namespace H2DProject.Controllers;

[Authorize]
public class ProductController : Controller
{
    private readonly H2DDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ProductController(H2DDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // ── GET /Product ─────────────────────────────────────────────
    public async Task<IActionResult> Index(string? search, int? categoryId, bool? isAvailable)
    {
        var query = _db.Products
            .Include(p => p.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search));

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId);

        if (isAvailable.HasValue)
            query = query.Where(p => p.IsAvailable == isAvailable);

        var products = await query
            .OrderBy(p => p.Category!.DisplayOrder)
            .ThenBy(p => p.Name)
            .ToListAsync();

        ViewBag.Categories = await _db.Categories.Where(c => c.IsActive == true).OrderBy(c => c.DisplayOrder).ToListAsync();
        ViewBag.Search = search;
        ViewBag.CategoryId = categoryId;
        ViewBag.IsAvailable = isAvailable;

        return View(products);
    }

    // ── GET /Product/Create ───────────────────────────────────────
    public async Task<IActionResult> Create()
    {
        await LoadCategories();
        return View(new Product { IsAvailable = true });
    }

    // ── POST /Product/Create ──────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product model, IFormFile? imageFile)
    {
        ModelState.Remove("Category");
        ModelState.Remove("OrderItems");
        ModelState.Remove("ImageUrl");

        if (!ModelState.IsValid)
        {
            await LoadCategories();
            return View(model);
        }

        if (imageFile != null && imageFile.Length > 0)
            model.ImageUrl = await SaveImage(imageFile);

        model.CreatedAt = DateTime.Now;
        _db.Products.Add(model);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã thêm sản phẩm \"{model.Name}\"";
        return RedirectToAction(nameof(Index));
    }

    // ── GET /Product/Edit/{id} ────────────────────────────────────
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();
        await LoadCategories(product.CategoryId);
        return View(product);
    }

    // ── POST /Product/Edit/{id} ───────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Product model, IFormFile? imageFile)
    {
        if (id != model.ProductId) return BadRequest();

        ModelState.Remove("Category");
        ModelState.Remove("OrderItems");
        ModelState.Remove("ImageUrl");

        if (!ModelState.IsValid)
        {
            await LoadCategories(model.CategoryId);
            return View(model);
        }

        var existing = await _db.Products.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Name = model.Name;
        existing.Description = model.Description;
        existing.Price = model.Price;
        existing.CategoryId = model.CategoryId;
        existing.IsAvailable = model.IsAvailable;

        if (imageFile != null && imageFile.Length > 0)
        {
            // Xoá ảnh cũ nếu có
            DeleteImage(existing.ImageUrl);
            existing.ImageUrl = await SaveImage(imageFile);
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã cập nhật sản phẩm \"{existing.Name}\"";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /Product/Delete/{id} ─────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();

        // Kiểm tra đã có trong đơn hàng chưa
        var hasOrders = await _db.OrderItems.AnyAsync(oi => oi.ProductId == id);
        if (hasOrders)
        {
            TempData["Error"] = $"Không thể xoá \"{product.Name}\" vì đã có trong đơn hàng. Hãy ẩn sản phẩm thay vì xoá.";
            return RedirectToAction(nameof(Index));
        }

        DeleteImage(product.ImageUrl);
        _db.Products.Remove(product);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã xoá sản phẩm \"{product.Name}\"";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /Product/ToggleAvailable ─────────────────────────────
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ToggleAvailable(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();

        product.IsAvailable = !product.IsAvailable;
        await _db.SaveChangesAsync();

        return Json(new { isAvailable = product.IsAvailable });
    }

    // ── Helpers ───────────────────────────────────────────────────
    private async Task LoadCategories(int? selected = null)
    {
        var cats = await _db.Categories
            .Where(c => c.IsActive == true)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        ViewBag.CategoryList = new SelectList(cats, "CategoryId", "Name", selected);
    }

    private async Task<string> SaveImage(IFormFile file)
    {
        var uploads = Path.Combine(_env.WebRootPath, "uploads", "products");
        Directory.CreateDirectory(uploads);

        var ext = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid()}{ext}";
        var path = Path.Combine(uploads, fileName);

        await using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/uploads/products/{fileName}";
    }

    private void DeleteImage(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return;
        var path = Path.Combine(_env.WebRootPath, imageUrl.TrimStart('/'));
        if (System.IO.File.Exists(path))
            System.IO.File.Delete(path);
    }
}