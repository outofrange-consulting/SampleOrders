namespace Orders.Api.Shipping;

public class Shipment
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = "";
    public ShipmentStatus Status { get; set; } = ShipmentStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
}

public enum ShipmentStatus { Pending, Dispatched }
