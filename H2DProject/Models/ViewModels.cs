namespace H2DProject.Models.ViewModels;

// ── Login ──────────────────────────────────
public class LoginViewModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool   RememberMe { get; set; }
}

// ── Order ──────────────────────────────────
// Models/ViewModels/ViewModels.cs
public class OrderViewModel
{
    public int? TableId { get; set; }
    public string? Note { get; set; }
    public string? PaymentMethod { get; set; }
    public int? DiscountId { get; set; }   // ← giảm toàn đơn
    public decimal? ManualDiscount { get; set; } // ← nhập tay số tiền giảm
    public List<OrderItemInput> Items { get; set; } = new();
}

public class OrderItemInput
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
    public int? DiscountId { get; set; }   // ← giảm riêng từng món
    public decimal? ManualDiscount { get; set; }
    public List<ToppingInput> Toppings { get; set; } = new();
}

public class ToppingInput
{
    public int ToppingId { get; set; }
    public int Quantity { get; set; }
}

// ── Checkout ───────────────────────────────
public class CheckoutViewModel
{
    public int    OrderId       { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
}
