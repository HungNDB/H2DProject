using System;
using System.Collections.Generic;

namespace H2DProject.Models;

public partial class Order
{
    public int OrderId { get; set; }

    public int? TableId { get; set; }

    public int? StaffId { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public decimal? SubTotal { get; set; }

    public decimal? Vatamount { get; set; }

    public decimal? Discount { get; set; }

    public decimal? TotalAmount { get; set; }

    public string? PaymentMethod { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual Staff? Staff { get; set; }

    public virtual Table? Table { get; set; }
}
