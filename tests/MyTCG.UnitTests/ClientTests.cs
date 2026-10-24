using System.Net;
using System.Net.Http.Json;
using MyTCG.Client;
using MyTCG.Contracts;
using Xunit;
namespace MyTCG.UnitTests;
public sealed class ClientTests
{
    [Fact] public async Task MobileRestoresAndPersistsRotatedRefreshToken() {
        var storage = new Storage { Token = "old" };
        var handler = new Handler(async r => {
            Assert.Equal("mobile", r.Headers.GetValues("X-MyTCG-Client").Single());
            Assert.Equal("old", (await r.Content!.ReadFromJsonAsync<RefreshRequest>())!.RefreshToken);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(Session("rotated")) };
        });
        var client = new AuthClient(new HttpClient(handler) { BaseAddress = new("https://example.com") }, storage);
        await client.Initialize(); Assert.NotNull(client.User); Assert.Equal("rotated", storage.Token);
    }
    [Fact] public async Task RejectedSessionIsCleared() {
        var storage = new Storage { Token = "expired" };
        var client = new AuthClient(new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)))) { BaseAddress = new("https://example.com") }, storage);
        await client.Initialize(); Assert.Null(client.User); Assert.Null(storage.Token);
    }
    [Fact] public async Task NetworkFailureAllowsBootstrapRetry() {
        var count = 0; var storage = new Storage { Token = "existing" };
        var client = new AuthClient(new HttpClient(new Handler(_ => ++count == 1 ? throw new HttpRequestException() : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Session("new")) }))) { BaseAddress = new("https://example.com") }, storage);
        await Assert.ThrowsAsync<ClientException>(() => client.Initialize()); await client.Initialize(); Assert.NotNull(client.User);
    }
    private static SessionResponse Session(string refresh) => new("access", DateTimeOffset.UtcNow.AddMinutes(10), new(Guid.NewGuid(), "Player", "p@example.com", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null), refresh);
    private sealed class Storage : ISessionStorage {
        public string? Token; public bool IsWeb => false;
        public Task<string?> ReadRefreshToken() => Task.FromResult(Token);
        public Task WriteRefreshToken(string? token) { Token = token; return Task.CompletedTask; }
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> action) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => action(request);
    }
}
