using H2DProject.Data;
using H2DProject.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace H2DProject.Services;

public class OrderService
{
    private readonly H2DDbContext _db;
    public OrderService(H2DDbContext db) => _db = db;

    public async Task<Order> CreateOrderAsync(OrderViewModel model, int staffId)
    {
        // ── 1. Load product & topping 1 lần ─────────────────────────────────
        var productIds = model.Items.Select(i => i.ProductId).Distinct().ToList();
        var toppingIds = model.Items
            .SelectMany(i => i.Toppings)
            .Select(t => t.ToppingId)
            .Distinct()
            .ToList();

        var discountIds = model.Items
            .Where(i => i.DiscountId.HasValue)
            .Select(i => i.DiscountId!.Value)
            .ToList();
        if (model.DiscountId.HasValue)
            discountIds.Add(model.DiscountId.Value);

        var products = await _db.Products
            .Where(p => productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId);

        var toppings = toppingIds.Count > 0
            ? await _db.Toppings
                .Where(t => toppingIds.Contains(t.ToppingId))
                .ToDictionaryAsync(t => t.ToppingId)
            : new Dictionary<int, Topping>();

        var discounts = discountIds.Count > 0
            ? await _db.DiscountConfigs
                .Where(d => discountIds.Contains(d.DiscountId) && d.IsActive == true)
                .ToDictionaryAsync(d => d.DiscountId)
            : new Dictionary<int, DiscountConfig>();

        // ── 2. Tạo order ─────────────────────────────────────────────────────
        var order = new Order
        {
            TableId = model.TableId,
            StaffId = staffId,
            Status = "Pending",
            Note = model.Note,
            PaymentMethod = model.PaymentMethod,
            CreatedAt = DateTime.Now,
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(); // cần OrderId trước khi add items

        // ── 3. Build OrderItems + Toppings ───────────────────────────────────
        decimal subTotal = 0;

        foreach (var itemInput in model.Items)
        {
            if (!products.TryGetValue(itemInput.ProductId, out var product))
                throw new Exception($"Product {itemInput.ProductId} not found");

            // Giảm giá từng món
            decimal itemDiscount = 0;
            if (itemInput.DiscountId.HasValue && discounts.TryGetValue(itemInput.DiscountId.Value, out var dc))
                itemDiscount = CalcDiscount(dc, product.Price * itemInput.Quantity);
            else if (itemInput.ManualDiscount.HasValue)
                itemDiscount = itemInput.ManualDiscount.Value;

            decimal toppingTotal = 0;
            var itemToppings = new List<OrderItemTopping>();

            foreach (var ti in itemInput.Toppings)
            {
                if (!toppings.TryGetValue(ti.ToppingId, out var topping))
                    throw new Exception($"Topping {ti.ToppingId} not found");

                itemToppings.Add(new OrderItemTopping
                {
                    ToppingId = ti.ToppingId,
                    Quantity = ti.Quantity,
                    UnitPrice = topping.Price,
                });
                toppingTotal += topping.Price * ti.Quantity;
            }

            var orderItem = new OrderItem
            {
                OrderId = order.OrderId,
                ProductId = itemInput.ProductId,
                Quantity = itemInput.Quantity,
                UnitPrice = product.Price,
                Note = itemInput.Note,
                DiscountAmount = itemDiscount,
                OrderItemToppings = itemToppings,
            };
            _db.OrderItems.Add(orderItem);

            subTotal += product.Price * itemInput.Quantity + toppingTotal - itemDiscount;
        }

        // ── 4. Giảm giá toàn đơn ─────────────────────────────────────────────
        decimal orderDiscount = 0;
        if (model.DiscountId.HasValue && discounts.TryGetValue(model.DiscountId.Value, out var orderDc))
            orderDiscount = CalcDiscount(orderDc, subTotal);
        else if (model.ManualDiscount.HasValue)
            orderDiscount = model.ManualDiscount.Value;

        // ── 5. Tổng tiền ──────────────────────────────────────────────────────
        const decimal vatRate = 0m;
        var vatAmount = Math.Round(subTotal * vatRate);
        var total = subTotal - orderDiscount + vatAmount;

        order.SubTotal = subTotal;
        order.Discount = orderDiscount;
        order.Vatamount = vatAmount;
        order.TotalAmount = total;

        await _db.SaveChangesAsync();
        return order;
    }

    public async Task<Order?> UpdateStatusAsync(int orderId, string status)
    {
        var order = await _db.Orders
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order is null) return null;

        order.Status = status;
        if (status == "Completed") order.CompletedAt = DateTime.Now;

        await _db.SaveChangesAsync();
        return order;
    }

    public async Task<Order?> CompleteOrderAsync(int orderId, string paymentMethod)
    {
        var order = await _db.Orders
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order is null) return null;

        order.Status = "Completed";
        order.PaymentMethod = paymentMethod;
        order.CompletedAt = DateTime.Now;

        await _db.SaveChangesAsync();
        return order;
    }

    private static decimal CalcDiscount(DiscountConfig dc, decimal amount)
    {
        if (dc.Type == "Percent") return Math.Round(amount * dc.Value / 100);
        if (dc.Type == "Fixed") return Math.Min(dc.Value, amount);
        return 0;
    }
}
