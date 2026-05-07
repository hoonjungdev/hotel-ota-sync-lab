using HotelOtaSync.Infrastructure.Channels.BlueWave;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HotelOtaSync.UnitTests.Channels.BlueWave;

/// Validates that AddBlueWaveChannel's option-validation rules trip at the
/// first IOptions resolve, so a misconfigured deployment fails fast at
/// startup rather than silently falling back to defaults at request time.
public class BlueWaveOptionsValidationTests
{
    private static IServiceProvider BuildSp(IDictionary<string, string?> settings)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddBlueWaveChannel(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void MissingBaseUrl_FailsValidation()
    {
        var sp = BuildSp(new Dictionary<string, string?>());

        var ex = Assert.Throws<OptionsValidationException>(() =>
            sp.GetRequiredService<IOptions<BlueWaveOptions>>().Value);

        Assert.Contains("BaseUrl", ex.Message);
    }

    [Fact]
    public void NonAbsoluteBaseUrl_FailsValidation()
    {
        // BindUriCore accepts the string but the resulting Uri.IsAbsoluteUri
        // is false, which the rule rejects.
        var sp = BuildSp(new Dictionary<string, string?>
        {
            ["Channels:BlueWave:BaseUrl"] = "/relative/path",
        });

        var ex = Assert.Throws<OptionsValidationException>(() =>
            sp.GetRequiredService<IOptions<BlueWaveOptions>>().Value);

        Assert.Contains("BaseUrl", ex.Message);
    }

    [Fact]
    public void ZeroPerAttemptTimeout_FailsValidation()
    {
        var sp = BuildSp(new Dictionary<string, string?>
        {
            ["Channels:BlueWave:BaseUrl"] = "http://blue.invalid/",
            ["Channels:BlueWave:PerAttemptTimeout"] = "00:00:00",
        });

        var ex = Assert.Throws<OptionsValidationException>(() =>
            sp.GetRequiredService<IOptions<BlueWaveOptions>>().Value);

        Assert.Contains("PerAttemptTimeout", ex.Message);
    }

    [Fact]
    public void TotalLessThanPerAttempt_FailsValidation()
    {
        var sp = BuildSp(new Dictionary<string, string?>
        {
            ["Channels:BlueWave:BaseUrl"] = "http://blue.invalid/",
            ["Channels:BlueWave:PerAttemptTimeout"] = "00:00:05",
            ["Channels:BlueWave:TotalRequestTimeout"] = "00:00:01",
        });

        var ex = Assert.Throws<OptionsValidationException>(() =>
            sp.GetRequiredService<IOptions<BlueWaveOptions>>().Value);

        Assert.Contains("TotalRequestTimeout", ex.Message);
    }

    [Fact]
    public void NegativeMaxRetryAttempts_FailsValidation()
    {
        var sp = BuildSp(new Dictionary<string, string?>
        {
            ["Channels:BlueWave:BaseUrl"] = "http://blue.invalid/",
            ["Channels:BlueWave:MaxRetryAttempts"] = "-1",
        });

        var ex = Assert.Throws<OptionsValidationException>(() =>
            sp.GetRequiredService<IOptions<BlueWaveOptions>>().Value);

        Assert.Contains("MaxRetryAttempts", ex.Message);
    }

    [Fact]
    public void ValidConfiguration_Resolves()
    {
        var sp = BuildSp(new Dictionary<string, string?>
        {
            ["Channels:BlueWave:BaseUrl"] = "http://blue.invalid/",
        });

        var opt = sp.GetRequiredService<IOptions<BlueWaveOptions>>().Value;

        Assert.NotNull(opt.BaseUrl);
        Assert.True(opt.BaseUrl!.IsAbsoluteUri);
        Assert.True(opt.PerAttemptTimeout > TimeSpan.Zero);
    }
}
