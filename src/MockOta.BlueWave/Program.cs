using MockOta.BlueWave.FaultInjection;
using MockOta.BlueWave.Inventory;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddXmlSerializerFormatters();

builder.Services.Configure<FaultInjectionOptions>(
    builder.Configuration.GetSection("FaultInjection"));
builder.Services.AddSingleton<IInventoryStore, InMemoryInventoryStore>();
builder.Services.AddSingleton<IReservationLog, InMemoryReservationLog>();

var app = builder.Build();

app.UseMiddleware<FaultInjectionMiddleware>();
app.MapControllers();
app.MapGet("/", () => "MockOta.BlueWave (OpenTravel-style XML mock)");

app.Run();

public partial class Program;
