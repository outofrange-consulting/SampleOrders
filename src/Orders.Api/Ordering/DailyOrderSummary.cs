using Marten.Events.Projections;

namespace Orders.Api.Ordering;

public class DailyOrderSummary
{
    public string Id { get; set; } = "";
    public DateOnly Date { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalAmount { get; set; }
}

public partial class DailyOrderSummaryProjection : MultiStreamProjection<DailyOrderSummary, string>
{
    public DailyOrderSummaryProjection()
    {
        Identity<OrderConfirmed>(e => DateOnly.FromDateTime(e.ConfirmedAt.UtcDateTime).ToString("yyyy-MM-dd"));
    }

    public void Apply(DailyOrderSummary summary, OrderConfirmed e)
    {
        summary.Date = DateOnly.FromDateTime(e.ConfirmedAt.UtcDateTime);
        summary.OrderCount++;
        summary.TotalAmount += e.Total;
    }
}
