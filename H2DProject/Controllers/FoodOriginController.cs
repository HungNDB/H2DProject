using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using H2DProject.Data;

namespace H2DProject.Controllers;

[Authorize]
public class FoodOriginController : Controller
{
    private readonly H2DDbContext _db;
    public FoodOriginController(H2DDbContext db) => _db = db;

    // ── GET /FoodOrigin ──────────────────────────────────────────
    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, string? q)
    {
        var fromDate = from ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-29));
        var toDate   = to   ?? DateOnly.FromDateTime(DateTime.Today);

        var query = _db.FoodOriginLogs
            .Include(f => f.Staff)
            .Where(f => f.EntryDate >= fromDate && f.EntryDate <= toDate);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(f => f.FoodName.Contains(q) || f.Supplier!.Contains(q));

        var logs = await query
            .OrderByDescending(f => f.EntryDate)
            .ThenByDescending(f => f.CreatedAt)
            .ToListAsync();

        ViewBag.From  = fromDate.ToString("yyyy-MM-dd");
        ViewBag.To    = toDate.ToString("yyyy-MM-dd");
        ViewBag.Q     = q;

        return View(logs);
    }

    // ── POST /FoodOrigin/Create ──────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        DateOnly entryDate, string foodName,
        decimal? quantity, string? unit,
        string? supplier, string? origin,
        string? invoiceInfo, string? condition,
        string? receivedBy, string? note)
    {
        if (string.IsNullOrWhiteSpace(foodName))
            return BadRequest("Tên thực phẩm không được để trống.");

        var staffClaim = User.FindFirst("StaffId")?.Value;
        int? staffId   = staffClaim != null ? int.Parse(staffClaim) : null;

        var log = new FoodOriginLog
        {
            EntryDate   = entryDate,
            FoodName    = foodName.Trim(),
            Quantity    = quantity,
            Unit        = unit?.Trim(),
            Supplier    = supplier?.Trim(),
            Origin      = origin?.Trim(),
            InvoiceInfo = invoiceInfo?.Trim(),
            Condition   = condition?.Trim(),
            ReceivedBy  = receivedBy?.Trim(),
            Note        = note?.Trim(),
            StaffId     = staffId
        };

        _db.FoodOriginLogs.Add(log);
        await _db.SaveChangesAsync();

        return Json(new
        {
            success     = true,
            logId       = log.LogId,
            foodName    = log.FoodName,
            entryDate   = log.EntryDate.ToString("dd/MM/yyyy"),
            supplier    = log.Supplier,
            origin      = log.Origin,
            condition   = log.Condition,
            receivedBy  = log.ReceivedBy,
            quantity    = log.Quantity,
            unit        = log.Unit,
            invoiceInfo = log.InvoiceInfo
        });
    }

    // ── POST /FoodOrigin/Delete ──────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var log = await _db.FoodOriginLogs.FindAsync(id);
        if (log == null) return NotFound();
        _db.FoodOriginLogs.Remove(log);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ── GET /FoodOrigin/ExportExcel ──────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ExportExcel(
        DateOnly? from, DateOnly? to, string? q,
        string? shopName, string? shopAddress, string? shopPhone)
    {
        var fromDate = from ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-29));
        var toDate   = to   ?? DateOnly.FromDateTime(DateTime.Today);

        var query = _db.FoodOriginLogs
            .Where(f => f.EntryDate >= fromDate && f.EntryDate <= toDate);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(f => f.FoodName.Contains(q) || f.Supplier!.Contains(q));

        var logs = await query
            .OrderBy(f => f.EntryDate)
            .ThenBy(f => f.CreatedAt)
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Nguồn Gốc Thực Phẩm");

        // ── Tiêu đề chính ───────────────────────────────────────
        ws.Cell("A1").Value = "SỔ THEO DÕI NGUỒN GỐC THỰC PHẨM";
        var titleRange = ws.Range("A1:I1");
        titleRange.Merge();
        titleRange.Style
            .Font.SetBold(true)
            .Font.SetFontSize(14)
            .Font.SetFontColor(XLColor.FromHtml("#1e3a5f"))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        ws.Row(1).Height = 28;

        // ── Thông tin cơ sở ─────────────────────────────────────
        ws.Cell("A2").Value = $"Tên cơ sở: {shopName ?? "..................................................................."}";
        ws.Range("A2:I2").Merge().Style.Font.SetItalic(true).Font.SetFontSize(10);

        ws.Cell("A3").Value = $"Địa chỉ: {shopAddress ?? "........................................................................"}";
        ws.Range("A3:I3").Merge().Style.Font.SetItalic(true).Font.SetFontSize(10);

        ws.Cell("A4").Value = $"Số điện thoại: {shopPhone ?? "..............................................................."}";
        ws.Range("A4:I4").Merge().Style.Font.SetItalic(true).Font.SetFontSize(10);

        ws.Cell("A5").Value = $"Kỳ theo dõi: {fromDate:dd/MM/yyyy} – {toDate:dd/MM/yyyy}";
        ws.Range("A5:I5").Merge().Style.Font.SetItalic(true).Font.SetFontSize(10)
            .Font.SetFontColor(XLColor.FromHtml("#0d9488"));

        ws.Row(6).Height = 6; // dòng trống

        // ── Header bảng ─────────────────────────────────────────
        int headerRow = 7;
        var headers = new[]
        {
            "STT", "Ngày nhập", "Tên thực phẩm",
            "Số lượng", "Đơn vị cung cấp",
            "Địa chỉ / Nguồn gốc", "Hóa đơn / Chứng từ",
            "Tình trạng", "Người nhận"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style
                .Font.SetBold(true)
                .Font.SetFontSize(10)
                .Font.SetFontColor(XLColor.White)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#1e3a5f"))
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                .Alignment.SetWrapText(true)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                .Border.SetOutsideBorderColor(XLColor.FromHtml("#4a6fa5"));
        }
        ws.Row(headerRow).Height = 32;

        // ── Dữ liệu ─────────────────────────────────────────────
        for (int i = 0; i < logs.Count; i++)
        {
            var log    = logs[i];
            int row    = headerRow + 1 + i;
            bool isEven = i % 2 == 1;
            var bgColor = isEven ? XLColor.FromHtml("#f0f4fa") : XLColor.White;

            var cells = new object?[]
            {
                i + 1,
                log.EntryDate.ToString("dd/MM/yyyy"),
                log.FoodName,
                log.Quantity.HasValue
                    ? $"{log.Quantity:G29} {log.Unit}".Trim()
                    : (object?)"",
                log.Supplier ?? "",
                log.Origin   ?? "",
                log.InvoiceInfo ?? "",
                log.Condition   ?? "",
                log.ReceivedBy  ?? ""
            };

            for (int col = 0; col < cells.Length; col++)
            {
                var cell = ws.Cell(row, col + 1);
                cell.Value   = cells[col]?.ToString() ?? "";
                cell.Style
                    .Fill.SetBackgroundColor(bgColor)
                    .Font.SetFontSize(10)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                    .Alignment.SetWrapText(true)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                    .Border.SetOutsideBorderColor(XLColor.FromHtml("#c9d6e8"));

                // Căn giữa cột STT và ngày
                if (col == 0 || col == 1)
                    cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            ws.Row(row).Height = 20;
        }

        // ── Dòng ghi chú cuối ───────────────────────────────────
        int noteRow = headerRow + 1 + logs.Count + 1;
        ws.Cell(noteRow, 1).Value =
            "Ghi chú: Sổ này được lập theo quy định về truy xuất nguồn gốc thực phẩm.";
        ws.Range(noteRow, 1, noteRow, 9).Merge().Style
            .Font.SetItalic(true)
            .Font.SetFontSize(9)
            .Font.SetFontColor(XLColor.Gray);

        // ── Độ rộng cột ─────────────────────────────────────────
        ws.Column(1).Width = 5;   // STT
        ws.Column(2).Width = 12;  // Ngày
        ws.Column(3).Width = 24;  // Tên thực phẩm
        ws.Column(4).Width = 14;  // Số lượng
        ws.Column(5).Width = 22;  // Đơn vị cung cấp
        ws.Column(6).Width = 22;  // Nguồn gốc
        ws.Column(7).Width = 18;  // Hóa đơn
        ws.Column(8).Width = 16;  // Tình trạng
        ws.Column(9).Width = 18;  // Người nhận

        // ── Freeze header ────────────────────────────────────────
        ws.SheetView.FreezeRows(headerRow);

        // ── Xuất file ────────────────────────────────────────────
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Seek(0, SeekOrigin.Begin);

        var fileName = $"NguonGocThucPham_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
