using Orders.Api.IntegrationEvents;

namespace Orders.Api.Ordering;

public static class OrderConfirmedHandler
{
    public static OrderPlaced Handle(OrderConfirmed e)
        => new(e.OrderId, e.CustomerId, e.Total, DateTimeOffset.UtcNow);
}
