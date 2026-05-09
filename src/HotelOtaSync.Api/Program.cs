using HotelOtaSync.Api.Auth;
using HotelOtaSync.Api.Endpoints;
using HotelOtaSync.Application.Channels;
using HotelOtaSync.Infrastructure.Cache;
using HotelOtaSync.Infrastructure.Channels.BlueWave;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ---- options + validators ----
builder.Services.AddOptions<AdminOptions>()
    .Bind(builder.Configuration.GetSection(AdminOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AdminOptions>, AdminOptionsValidator>();

// ---- channel adapters + cache ----
builder.Services.AddBlueWaveChannel(builder.Configuration);
builder.Services.AddRedisRateCache(builder.Configuration);

// ---- application use cases ----
builder.Services.AddSingleton<ChannelRateRefresher>();   // stateless, safe as singleton
builder.Services.AddSingleton<GetCachedRatesQuery>();    // stateless

// ---- problem details + endpoint filter ----
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<AdminTokenFilter>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapHealthEndpoints();
app.MapRatesEndpoints();
app.MapAdminEndpoints();

app.Run();

// Note: do NOT add `public partial class Program {}` here — that would
// re-introduce the global-namespace ambiguity with MockOta.BlueWave.
// Integration tests use `HotelOtaSync.Api.ApiAssemblyMarker` instead.
