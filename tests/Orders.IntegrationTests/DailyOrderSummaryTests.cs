using Alba;
using Marten;
using Marten.Events;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Ordering;
using TUnit;

namespace Orders.IntegrationTests;

public class DailyOrderSummaryTests
{
    [ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
    public required AppFixture App { get; init; }

    [Test]
    public async Task daily_summary_counts_and_totals_confirmed_orders()
    {
        var store = App.Host.Services.GetRequiredService<IDocumentStore>();
        var dateKey = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        // Flush any in-flight projections to get a stable baseline
        await store.WaitForNonStaleProjectionDataAsync(TimeSpan.FromSeconds(15));

        await using var baselineSession = store.LightweightSession();
        var baseline = await baselineSession.LoadAsync<DailyOrderSummary>(dateKey);
        var baselineCount = baseline?.OrderCount ?? 0;
        var baselineAmount = baseline?.TotalAmount ?? 0m;

        // Confirm two orders: 1 × 10 + 2 × 15 = 40 total
        await CreateAndConfirmOrder("cust-summary-1", quantity: 1, unitPrice: 10m);
        await CreateAndConfirmOrder("cust-summary-2", quantity: 2, unitPrice: 15m);

        // Wait for async daemon to project the OrderConfirmed events
        await store.WaitForNonStaleProjectionDataAsync(TimeSpan.FromSeconds(30));

        var result = await App.Host.Scenario(s =>
        {
            s.Get.Url($"/orders/daily-summary/{dateKey}");
            s.StatusCodeShouldBeOk();
        });

        var summary = result.ReadAsJson<DailyOrderSummary>();
        await Assert.That(summary).IsNotNull();
        // >= to tolerate confirmations from other parallel tests in this session
        await Assert.That(summary!.OrderCount).IsGreaterThanOrEqualTo(baselineCount + 2);
        await Assert.That(summary.TotalAmount).IsGreaterThanOrEqualTo(baselineAmount + 40m);
    }

    private async Task CreateAndConfirmOrder(string customerId, int quantity, decimal unitPrice)
    {
        var created = await App.Host.Scenario(s =>
        {
            s.Post.Json(new CreateOrder(customerId)).ToUrl("/orders");
            s.StatusCodeShouldBe(201);
        });
        var location = created.Context.Response.Headers.Location.ToString();

        await App.Host.Scenario(s =>
        {
            s.Post.Json(new AddItem("SKU-1", quantity, unitPrice)).ToUrl($"{location}/items");
            s.StatusCodeShouldBe(204);
        });

        await App.Host.Scenario(s =>
        {
            s.Post.Json(new { }).ToUrl($"{location}/confirm");
            s.StatusCodeShouldBeOk();
        });
    }
}
