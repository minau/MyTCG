using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices;
using Microsoft.Extensions.DependencyInjection;
using MyTCG.Client;
namespace MyTCG.Mobile;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        builder.Services.AddSingleton<ISessionStorage, DeviceSessionStorage>();
        builder.Services.AddSingleton(_ => new HttpClient(new ApiEndpointHandler()) { BaseAddress = new Uri("https://api.mytcg.example.com/"), Timeout = TimeSpan.FromSeconds(20) });
        builder.Services.AddSingleton<AuthClient>(); builder.Services.AddSingleton<MainPage>();
        return builder.Build();
    }
}
public static class ApiConfiguration
{
    public static Uri BaseUri()
    {
        var configured = Preferences.Default.Get("api.baseUrl", "");
        if (Uri.TryCreate(configured, UriKind.Absolute, out var uri) && uri.Scheme == "https") return uri;
#if DEBUG
        if (uri is not null && uri.Scheme == "http") return uri;
        return new Uri(DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5080/" : "http://localhost:5080/");
#else
        return new Uri("https://api.mytcg.example.com/");
#endif
    }
}
public sealed class DeviceSessionStorage : ISessionStorage
{
    public bool IsWeb => false;
    public Task<string?> ReadRefreshToken() => SecureStorage.Default.GetAsync("mytcg.refresh");
    public async Task WriteRefreshToken(string? token)
    {
        if (token is null) SecureStorage.Default.Remove("mytcg.refresh");
        else await SecureStorage.Default.SetAsync("mytcg.refresh", token);
    }
}

public sealed class ApiEndpointHandler : DelegatingHandler
{
    public ApiEndpointHandler() : base(new HttpClientHandler()) { }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.RequestUri = new Uri(ApiConfiguration.BaseUri(), request.RequestUri!.PathAndQuery.TrimStart('/'));
        return base.SendAsync(request, cancellationToken);
    }
}
