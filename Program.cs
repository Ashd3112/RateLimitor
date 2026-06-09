var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var rateLimitSection = builder.Configuration.GetSection(RateLimiterApi.Models.RateLimitOptions.Position);
var rateLimitOptions = rateLimitSection.Get<RateLimiterApi.Models.RateLimitOptions>() ?? new RateLimiterApi.Models.RateLimitOptions();
builder.Services.Configure<RateLimiterApi.Models.RateLimitOptions>(rateLimitSection);

if (rateLimitOptions.StorageType.Equals("Redis", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDistributedMemoryCache(); // Fallback in-memory distributed cache (or AddStackExchangeRedisCache)
    builder.Services.AddSingleton<RateLimiterApi.Services.IRateLimitStore, RateLimiterApi.Services.DistributedRateLimitStore>();
}
else
{
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<RateLimiterApi.Services.IRateLimitStore, RateLimiterApi.Services.MemoryRateLimitStore>();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<RateLimiterApi.Middleware.RateLimitingMiddleware>();

app.MapGet("/api/default", () => Results.Ok(new { Message = "This endpoint uses the Default policy." }))
   .WithName("GetDefault");

app.MapGet("/", () => Results.Ok(new {
    Message = "Welcome to the Custom API Rate Limiter API!",
    DefaultPolicyEndpoint = "/api/default",
    PremiumPolicyEndpoint = "/api/premium"
})).WithName("Home");

app.MapGet("/api/premium", () => Results.Ok(new { Message = "Welcome, Premium user! This endpoint uses the Premium policy." }))
   .WithMetadata(new RateLimiterApi.Attributes.RateLimitAttribute("Premium"))
   .WithName("GetPremium");

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
