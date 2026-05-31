using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Marten;
using Orders.Api.IntegrationEvents;
using Orders.Api.Ordering;
using Scalar.AspNetCore;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Http;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMarten(opts =>
{
    opts.Connection(builder.Configuration.GetConnectionString("marten")!);
    opts.Projections.Snapshot<Order>(SnapshotLifecycle.Inline);
})
.IntegrateWithWolverine()
.UseLightweightSessions()
.ApplyAllDatabaseChangesOnStartup()
.AddAsyncDaemon(DaemonMode.HotCold)
.PublishEventsToWolverine("ordering-events", relay =>
{
    relay.PublishEvent<OrderConfirmed>();
});

builder.Host.UseWolverine(opts =>
{
    // Explicitly set so tests can find handlers even when entry assembly differs
    opts.ApplicationAssembly = typeof(Program).Assembly;

    opts.Policies.AutoApplyTransactions();

    opts.PublishMessage<OrderConfirmed>().ToLocalQueue("ordering-side-effects");
    opts.PublishMessage<OrderPlaced>().ToLocalQueue("shipping").UseDurableInbox();

    opts.OnException<Exception>()
        .RetryWithCooldown(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(250));
});

builder.Services.AddWolverineHttp();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapWolverineEndpoints();

// Confirm uses a standard minimal API endpoint because Wolverine.Http 6.x returns 204
// for IResult-returning handlers when AutoApplyTransactions is active.
// The Marten async daemon relays OrderConfirmed to Wolverine regardless.
app.MapPost("/orders/{id}/confirm", async (Guid id, IDocumentSession session) =>
{
    var order = await session.Events.AggregateStreamAsync<Order>(id);
    if (order is null)
        return Results.NotFound();
    if (!order.HasItems)
        return Results.Problem("Cannot confirm an empty order.", statusCode: 400);
    var confirmedEvent = new OrderConfirmed(order.Id, order.CustomerId, order.Total);
    order.Apply(confirmedEvent);
    session.Events.Append(id, confirmedEvent);
    await session.SaveChangesAsync();
    return Results.Ok(order);
});

app.Run();

public partial class Program { }
