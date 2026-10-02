using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Auth;

namespace TaskManager.IntegrationTests.Api;

/// <summary>
/// Hosts the real API pipeline (routing, auth, JSON, error handling) in memory.
/// By default the repositories are swapped for in-memory fakes, so no database is required.
/// </summary>
public class TaskManagerApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "integration-tests-signing-key-at-least-32-bytes";
    public const string Issuer = "TaskManager.Api";
    public const string Audience = "TaskManager.Client";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected virtual string? ConnectionString => null;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:TaskManager", ConnectionString ?? "Server=unused;Database=unused");
        builder.UseSetting("Database:InitializeOnStartup", "false");
        builder.UseSetting("Database:SeedDemoData", "false");
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Jwt:ExpirationMinutes", "60");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "10000");

        if (ConnectionString is null)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.RemoveAll<ITaskRepository>();
                services.AddSingleton<IUserRepository, InMemoryUserRepository>();
                services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
            });
        }
    }

    public async Task<(HttpClient Client, AuthResponse Auth)> CreateAuthenticatedClientAsync(string? username = null)
    {
        username ??= $"user{Guid.NewGuid():N}"[..20];
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(username, $"{username}@example.com", "Sup3rSecret!"), Json);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }
}
