using HotelOtaSync.Api.Auth;
using Microsoft.Extensions.Hosting;

namespace HotelOtaSync.UnitTests.Auth;

public class AdminOptionsValidatorTests
{
    private static AdminOptionsValidator Validator(string envName) =>
        new(new StubHostEnv(envName));

    [Fact]
    public void Validate_ProductionEnvWithDevSentinelToken_Fails()
    {
        var result = Validator(Environments.Production)
            .Validate(name: null, options: new AdminOptions { Token = "INSECURE-DEV-ONLY-FOO-1234567890" });

        Assert.True(result.Failed);
        Assert.Contains("INSECURE-DEV-ONLY", result.FailureMessage);
    }

    [Fact]
    public void Validate_DevelopmentEnvWithDevSentinelToken_Succeeds()
    {
        var result = Validator(Environments.Development)
            .Validate(name: null, options: new AdminOptions { Token = "INSECURE-DEV-ONLY-FOO-1234567890" });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_AnyEnvWithCustomToken_Succeeds()
    {
        var prod = Validator(Environments.Production)
            .Validate(name: null, options: new AdminOptions { Token = "real-rotated-token-please" });
        var dev = Validator(Environments.Development)
            .Validate(name: null, options: new AdminOptions { Token = "real-rotated-token-please" });

        Assert.True(prod.Succeeded);
        Assert.True(dev.Succeeded);
    }

    private sealed class StubHostEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
