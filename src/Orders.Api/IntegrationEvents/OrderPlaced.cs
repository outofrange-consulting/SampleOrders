namespace Orders.Api.IntegrationEvents;

public record OrderPlaced(Guid OrderId, string CustomerId, decimal Total, DateTimeOffset PlacedAt);
