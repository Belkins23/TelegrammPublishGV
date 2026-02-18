using Prometheus;
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

// Метрика для Grafana: версия приложения, Redis и RabbitMQ (без паролей)
try
{
    var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";
    var redis = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RedisSettings>>().Value;
    var rabbit = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RabbitMQStatesOptions>>().Value;
    var redisHost = "?";
    var redisPort = "?";
    if (!string.IsNullOrEmpty(redis.ConnectionString))
    {
        var firstPart = redis.ConnectionString.Split(',')[0].Trim();
        var colon = firstPart.LastIndexOf(':');
        if (colon > 0) { redisHost = firstPart[..colon]; redisPort = firstPart[(colon + 1)..]; }
        else { redisHost = firstPart; }
    }
    var gauge = Prometheus.Metrics.CreateGauge(
        "telegramm_backend_info",
        "Version and backends (Redis, RabbitMQ) used by TelegrammPublishGV",
        new Prometheus.GaugeConfiguration { LabelNames = new[] { "version", "redis_host", "redis_port", "rabbitmq_host", "rabbitmq_port" } });
    gauge.WithLabels(version, redisHost, redisPort, rabbit.HostName ?? "?", rabbit.Port.ToString()).Set(1);
}
catch
{
    // метрика опциональна при ошибке конфигурации
}

app.UseSerilogRequestLogging();

app.UseHttpMetrics();
app.MapMetrics();

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
