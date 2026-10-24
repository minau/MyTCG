using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyTCG.Contracts;
using MyTCG.Infrastructure;
using Xunit;
namespace MyTCG.IntegrationTests;
public sealed class AuthTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Password = "MyTCG-SecurePassword42!";
    private static RegisterRequest Registration() { var id = Guid.NewGuid().ToString("N")[..16]; return new("Player_" + id, id + "@example.com", Password, Password); }
    private async Task<(HttpClient Client, RegisterRequest User, SessionResponse Session)> Session() {
        var c = fixture.Client(); var r = Registration(); Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/auth/register", r)).StatusCode);
        var response = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest(r.Username, Password)); response.EnsureSuccessStatusCode();
        return (c, r, (await response.Content.ReadFromJsonAsync<SessionResponse>())!);
    }
    [Fact] public async Task RegistrationStoresIdentityUserAndDefaultRole() {
        var c = fixture.Client(); var r = Registration(); var response = await c.PostAsJsonAsync("/api/auth/register", r);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); var user = (await response.Content.ReadFromJsonAsync<UserDto>())!;
        Assert.Equal(r.Username, user.Username);
        await using var scope = fixture.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Users.SingleAsync(x => x.Id == user.Id);
        Assert.NotEqual(Password, stored.PasswordHash); Assert.Equal(r.Email.ToUpperInvariant(), stored.NormalizedEmail); Assert.True(stored.IsActive);
        Assert.True(await db.UserRoles.AnyAsync(x => x.UserId == user.Id));
    }
    [Fact] public async Task DuplicateEmailCaseInsensitiveRejected() {
        var (c, r, _) = await Session(); var other = Registration() with { Email = r.Email.ToUpperInvariant() };
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/register", other)).StatusCode);
    }
    [Fact] public async Task DuplicateUsernameCaseInsensitiveRejected() {
        var (c, r, _) = await Session(); var other = Registration() with { Username = r.Username.ToUpperInvariant() };
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/register", other)).StatusCode);
    }
    [Theory] [InlineData("weak")] [InlineData("longbutnootherrequirements")]
    public async Task WeakPasswordRejected(string password) {
        var r = Registration() with { Password = password, ConfirmPassword = password };
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client().PostAsJsonAsync("/api/auth/register", r)).StatusCode);
    }
    [Fact] public async Task PasswordConfirmationRequired() {
        var r = Registration() with { ConfirmPassword = "different" };
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client().PostAsJsonAsync("/api/auth/register", r)).StatusCode);
    }
    [Fact] public async Task LoginWithEmailAndUsernameAndProtectedProfile() {
        var (c, r, s) = await Session(); Assert.NotNull(s.RefreshToken);
        var emailLogin = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest(r.Email, Password)); Assert.Equal(HttpStatusCode.OK, emailLogin.StatusCode);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.AccessToken);
        var me = await c.GetFromJsonAsync<UserDto>("/api/users/me"); Assert.Equal(r.Username, me!.Username); Assert.NotNull(me.LastLoginAt);
        var update = await c.PutAsJsonAsync("/api/users/me", new UpdateUserRequest(r.Username + "x", "new" + r.Email)); Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(r.Username + "x", (await c.GetFromJsonAsync<UserDto>("/api/users/me"))!.Username);
    }
    [Fact] public async Task InvalidCredentialsDoNotRevealAccountExistence() {
        var (c, r, _) = await Session();
        var existing = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest(r.Username, "WrongPassword!123"));
        var missing = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest("missing-user", "WrongPassword!123"));
        Assert.Equal(HttpStatusCode.Unauthorized, existing.StatusCode); Assert.Equal(existing.StatusCode, missing.StatusCode);
        Assert.Equal(await existing.Content.ReadAsStringAsync(), await missing.Content.ReadAsStringAsync());
    }
    [Fact] public async Task IdentityLockoutBlocksCorrectPasswordAfterFiveFailures() {
        var (c, r, _) = await Session();
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login", new LoginRequest(r.Username, "WrongPassword!123"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login", new LoginRequest(r.Username, Password))).StatusCode);
    }
    [Fact] public async Task ProtectedEndpointsRequireAuthentication() {
        var c = fixture.Client(); Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PutAsJsonAsync("/api/users/me", new UpdateUserRequest("Player", "p@example.com"))).StatusCode);
    }
    [Fact] public async Task RotationRejectsReplayAndRevokesWholeSession() {
        var (c, _, s) = await Session();
        var first = await c.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(s.RefreshToken)); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var rotated = (await first.Content.ReadFromJsonAsync<SessionResponse>())!; Assert.NotEqual(s.RefreshToken, rotated.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(s.RefreshToken))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(rotated.RefreshToken))).StatusCode);
        c.DefaultRequestHeaders.Authorization = new("Bearer", rotated.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/users/me")).StatusCode);
    }
    [Fact] public async Task ConcurrentRotationAllowsOnlyOneAndRevokesOnReplay() {
        var (c, _, s) = await Session();
        var results = await Task.WhenAll(c.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(s.RefreshToken)), c.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(s.RefreshToken)));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Unauthorized);
    }
    [Fact] public async Task LogoutImmediatelyRevokesAccessAndRefresh() {
        var (c, _, s) = await Session(); c.DefaultRequestHeaders.Authorization = new("Bearer", s.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(s.RefreshToken))).StatusCode);
    }
    [Fact] public async Task WebUsesHttpOnlySecureCookieAndChecksCsrf() {
        var c = fixture.Client(true); var r = Registration(); await c.PostAsJsonAsync("/api/auth/register", r);
        var response = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest(r.Username, Password)); response.EnsureSuccessStatusCode();
        var session = (await response.Content.ReadFromJsonAsync<SessionResponse>())!; Assert.Null(session.RefreshToken);
        var cookie = response.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains("httponly", cookie.ToLowerInvariant()); Assert.Contains("secure", cookie.ToLowerInvariant()); Assert.Contains("samesite=strict", cookie.ToLowerInvariant());
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(null))).StatusCode);
        c.DefaultRequestHeaders.Remove("Origin"); c.DefaultRequestHeaders.Add("Origin", "https://evil.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(null))).StatusCode);
    }
    [Fact] public async Task MobileModeNeverUsesWebCookie() {
        var c = fixture.Client(true); var r = Registration(); await c.PostAsJsonAsync("/api/auth/register", r);
        await c.PostAsJsonAsync("/api/auth/login", new LoginRequest(r.Username, Password));
        c.DefaultRequestHeaders.Remove("X-MyTCG-Client"); c.DefaultRequestHeaders.Add("X-MyTCG-Client", "mobile");
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(null))).StatusCode);
    }
    [Fact] public async Task RefreshSecretsAreHashedInDatabase() {
        var (_, _, s) = await Session(); await using var scope = fixture.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.RefreshTokens.AnyAsync(x => x.Hash == AccountService.Hash(s.RefreshToken!)));
        Assert.False(await db.RefreshTokens.AnyAsync(x => x.Hash == s.RefreshToken));
    }
    [Fact] public async Task ExpiredSessionAndInactiveUserCannotRefreshOrUseApi() {
        var (c, _, session) = await Session();
        await using var scope = fixture.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var token = await db.RefreshTokens.SingleAsync(x => x.Hash == AccountService.Hash(session.RefreshToken!));
        await db.Sessions.Where(x => x.Id == token.SessionId).ExecuteUpdateAsync(p => p.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
        c.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(session.RefreshToken))).StatusCode);
        var (other, _, active) = await Session();
        await db.Users.Where(x => x.Id == active.User.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsActive, false));
        other.DefaultRequestHeaders.Authorization = new("Bearer", active.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(active.RefreshToken))).StatusCode);
    }
    [Fact] public async Task InvalidJwtSignatureIsRejected() {
        var (c, _, session) = await Session();
        var parts = session.AccessToken.Split('.');
        parts[2] = (parts[2][0] == 'a' ? "b" : "a") + parts[2][1..];
        c.DefaultRequestHeaders.Authorization = new("Bearer", string.Join('.', parts));
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/users/me")).StatusCode);
    }
    [Fact] public async Task AuthenticationRateLimiterReturns429() {
        using var limited = fixture.App.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:PermitLimit"] = "1" })));
        using var client = limited.CreateClient(); client.DefaultRequestHeaders.Add("X-MyTCG-Client", "mobile");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("absent", "invalid"))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("absent", "invalid"))).StatusCode);
    }
    [Fact] public async Task DevelopmentExposesInteractiveDocsAndBearerContract() {
        var client = fixture.Client();
        var html = await client.GetStringAsync("/docs/index.html"); Assert.Contains("swagger-ui", html);
        using var contract = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        Assert.Equal("bearer", contract.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        Assert.True(contract.RootElement.GetProperty("paths").GetProperty("/api/users/me").GetProperty("get").GetProperty("security").GetArrayLength() > 0);
    }
    [Fact] public async Task ProductionHidesInteractiveDocsAndContract() {
        using var production = fixture.App.WithWebHostBuilder(builder => builder.UseSetting("environment", "Production"));
        using var client = production.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/docs/index.html")).StatusCode);
    }
    [Fact] public async Task HealthChecksRealDatabase() => Assert.Equal(HttpStatusCode.OK, (await fixture.Client().GetAsync("/health")).StatusCode);
}
