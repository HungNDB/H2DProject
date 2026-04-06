using System;
using System.Collections.Generic;

namespace H2DProject.Models;

public partial class Topping
{
    public int ToppingId { get; set; }

    public string Name { get; set; } = null!;

    public decimal Price { get; set; }

    public bool IsAvailable { get; set; }

    public virtual ICollection<OrderItemTopping> OrderItemToppings { get; set; } = new List<OrderItemTopping>();
}
