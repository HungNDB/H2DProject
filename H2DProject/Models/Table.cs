using System;
using System.Collections.Generic;

namespace H2DProject.Models;

public partial class Table
{
    public int TableId { get; set; }

    public string TableNumber { get; set; } = null!;

    public string? Zone { get; set; }

    public int? Capacity { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
