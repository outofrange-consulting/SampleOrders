namespace Orders.Api.Ordering;

public record OrderItem(string Sku, int Quantity, decimal UnitPrice);

public class Order
{
    public Guid Id { get; set; }
    public string CustomerId { get; private set; } = "";
    public OrderStatus Status { get; private set; } = OrderStatus.Draft;
    public List<OrderItem> Items { get; private set; } = new();
    public decimal Total => Items.Sum(i => i.UnitPrice * i.Quantity);

    public void Apply(OrderCreated e)
    {
        Id = e.OrderId;
        CustomerId = e.CustomerId;
        Status = OrderStatus.Draft;
    }

    public void Apply(OrderItemAdded e) => Items.Add(new OrderItem(e.Sku, e.Quantity, e.UnitPrice));

    public void Apply(OrderConfirmed _) => Status = OrderStatus.Confirmed;

    public bool HasItems => Items.Count > 0;
}

public enum OrderStatus { Draft, Confirmed, Cancelled }

public record OrderCreated(Guid OrderId, string CustomerId);
public record OrderItemAdded(string Sku, int Quantity, decimal UnitPrice);
public record OrderConfirmed(Guid OrderId, string CustomerId, decimal Total, DateTimeOffset ConfirmedAt);
