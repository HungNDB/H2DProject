using System;

namespace H2DProject.Data;

public partial class FoodOriginLog
{
    public int LogId { get; set; }

    /// <summary>Ngày nhập hàng</summary>
    public DateOnly EntryDate { get; set; }

    /// <summary>Tên thực phẩm (thịt heo, rau cải, cá lóc...)</summary>
    public string FoodName { get; set; } = null!;

    /// <summary>Số lượng nhập</summary>
    public decimal? Quantity { get; set; }

    /// <summary>Đơn vị (kg, g, lít...)</summary>
    public string? Unit { get; set; }

    /// <summary>Đơn vị cung cấp / tên người bán / cơ sở</summary>
    public string? Supplier { get; set; }

    /// <summary>Địa chỉ / nguồn gốc (chợ, siêu thị, lò mổ, công ty...)</summary>
    public string? Origin { get; set; }

    /// <summary>Hóa đơn/chứng từ: có hoặc không, nếu có ghi số hóa đơn</summary>
    public string? InvoiceInfo { get; set; }

    /// <summary>Tình trạng: tươi sống, đông lạnh, đóng gói...</summary>
    public string? Condition { get; set; }

    /// <summary>Người nhận hàng (ký tên)</summary>
    public string? ReceivedBy { get; set; }

    /// <summary>Ghi chú thêm</summary>
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? StaffId { get; set; }

    public virtual Staff? Staff { get; set; }
}
