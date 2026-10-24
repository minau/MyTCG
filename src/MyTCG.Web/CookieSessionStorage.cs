using Microsoft.AspNetCore.Components.WebAssembly.Http;
using MyTCG.Client;
namespace MyTCG.Web;
public sealed class CookieSessionStorage : ISessionStorage
{
    public bool IsWeb => true;
    public Task<string?> ReadRefreshToken() => Task.FromResult<string?>(null);
    public Task WriteRefreshToken(string? token) => Task.CompletedTask;
}
public sealed class CookieHandler : DelegatingHandler
{
    public CookieHandler() : base(new HttpClientHandler()) { }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return base.SendAsync(request, cancellationToken);
    }
}
