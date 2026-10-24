using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MyTCG.Contracts;
namespace MyTCG.Client;
public interface ISessionStorage
{
    bool IsWeb { get; }
    Task<string?> ReadRefreshToken();
    Task WriteRefreshToken(string? token);
}
public sealed class ClientException(string message) : Exception(message);
public sealed class AuthClient(HttpClient http, ISessionStorage storage)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private SessionResponse? session;
    private bool initialized;
    public UserDto? User => session?.User;
    public event Action? Changed;
    public async Task Reset() { await gate.WaitAsync(); try { await Set(null); initialized = false; } finally { gate.Release(); } }
    public async Task Initialize()
    {
        await gate.WaitAsync();
        try { if (!initialized) { await RefreshCore(); initialized = true; } }
        finally { gate.Release(); }
    }
    public async Task Register(RegisterRequest request) {
        using var response = await Send(HttpMethod.Post, "api/auth/register", request, false);
        await Ensure(response);
    }
    public async Task Login(LoginRequest request)
    {
        await gate.WaitAsync();
        try {
            using var response = await Send(HttpMethod.Post, "api/auth/login", request, false);
            await Ensure(response); await Set(await response.Content.ReadFromJsonAsync<SessionResponse>());
            initialized = true;
        } finally { gate.Release(); }
    }
    public async Task Logout()
    {
        await gate.WaitAsync();
        try {
            await EnsureFresh();
            using var response = await Send(HttpMethod.Post, "api/auth/logout", new { }, true);
            await Ensure(response); await Set(null);
        } finally { gate.Release(); }
    }
    public async Task<UserDto> GetMe()
    {
        await gate.WaitAsync();
        try {
            await EnsureFresh();
            using var response = await Send(HttpMethod.Get, "api/users/me", null, true);
            await Ensure(response); return (await response.Content.ReadFromJsonAsync<UserDto>())!;
        } finally { gate.Release(); }
    }
    public async Task UpdateMe(UpdateUserRequest request)
    {
        await gate.WaitAsync();
        try {
            await EnsureFresh();
            using var response = await Send(HttpMethod.Put, "api/users/me", request, true);
            await Ensure(response);
            session = session! with { User = (await response.Content.ReadFromJsonAsync<UserDto>())! }; Changed?.Invoke();
        } finally { gate.Release(); }
    }
    private async Task EnsureFresh() {
        if (session is null || session.ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(30)) await RefreshCore();
        if (session is null) throw new ClientException("Veuillez vous reconnecter.");
    }
    private async Task RefreshCore()
    {
        var token = await storage.ReadRefreshToken();
        if (!storage.IsWeb && token is null) { await Set(null); return; }
        using var response = await Send(HttpMethod.Post, "api/auth/refresh", new RefreshRequest(token), false);
        if (response.StatusCode == HttpStatusCode.Unauthorized) { await Set(null); return; }
        await Ensure(response); await Set(await response.Content.ReadFromJsonAsync<SessionResponse>());
    }
    private async Task Set(SessionResponse? value) {
        await storage.WriteRefreshToken(value?.RefreshToken);
        session = value; Changed?.Invoke();
    }
    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body, bool authorize)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-MyTCG-Client", storage.IsWeb ? "web" : "mobile");
        if (authorize && session is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        if (body is not null) request.Content = JsonContent.Create(body);
        try { return await http.SendAsync(request); }
        catch (HttpRequestException) { throw new ClientException("Connexion au serveur impossible. Vérifiez votre réseau et réessayez."); }
        catch (TaskCanceledException) { throw new ClientException("Le serveur met trop de temps à répondre. Réessayez."); }
    }
    private async Task Ensure(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode == HttpStatusCode.Unauthorized) await Set(null);
        if ((int)response.StatusCode == 429) throw new ClientException("Trop de tentatives. Veuillez patienter une minute.");
        ApiError? error = null;
        try { error = await response.Content.ReadFromJsonAsync<ApiError>(); } catch (System.Text.Json.JsonException) { }
        throw new ClientException(error?.Errors is { Length: > 0 } errors ? string.Join(" ", errors) : "La requête a échoué. Réessayez.");
    }
}
