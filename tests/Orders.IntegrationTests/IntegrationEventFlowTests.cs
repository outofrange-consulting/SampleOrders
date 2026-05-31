using Alba;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.IntegrationEvents;
using Orders.Api.Ordering;
using Orders.Api.Shipping;
using TUnit;

namespace Orders.IntegrationTests;

public class IntegrationEventFlowTests
{
    [ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
    public required AppFixture App { get; init; }

    [Test]
    public async Task confirming_an_order_creates_a_shipment()
    {
        var orderId = await CreateAndConfirmOrder();

        // Poll until the Shipment document appears (async daemon + handlers need time)
        var shipment = await WaitForShipmentAsync(orderId, timeout: TimeSpan.FromSeconds(15));

        await Assert.That(shipment).IsNotNull();
        await Assert.That(shipment!.Status).IsEqualTo(ShipmentStatus.Pending);
        await Assert.That(shipment.CustomerId).IsEqualTo("cust-flow");
    }

    private async Task<Shipment?> WaitForShipmentAsync(Guid orderId, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(250);
            await using var session = App.Host.Services.GetRequiredService<IDocumentStore>()
                .LightweightSession();
            var shipment = await session.LoadAsync<Shipment>(orderId);
            if (shipment != null)
                return shipment;
        }
        return null;
    }

    private async Task<Guid> CreateAndConfirmOrder()
    {
        var created = await App.Host.Scenario(s =>
        {
            s.Post.Json(new CreateOrder("cust-flow")).ToUrl("/orders");
            s.StatusCodeShouldBe(201);
        });

        var location = created.Context.Response.Headers.Location.ToString();
        var orderId = Guid.Parse(location.Split('/').Last());

        await App.Host.Scenario(s =>
        {
            s.Post.Json(new AddItem("SKU-1", 1, 15m)).ToUrl($"{location}/items");
            s.StatusCodeShouldBe(204);
        });

        await App.Host.Scenario(s =>
        {
            s.Post.Json(new { }).ToUrl($"{location}/confirm");
            s.StatusCodeShouldBeOk();
        });

        return orderId;
    }
}
