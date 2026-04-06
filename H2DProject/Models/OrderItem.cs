using System;
using System.Collections.Generic;

namespace H2DProject.Models;

public partial class OrderItem
{
    public int OrderItemId { get; set; }

    public int? OrderId { get; set; }

    public int? ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal? SubTotal { get; set; }

    public string? Note { get; set; }

    public decimal DiscountAmount { get; set; }

    public virtual Order? Order { get; set; }

    public virtual ICollection<OrderItemTopping> OrderItemToppings { get; set; } = new List<OrderItemTopping>();

    public virtual Product? Product { get; set; }
}
