using System;
using System.Collections.Generic;

namespace H2DProject.Data;

public partial class BankAccount
{
    public int Id { get; set; }

    public string BankId { get; set; } = null!;

    public string BankName { get; set; } = null!;

    public string AccountNumber { get; set; } = null!;

    public string AccountName { get; set; } = null!;

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }
}
