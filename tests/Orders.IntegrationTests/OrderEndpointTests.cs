using Alba;
using Orders.Api.Ordering;
using TUnit;

namespace Orders.IntegrationTests;

public class OrderEndpointTests
{
    [ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
    public required AppFixture App { get; init; }

    [Test]
    public async Task create_order_returns_201_with_location()
    {
        var result = await App.Host.Scenario(s =>
        {
            s.Post.Json(new CreateOrder("cust-1")).ToUrl("/orders");
            s.StatusCodeShouldBe(201);
        });

        var location = result.Context.Response.Headers.Location.ToString();
        await Assert.That(location).IsNotNull();
        await Assert.That(location).StartsWith("/orders/");
    }

    [Test]
    public async Task create_add_confirm_round_trip()
    {
        // CREATE
        var created = await App.Host.Scenario(s =>
        {
            s.Post.Json(new CreateOrder("cust-1")).ToUrl("/orders");
            s.StatusCodeShouldBe(201);
        });

        var location = created.Context.Response.Headers.Location.ToString();

        // ADD ITEM — [EmptyResponse] endpoint returns 204 No Content
        await App.Host.Scenario(s =>
        {
            s.Post.Json(new AddItem("SKU-1", 2, 10m)).ToUrl($"{location}/items");
            s.StatusCodeShouldBe(204);
        });

        // GET — should be in Draft status
        var getResult = await App.Host.Scenario(s =>
        {
            s.Get.Url(location);
            s.StatusCodeShouldBeOk();
        });
        var draft = getResult.ReadAsJson<Order>();
        await Assert.That(draft!.Status).IsEqualTo(OrderStatus.Draft);

        // CONFIRM
        var confirmed = await App.Host.Scenario(s =>
        {
            s.Post.Json(new { }).ToUrl($"{location}/confirm");
            s.StatusCodeShouldBeOk();
        });
        var order = confirmed.ReadAsJson<Order>();
        await Assert.That(order!.Status).IsEqualTo(OrderStatus.Confirmed);
        await Assert.That(order.Total).IsEqualTo(20m);
    }

    [Test]
    public async Task confirming_empty_order_returns_400()
    {
        var created = await App.Host.Scenario(s =>
        {
            s.Post.Json(new CreateOrder("cust-1")).ToUrl("/orders");
            s.StatusCodeShouldBe(201);
        });

        var location = created.Context.Response.Headers.Location.ToString();

        await App.Host.Scenario(s =>
        {
            s.Post.Json(new { }).ToUrl($"{location}/confirm");
            s.StatusCodeShouldBe(400);
        });
    }

    [Test]
    public async Task get_nonexistent_order_returns_404()
    {
        await App.Host.Scenario(s =>
        {
            s.Get.Url($"/orders/{Guid.NewGuid()}");
            s.StatusCodeShouldBe(404);
        });
    }
}
