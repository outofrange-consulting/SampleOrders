using Orders.Api.Ordering;
using TUnit;

namespace Orders.UnitTests;

public class OrderAggregateTests
{
    [Test]
    public async Task applying_events_builds_current_state()
    {
        var order = new Order();
        var id = Guid.NewGuid();
        order.Apply(new OrderCreated(id, "cust-1"));
        order.Apply(new OrderItemAdded("SKU-1", 2, 10m));

        await Assert.That(order.Status).IsEqualTo(OrderStatus.Draft);
        await Assert.That(order.Total).IsEqualTo(20m);
        await Assert.That(order.HasItems).IsTrue();
    }

    [Test]
    public async Task confirmed_order_has_confirmed_status()
    {
        var order = new Order();
        var id = Guid.NewGuid();
        order.Apply(new OrderCreated(id, "cust-1"));
        order.Apply(new OrderItemAdded("SKU-1", 1, 5m));
        order.Apply(new OrderConfirmed(id, "cust-1", 5m, DateTimeOffset.UtcNow));

        await Assert.That(order.Status).IsEqualTo(OrderStatus.Confirmed);
    }

    [Test]
    [Arguments(0, false)]
    [Arguments(1, true)]
    public async Task has_items_depends_on_item_count(int itemCount, bool expected)
    {
        var order = new Order();
        order.Apply(new OrderCreated(Guid.NewGuid(), "c"));
        for (var i = 0; i < itemCount; i++)
            order.Apply(new OrderItemAdded("S", 1, 1m));

        await Assert.That(order.HasItems).IsEqualTo(expected);
    }

    [Test]
    public async Task total_is_sum_of_all_items()
    {
        var order = new Order();
        order.Apply(new OrderCreated(Guid.NewGuid(), "c"));
        order.Apply(new OrderItemAdded("A", 2, 5m));
        order.Apply(new OrderItemAdded("B", 3, 10m));

        await Assert.That(order.Total).IsEqualTo(40m);
    }

    [Test]
    public async Task multiple_items_are_accumulated()
    {
        var order = new Order();
        order.Apply(new OrderCreated(Guid.NewGuid(), "c"));
        order.Apply(new OrderItemAdded("A", 1, 1m));
        order.Apply(new OrderItemAdded("B", 1, 1m));

        await Assert.That(order.Items.Count).IsEqualTo(2);
    }
}
