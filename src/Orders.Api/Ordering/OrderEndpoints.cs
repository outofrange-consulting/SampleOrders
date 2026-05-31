using Wolverine.Http;
using Wolverine.Http.Marten;
using Wolverine.Marten;

namespace Orders.Api.Ordering;

public record CreateOrder(string CustomerId);
public record AddItem(string Sku, int Quantity, decimal UnitPrice);

public static class OrderEndpoints
{
    [WolverinePost("/orders")]
    public static (CreationResponse, IStartStream) Create(CreateOrder cmd)
    {
        var id = Guid.NewGuid();
        var start = MartenOps.StartStream<Order>(id, new OrderCreated(id, cmd.CustomerId));
        return (new CreationResponse($"/orders/{id}"), start);
    }

    [WolverinePost("/orders/{id}/items")]
    [EmptyResponse]
    public static OrderItemAdded AddItem(Guid id, AddItem cmd, [Aggregate] Order order)
        => new(cmd.Sku, cmd.Quantity, cmd.UnitPrice);

    [WolverineGet("/orders/{id}")]
    public static Order Get([Document] Order order) => order;
}
