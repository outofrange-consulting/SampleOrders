# SampleOrders

A sample ASP.NET Core project demonstrating idiomatic use of [Wolverine](https://wolverinefx.net), [Marten](https://martendb.io), [TUnit](https://tunit.dev), and [Alba](https://jasperfx.github.io/alba) together. The domain is a minimal order fulfillment workflow covering HTTP endpoints, domain events, and integration event publishing and consumption.

## What this demonstrates

| Pattern | Location |
|---|---|
| HTTP endpoints with Wolverine.Http | `src/Orders.Api/Ordering/OrderEndpoints.cs` |
| Event-sourced aggregate | `src/Orders.Api/Ordering/Order.cs` |
| Domain event → integration event bridge | `src/Orders.Api/Ordering/OrderConfirmedHandler.cs` |
| Integration event consumer (cross-context) | `src/Orders.Api/Shipping/ShipmentHandler.cs` |
| TUnit async unit tests | `tests/Orders.UnitTests/` |
| Alba integration tests for HTTP | `tests/Orders.IntegrationTests/` |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (10.0.300+)
- PostgreSQL

## Run the API

1. Update the `marten` connection string in `src/Orders.Api/appsettings.json` to point at your PostgreSQL instance.
2. From the solution root:

```bash
dotnet run --project src/Orders.Api
```

Marten applies schema migrations automatically on startup. The OpenAPI UI is available at `http://localhost:5180/scalar/v1`.

## Run the tests

**Unit tests** — no database required:

```bash
dotnet test tests/Orders.UnitTests
```

**Integration tests** — require PostgreSQL:

`AppFixture.cs` connects to `Host=postgres;Database=orders_test`. Update the connection string in that file to point at your instance, then:

```bash
dotnet test tests/Orders.IntegrationTests
```

For Docker-based CI, the comment in `AppFixture.cs` shows how to swap in Testcontainers instead.

## Project structure

```
SampleOrders/
├── src/
│   └── Orders.Api/
│       ├── Program.cs                   # Startup: Marten, Wolverine, OpenAPI
│       ├── IntegrationEvents/
│       │   └── OrderPlaced.cs           # Cross-context event contract
│       ├── Ordering/
│       │   ├── Order.cs                 # Event-sourced aggregate + domain events
│       │   ├── OrderEndpoints.cs        # Wolverine.Http endpoints
│       │   └── OrderConfirmedHandler.cs # Domain → integration event bridge
│       └── Shipping/
│           ├── Shipment.cs
│           └── ShipmentHandler.cs       # Integration event consumer
└── tests/
    ├── Orders.UnitTests/
    │   └── OrderAggregateTests.cs       # 6 tests: aggregate state, totals, parameterized
    └── Orders.IntegrationTests/
        ├── AppFixture.cs                # IAlbaHost factory (shared per test session)
        ├── OrderEndpointTests.cs        # 4 tests: HTTP round-trips, status codes
        └── IntegrationEventFlowTests.cs # 1 test: full async event chain
```

## Patterns covered

### HTTP endpoints (Wolverine.Http)

Endpoints are static methods on a plain class annotated with `[WolverinePost]` or `[WolverineGet]` — no controller base class required. Wolverine generates the actual route handler at build time.

```csharp
[WolverinePost("/orders")]
public static (CreationResponse, IStartStream) Create(CreateOrder cmd) { ... }
```

Returning a tuple of `(CreationResponse, IStartStream)` lets Wolverine emit the 201 response and start a Marten event stream in the same transaction. `[EmptyResponse]` signals a 204 response. `[Aggregate]` and `[Document]` method parameters are resolved from Marten automatically.

### Domain events

`Order` is an event-sourced aggregate. Each `Apply` overload advances state from one event type:

```
OrderCreated → OrderItemAdded (×n) → OrderConfirmed
```

Marten persists events to the stream and rebuilds an `Order` snapshot inline on every write. The Marten async daemon watches the stream and relays `OrderConfirmed` to Wolverine's message bus.

### Integration events

`OrderConfirmedHandler.Handle(OrderConfirmed)` translates the internal domain event into the cross-context `OrderPlaced` contract. Wolverine routes `OrderPlaced` to `ShipmentHandler` via a durable local queue, keeping the Ordering and Shipping vocabularies separate.

Full flow: `POST /orders/{id}/confirm` → `OrderConfirmed` (domain event) → `OrderPlaced` (integration event) → `Shipment` document created.

## Idiomatic choices

| Library | Choice | Reason |
|---|---|---|
| Wolverine.Http | Return tuples with side-effect types | Wolverine composes transactional side effects declaratively; no imperative saves |
| Wolverine | `AutoApplyTransactions()` policy | Every handler runs inside a Marten unit of work automatically |
| Marten | `SnapshotLifecycle.Inline` on `Order` | Order reads use the snapshot table; no stream replay on every GET |
| Marten | `ApplyAllDatabaseChangesOnStartup()` | Schema migrations run at startup; no separate migration step needed |
| Marten | `UseLightweightSessions()` | Lightweight sessions skip the identity map; appropriate for a write-oriented service |
| TUnit | `await Assert.That(...)` | TUnit's assertion API is fully async; `await` is required, not optional |
| TUnit | `[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]` | One `IAlbaHost` starts per test session, not per test class |
| Alba | `Scenario(s => ...)` | Composes the request, status assertion, and response read in one fluent block |
| Confirm endpoint | Standard `app.MapPost`, not `[WolverinePost]` | Wolverine.Http 6.x returns 204 for `Task<IResult>` handlers under `AutoApplyTransactions`; a standard minimal API endpoint gives the correct 200 + body while the Marten daemon still relays the event |
