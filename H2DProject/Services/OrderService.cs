using Microsoft.EntityFrameworkCore;
using H2DProject.Data;
using H2DProject.Models;
using H2DProject.Models.ViewModels;

namespace H2DProject.Services;

public class OrderService
{
    private readonly H2DDbContext _db;

    public OrderService(H2DDbContext db) => _db = db;

    public async Task<Order> CreateOrderAsync(OrderViewModel model, int staffId)
    {
        var productIds = model.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId);

        // Lấy tất cả toppings cần dùng
        var toppingIds = model.Items
            .SelectMany(i => i.Toppings.Select(t => t.ToppingId))
            .Distinct().ToList();
        var toppings = await _db.Toppings
            .Where(t => toppingIds.Contains(t.ToppingId))
            .ToDictionaryAsync(t => t.ToppingId);

        var items = model.Items.Select(i => {
            var orderItem = new OrderItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = products[i.ProductId].Price,
                Note = i.Note
            };

            // Thêm toppings vào OrderItem
            foreach (var t in i.Toppings)
            {
                if (toppings.ContainsKey(t.ToppingId))
                {
                    orderItem.OrderItemToppings.Add(new OrderItemTopping
                    {
                        ToppingId = t.ToppingId,
                        Quantity = t.Quantity,
                        UnitPrice = toppings[t.ToppingId].Price
                    });
                }
            }
            return orderItem;
        }).ToList();

        // Tính tổng tiền gồm cả topping
        var subTotal = items.Sum(i =>
            (i.Quantity * i.UnitPrice) +
            i.OrderItemToppings.Sum(t => t.Quantity * t.UnitPrice)
        );
        var vatAmount = Math.Round(subTotal * 0.1m);

        var order = new Order
        {
            TableId = model.TableId ?? 0,
            StaffId = staffId,
            Note = model.Note,
            SubTotal = subTotal,
            Vatamount = vatAmount,
            Discount = 0,
            TotalAmount = subTotal + vatAmount,
            Status = "Pending",
            CreatedAt = DateTime.Now
        };

        foreach (var item in items)
            order.OrderItems.Add(item);

        if (model.TableId.HasValue && model.TableId > 0)
        {
            var table = await _db.Tables.FindAsync(model.TableId);
            if (table != null) table.Status = "Occupied";
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        return order;
    }

    public async Task<Order?> CompleteOrderAsync(int orderId, string paymentMethod)
    {
        var order = await _db.Orders
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order == null) return null;

        order.Status = "Completed";
        order.PaymentMethod = paymentMethod;
        order.CompletedAt = DateTime.Now;
        if (order.Table != null) order.Table.Status = "Available";

        await _db.SaveChangesAsync();
        return order;
    }

    public async Task<Order?> UpdateStatusAsync(int orderId, string newStatus)
    {
        var order = await _db.Orders
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order == null) return null;

        order.Status = newStatus;

        if (newStatus is "Completed" or "Cancelled")
        {
            if (newStatus == "Completed") order.CompletedAt = DateTime.Now;
            if (order.Table != null) order.Table.Status = "Available";
        }

        await _db.SaveChangesAsync();
        return order;
    }
}