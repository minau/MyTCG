using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyTCG.Infrastructure;
using Testcontainers.PostgreSql;
using Xunit;
namespace MyTCG.IntegrationTests;
public sealed class ApiFixture : IAsyncLifetime
{
    private PostgreSqlContainer? database;
    public WebApplicationFactory<Program> App { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable("MYTCG_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connection)) {
            database = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await database.StartAsync(); connection = database.GetConnectionString();
        }
        App = new WebApplicationFactory<Program>().WithWebHostBuilder(b => {
            b.UseEnvironment("Development");
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string,string?> {
                ["ConnectionStrings:Database"] = connection,
                ["Jwt:Key"] = "IntegrationTestsOnly-NotForProduction-1234567890!",
                ["Cors:Origins:0"] = "https://web.mytcg.test", ["Auth:PermitLimit"] = "1000"
            }));
        });
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roles.RoleExistsAsync("User")) Assert.True((await roles.CreateAsync(new("User"))).Succeeded);
    }
    public HttpClient Client(bool web = false) {
        var client = App.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://api.mytcg.test"), HandleCookies = web });
        client.DefaultRequestHeaders.Add("X-MyTCG-Client", web ? "web" : "mobile");
        if (web) client.DefaultRequestHeaders.Add("Origin", "https://web.mytcg.test");
        return client;
    }
    public async Task DisposeAsync() { await App.DisposeAsync(); if (database is not null) await database.DisposeAsync(); }
}
