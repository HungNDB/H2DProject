using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using H2DProject.Data;
using System.Drawing;
using System.Drawing.Printing;

namespace H2DProject.Controllers;

[Authorize]
public class LabelPrintController : Controller
{
    private readonly H2DDbContext _db;
    public LabelPrintController(H2DDbContext db) => _db = db;

    // ── GET /LabelPrint/Index/{orderId} ──────────────────
    public async Task<IActionResult> Index(int orderId)
    {
        var order = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.OrderItemToppings).ThenInclude(t => t.Topping)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order == null) return NotFound();
        return View(order);
    }

    // ── POST /LabelPrint/Print ────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Print([FromBody] PrintLabelRequest req)
    {
        if (req?.Items == null || !req.Items.Any())
            return BadRequest(new { error = "Không có món nào để in" });

        try
        {
            foreach (var item in req.Items)
            {
                PrintOneLabel(item);
                await Task.Delay(250); // Delay giữa các tem
            }
            return Json(new { success = true, count = req.Items.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── In 1 tem 50x30mm ─────────────────────────────────
    // 50mm = 189px @96dpi, 30mm = 113px @96dpi
    // Dùng đơn vị 1/100 inch: 50mm≈197, 30mm≈118
    private static void PrintOneLabel(PrintLabelItem item)
    {
        var pd = new PrintDocument();
        pd.PrinterSettings.PrinterName = "Xprinter XP-365B";

        var paper = new PaperSize("Label50x30", 197, 118);
        pd.DefaultPageSettings.PaperSize = paper;
        pd.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

        pd.PrintPage += (_, e) =>
        {
            var g = e.Graphics!;
            g.PageUnit = GraphicsUnit.Millimeter;

            float px = 2f;      // padding trái/phải
            float py = 2f;      // padding trên
            float pw = 46f;     // chiều rộng dùng được
            float y = py;

            var fmtL = new StringFormat
            {
                Alignment = StringAlignment.Near,
                Trimming = StringTrimming.EllipsisWord
            };
            var fmtR = new StringFormat { Alignment = StringAlignment.Far };
            var black = Brushes.Black;
            var gray = Brushes.Gray;

            // ── Tên món (to, đậm) ──────────────────────────────────
            float namePt = item.ProductName.Length > 20 ? 7.5f
                         : item.ProductName.Length > 14 ? 8.5f : 9.5f;

            // Thêm "ly X/Y" vào tên nếu > 1
            var displayName = item.CopyLabel > 1
                ? $"{item.ProductName} (ly {item.CopyIndex}/{item.CopyLabel})"
                : item.ProductName;

            using var fName = new Font("Arial", namePt, FontStyle.Bold);
            g.DrawString(displayName, fName, black, new RectangleF(px, y, pw, 10f), fmtL);
            y += namePt > 8.5f ? 8f : 9f;

            // ── Topping ────────────────────────────────────────────
            if (!string.IsNullOrEmpty(item.Toppings))
            {
                using var fTop = new Font("Arial", 6.5f, FontStyle.Regular);
                g.DrawString(item.Toppings, fTop, gray,
                    new RectangleF(px, y, pw, 6f), fmtL);
                y += 5.5f;
            }

            // ── Đường kẻ nét đứt ──────────────────────────────────
            using var dashPen = new Pen(Color.LightGray, 0.3f);
            dashPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
            g.DrawLine(dashPen, px, y, px + pw, y);
            y += 2.5f;

            // ── Ghi chú (đậm, dễ đọc — không cần nền đen) ─────────
            if (!string.IsNullOrEmpty(item.Note))
            {
                using var fNote = new Font("Arial", 8f, FontStyle.Bold);
                g.DrawString(item.Note, fNote, black,
                    new RectangleF(px, y, pw, 7f), fmtL);
                y += 7f;
            }

            // ── Giá (góc dưới trái) ────────────────────────────────
            // Đặt cố định gần đáy tem (28mm)
            float yPrice = 22f;
            using var fPrice = new Font("Arial", 7.5f, FontStyle.Bold);
            using var fSize = new Font("Arial", 5.5f, FontStyle.Regular);
            var priceStr = item.TotalPrice.ToString("N0") + " đ";
            g.DrawString(priceStr, fPrice, black,
                new RectangleF(px, yPrice, pw * 0.6f, 6f), fmtL);
        };

        pd.Print();
    }
}

// ── Models ───────────────────────────────────────────────
public class PrintLabelRequest
{
    public int orderId { get; set; }
    public List<PrintLabelItem> Items { get; set; } = new();
}

public class PrintLabelItem
{
    public int OrderItemId { get; set; }
    public string ProductName { get; set; } = "";
    public string? Toppings { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Note { get; set; }
    public int CopyIndex { get; set; } = 1; // Ly thứ mấy
    public int CopyLabel { get; set; } = 1; // Tổng số ly
}