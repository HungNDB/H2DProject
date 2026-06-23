using System;
using System.Collections.Generic;

namespace H2DProject.Data;

public partial class ExpenseCategory
{
    public int CategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string Icon { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
