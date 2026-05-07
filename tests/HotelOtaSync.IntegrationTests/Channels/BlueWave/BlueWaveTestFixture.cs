using Microsoft.AspNetCore.Mvc.Testing;
using MockOta.BlueWave.Inventory;

namespace HotelOtaSync.IntegrationTests.Channels.BlueWave;

/// xUnit collection fixture: spins up MockOta.BlueWave once per test class
/// and exposes its TestServer handler so each test can wire its own
/// BlueWaveClient on top (with custom retry/CB tuning, fault headers, etc).
///
/// We use InMemoryInventoryStore (public, MockOta.BlueWave-only) as the
/// TEntryPoint marker because both HotelOtaSync.Api and MockOta.BlueWave
/// declare a global `Program` partial — referencing `Program` directly would
/// be ambiguous in this test project.
public sealed class BlueWaveTestFixture : IDisposable
{
    public WebApplicationFactory<InMemoryInventoryStore> Factory { get; }

    public BlueWaveTestFixture()
    {
        Factory = new WebApplicationFactory<InMemoryInventoryStore>();
        // Force the host to materialise so .Server.CreateHandler() is ready.
        _ = Factory.CreateClient();
    }

    public HttpMessageHandler CreateHandler() => Factory.Server.CreateHandler();

    public void Dispose() => Factory.Dispose();
}
