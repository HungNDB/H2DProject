using System;
using System.Collections.Generic;

namespace H2DProject.Data;

public partial class Expense
{
    public int ExpenseId { get; set; }

    public int CategoryId { get; set; }

    public string Name { get; set; } = null!;

    public decimal? Quantity { get; set; }

    public string? Unit { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Note { get; set; }

    public DateOnly ExpenseDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? StaffId { get; set; }

    public virtual ExpenseCategory Category { get; set; } = null!;

    public virtual Staff? Staff { get; set; }
}
