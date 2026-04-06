using Microsoft.AspNetCore.SignalR;

namespace H2DProject.Hubs;

public class OrderHub : Hub
{
    public async Task JoinGroup(string groupName)
        => await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

    public async Task LeaveGroup(string groupName)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
}

public class OrderNotification
{
    public int OrderId { get; set; }
    public string TableName { get; set; } = "";
    public string StaffName { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Note { get; set; }
    public List<OrderItemNotification> Items { get; set; } = new();
}

public class OrderItemNotification
{
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public string? Note { get; set; }
}

public class OrderStatusUpdate
{
    public int OrderId { get; set; }
    public string NewStatus { get; set; } = "";
    public string TableName { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}