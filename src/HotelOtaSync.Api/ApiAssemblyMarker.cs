namespace HotelOtaSync.Api;

/// Public type used as the WebApplicationFactory&lt;T&gt; generic parameter in
/// integration tests. We can't use Program directly: top-level statements
/// place Program in the global namespace, and MockOta.BlueWave already
/// declares `public partial class Program;` there too — referring to
/// `Program` from the IntegrationTests project (which references both
/// hosts) would be ambiguous. This marker lives in HotelOtaSync.Api's
/// own namespace and is found by WebApplicationFactory's assembly probing.
public sealed class ApiAssemblyMarker;
