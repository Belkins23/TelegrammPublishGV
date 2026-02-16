using Serilog;
using TelegrammPublishGV;
using TelegrammPublishGV.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

builder.WebHost.UseUrls("http://0.0.0.0:5689");

builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.Configure<RedisSettings>(builder.Configuration.GetSection(RedisSettings.SectionName));
builder.Services.Configure<RabbitMQStatesOptions>(builder.Configuration.GetSection(RabbitMQStatesOptions.SectionName));
builder.Services.AddSingleton<IOrderStatePublisher, RabbitMQOrderStatePublisher>();
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RedisSettings>>().Value;
    return StackExchange.Redis.ConnectionMultiplexer.Connect(settings.ConnectionString);
});
builder.Services.AddSingleton<IOrderPayloadStore, RedisOrderPayloadStore>();
builder.Services.AddSingleton<TelegramCallbackProcessor>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<TelegramWebhookSetupService>();
builder.Services.AddHostedService<TelegramUpdatesHostedService>();
builder.Services.AddControllers()
    .AddNewtonsoftJson();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TelegrammPublishGV API",
        Version = "v1",
        Description = "Web API для публикации сообщений в Telegram-канал"
    });
});

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "TelegrammPublishGV API v1");
});

app.MapControllers();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast(
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
});

try
{
    Log.Information("Запуск приложения на http://0.0.0.0:5689");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Приложение завершилось с ошибкой");
}
finally
{
    Log.CloseAndFlush();
}

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
