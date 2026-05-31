using Marten;
using Orders.Api.IntegrationEvents;

namespace Orders.Api.Shipping;

public static class ShipmentHandler
{
    public static void Handle(OrderPlaced e, IDocumentSession session)
    {
        session.Store(new Shipment
        {
            Id = e.OrderId,
            CustomerId = e.CustomerId,
            Status = ShipmentStatus.Pending,
            CreatedAt = e.PlacedAt
        });
    }
}
