using Alba;
using TUnit.Core.Interfaces;

namespace Orders.IntegrationTests;

public class AppFixture : IAsyncInitializer, IAsyncDisposable
{
    // Use the shared postgres instance in the multica network.
    // In CI/CD with Docker available, swap this for Testcontainers:
    //   new PostgreSqlBuilder("postgres:17-alpine").Build()
    private const string ConnectionString =
        "Host=postgres;Database=orders_test;Username=multica;Password=593abbf9e1842d8eac2a4e9eb054008986aaf8c2fd6f2303";

    public IAlbaHost Host { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Host = await AlbaHost.For<Program>(builder =>
        {
            builder.UseSetting("ConnectionStrings:marten", ConnectionString);
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (Host is not null)
            await Host.DisposeAsync();
    }
}
