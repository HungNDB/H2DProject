using System;
using System.Collections.Generic;

namespace H2DProject.Models;

public partial class DiscountConfig
{
    public int DiscountId { get; set; }

    public string Name { get; set; } = null!;

    public string Type { get; set; } = null!;

    public decimal Value { get; set; }

    public string ApplyTo { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}
