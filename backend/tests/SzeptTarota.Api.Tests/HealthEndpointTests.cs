using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using SzeptTarota.Api.Tests.Infrastructure;

namespace SzeptTarota.Api.Tests;

[Collection(ApiCollection.Name)]
public class HealthEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_returns_ok_when_database_is_available()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("ok", body!.Status);
    }

    [Fact]
    public async Task Health_returns_service_unavailable_when_database_is_down()
    {
        await using var unreachableDatabase = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2");
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        });
        var client = unreachableDatabase.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("unavailable", body!.Status);
    }

    [Fact]
    public async Task Health_reports_check_time_from_the_application_clock()
    {
        var now = new DateTimeOffset(2026, 3, 1, 12, 30, 0, TimeSpan.Zero);
        factory.Time.SetUtcNow(now);
        var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<HealthResponse>("/api/health");

        Assert.Equal(now, body!.CheckedAt);
    }

    private sealed record HealthResponse(string Status, DateTimeOffset CheckedAt);
}
