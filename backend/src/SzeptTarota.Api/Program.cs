using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SzeptTarota.Api.Cart;
using SzeptTarota.Api.Catalog;
using SzeptTarota.Api.Contact;
using SzeptTarota.Application;
using SzeptTarota.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var catalogDirectory = Path.Combine(builder.Environment.ContentRootPath, "Catalog");

builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProductCatalog(catalogDirectory);
builder.Services.AddContactRateLimiting(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

var app = builder.Build();

app.Services.ApplyMigrationsIfEnabled();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseRateLimiter();

var api = app.MapGroup("/api");

api.MapHealthChecks("/health", new HealthCheckOptions
{
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    },
    ResponseWriter = (context, report) => context.Response.WriteAsJsonAsync(new
    {
        status = report.Status == HealthStatus.Healthy ? "ok" : "unavailable",
        checkedAt = context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(),
    }),
})
    .WithName("Health");

api.MapProductEndpoints();
api.MapCartEndpoints();
api.MapContactEndpoints();

app.Run();

public partial class Program;
