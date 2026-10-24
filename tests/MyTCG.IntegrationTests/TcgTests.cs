using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;
using MyTCG.Application;
using MyTCG.Contracts;
using MyTCG.Domain;
using MyTCG.Infrastructure;
using Xunit;

namespace MyTCG.IntegrationTests;
public sealed class TcgTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<HttpClient> Player()
    {
        var c = fixture.Client();
        var id = Guid.NewGuid().ToString("N");
        const string pw = "MyTCG-SecurePassword42!";
        var reg = await c.PostAsJsonAsync("/api/auth/register", new RegisterRequest("P_" + id[..20], id + "@example.com", pw, pw));
        reg.EnsureSuccessStatusCode();
        var login = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest(id + "@example.com", pw));
        login.EnsureSuccessStatusCode();
        var s = (await login.Content.ReadFromJsonAsync<SessionResponse>())!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.AccessToken);
        return c;
    }

    private static string P(TcgDto t, string suffix = "") => $"/api/tcgs/{t.Id}" + suffix;
    private static async Task<TcgDto> Create(HttpClient c, bool priv = false)
    {
        var r = await c.PostAsJsonAsync("/api/tcgs", new TcgRequest("Universe_" + Guid.NewGuid().ToString("N"), "Test", priv ? 1 : 0));
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<TcgDto>())!;
    }

    private static async Task<CardDto> Card(HttpClient c, TcgDto t, string number = "001", bool active = true)
    {
        var r = await c.PostAsJsonAsync(P(t, "/cards"), new CardRequest("Hero", "Description", number, 0, 0, 3, 4, 5, 100, null, active));
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<CardDto>())!;
    }

    private static async Task<OpeningDto> Open(HttpClient c, TcgDto t, string? key = null)
    {
        var r = await c.PostAsJsonAsync(P(t, "/boosters/open"), new OpenBoosterRequest(key ?? Guid.NewGuid().ToString()));
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<OpeningDto>())!;
    }

    [Fact]
    public async Task CreationAndPublicJoining()
    {
        using var owner = await Player();
        using var other = await Player();
        var t = await Create(owner);
        Assert.True(t.IsAdmin);
        Assert.Equal(1, t.Members);
        Assert.Contains((await owner.GetFromJsonAsync<TcgDto[]>("/api/tcgs/mine"))!, x => x.Id == t.Id);
        Assert.Equal(HttpStatusCode.OK, (await other.PostAsync(P(t, "/join"), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await other.PostAsync(P(t, "/join"), null)).StatusCode);
        Assert.False((await other.GetFromJsonAsync<TcgDto>(P(t)))!.IsAdmin);
    }

    [Fact]
    public async Task PrivateInvitationsAreHiddenProtectedAndRotatable()
    {
        using var owner = await Player();
        using var other = await Player();
        var t = await Create(owner, true);
        Assert.DoesNotContain((await other.GetFromJsonAsync<TcgDto[]>("/api/tcgs/public"))!, x => x.Id == t.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(P(t))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/tcgs/join-by-code", new JoinCodeRequest("invalid"))).StatusCode);
        var initial = (await owner.GetFromJsonAsync<CodeDto>(P(t, "/invitation")))!;
        var regenerated = await owner.PostAsync(P(t, "/invitation/regenerate"), null);
        regenerated.EnsureSuccessStatusCode();
        var code = (await regenerated.Content.ReadFromJsonAsync<CodeDto>())!;
        Assert.NotEqual(initial.Code, code.Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/tcgs/join-by-code", new JoinCodeRequest(initial.Code))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.PostAsJsonAsync("/api/tcgs/join-by-code", new JoinCodeRequest(code.Code))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(P(t, "/invitation"))).StatusCode);
    }

    [Fact]
    public async Task CardsAreAdminOnlyValidatedAndScoped()
    {
        using var owner = await Player();
        using var member = await Player();
        var a = await Create(owner);
        var b = await Create(owner);
        await member.PostAsync(P(a, "/join"), null);
        var card = await Card(owner, a);
        var r = new CardRequest("Hero", "", "001", 0, 0, 3, 1, 1, 1, null);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync(P(a, "/cards"), r)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(P(a, "/cards"), r with { ActionCost = 13 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsJsonAsync(P(b, $"/cards/{card.Id}"), r)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(P(a, "/cards"), r)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync(P(b, "/collection"))).StatusCode);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task EffectAndEquipmentTypesRoundTrip(int type)
    {
        using var c = await Player();
        var t = await Create(c);
        var request = new CardRequest("Carte", "Description", "001", type, 0, 3, null, null, 100, null);
        var created = await c.PostAsJsonAsync(P(t, "/cards"), request);
        created.EnsureSuccessStatusCode();
        var card = (await created.Content.ReadFromJsonAsync<CardDto>())!;
        Assert.Equal(type, card.CardType);
        var persisted = Assert.Single((await c.GetFromJsonAsync<CardDto[]>(P(t, "/cards")))!);
        Assert.Equal(type, persisted.CardType);
        Assert.Null(persisted.Attack);
        Assert.Null(persisted.Defense);
        var updated = await c.PutAsJsonAsync(P(t, $"/cards/{card.Id}"), request with { CardType = type == 1 ? 2 : 1 });
        updated.EnsureSuccessStatusCode();
        Assert.Equal(type == 1 ? 2 : 1, (await updated.Content.ReadFromJsonAsync<CardDto>())!.CardType);
    }

    [Fact]
    public async Task BoosterDuplicatesIdempotencyArchivalAndProgress()
    {
        using var c = await Player();
        var t = await Create(c);
        var a = await Card(c, t);
        await Card(c, t, "002", false);
        var key = Guid.NewGuid().ToString();
        var opening = await Open(c, t, key);
        Assert.Equal(5, opening.Cards.Length);
        Assert.All(opening.Cards, x => Assert.Equal(a.Id, x.Id));
        Assert.Equal(opening.Id, (await Open(c, t, key)).Id);
        var collection = (await c.GetFromJsonAsync<CollectionItem[]>(P(t, "/collection")))!;
        Assert.Equal(5, collection.Single(x => x.Card.Id == a.Id).Quantity);
        var progress = (await c.GetFromJsonAsync<CollectionProgressDto>(P(t, "/collection/progress")))!;
        Assert.Equal(new CollectionProgressDto(1, 5, 2), progress);
        await c.PatchAsJsonAsync(P(t, $"/cards/{a.Id}/status"), new StatusRequest(false));
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(P(t, "/boosters/open"), new OpenBoosterRequest(Guid.NewGuid().ToString()))).StatusCode);
        Assert.Equal(2, (await c.GetFromJsonAsync<BoosterStatusDto>(P(t, "/boosters/status")))!.Remaining);
        Assert.Equal(5, (await c.GetFromJsonAsync<CollectionItem[]>(P(t, "/collection")))!.Single(x => x.Card.Id == a.Id).Quantity);
    }

    [Fact]
    public async Task ConcurrentOpeningsRespectQuotaAndIdempotency()
    {
        using var c = await Player();
        var t = await Create(c);
        await Card(c, t);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => c.PostAsJsonAsync(P(t, "/boosters/open"), new OpenBoosterRequest(Guid.NewGuid().ToString()))));
        Assert.Equal(3, responses.Count(x => x.IsSuccessStatusCode));
        Assert.All(responses.Where(x => !x.IsSuccessStatusCode), x => Assert.Equal(HttpStatusCode.Conflict, x.StatusCode));
        Assert.Equal(15, (await c.GetFromJsonAsync<CollectionProgressDto>(P(t, "/collection/progress")))!.Copies);
        var b = await Create(c);
        await Card(c, b);
        var key = Guid.NewGuid().ToString();
        var same = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Open(c, b, key)));
        Assert.Single(same.Select(x => x.Id).Distinct());
        Assert.Equal(2, (await c.GetFromJsonAsync<BoosterStatusDto>(P(b, "/boosters/status")))!.Remaining);
    }

    [Fact]
    public async Task UnlimitedBoostersCanExceedQuotaAndLimitCanBeReenabled()
    {
        using var c = await Player();
        var t = await Create(c);
        await Card(c, t);
        var user = (await c.GetFromJsonAsync<UserDto>("/api/users/me"))!.Id;
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = new BoosterOptions { DailyLimitEnabled = false };
        var service = new BoosterOpeningService(db, Options.Create(settings), TimeProvider.System);
        var key = Guid.NewGuid().ToString();
        var first = await service.Open(user, t.Id, key);
        for (var i = 0; i < 4; i++)
            await service.Open(user, t.Id, Guid.NewGuid().ToString());
        Assert.True((await service.BoosterStatus(user, t.Id)).IsUnlimited);
        Assert.Equal(first.Id, (await service.Open(user, t.Id, key)).Id);
        Assert.Equal(25, (await c.GetFromJsonAsync<CollectionProgressDto>(P(t, "/collection/progress")))!.Copies);
        settings.DailyLimitEnabled = true;
        var status = await service.BoosterStatus(user, t.Id);
        Assert.False(status.IsUnlimited);
        Assert.Equal(0, status.Remaining);
        await Assert.ThrowsAsync<TcgException>(() => service.Open(user, t.Id, Guid.NewGuid().ToString()));
        Assert.Equal(first.Id, (await service.Open(user, t.Id, key)).Id);
    }

    [Fact]
    public async Task EmptyCatalogueConsumesNothing()
    {
        using var c = await Player();
        var t = await Create(c);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(P(t, "/boosters/open"), new OpenBoosterRequest(Guid.NewGuid().ToString()))).StatusCode);
        Assert.Equal(3, (await c.GetFromJsonAsync<BoosterStatusDto>(P(t, "/boosters/status")))!.Remaining);
    }

    [Fact]
    public async Task DeckOwnershipQuantitiesAndPrimary()
    {
        using var c = await Player();
        using var stranger = await Player();
        var t = await Create(c);
        var card = await Card(c, t);
        var bad = new DeckRequest("Deck", [new(card.Id, 1)]);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(P(t, "/decks"), bad)).StatusCode);
        await Open(c, t);
        var response = await c.PostAsJsonAsync(P(t, "/decks"), bad with { Cards = [new(card.Id, 5)] });
        response.EnsureSuccessStatusCode();
        var d = (await response.Content.ReadFromJsonAsync<DeckDto>())!;
        Assert.False(d.IsValid);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(P(t, $"/decks/{d.Id}"), bad with { Cards = [new(card.Id, 6)] })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(P(t, $"/decks/{d.Id}"), bad with { Cards = [new(card.Id, 17)] })).StatusCode);
        var b = await Create(c);
        var foreign = await Card(c, b);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(P(t, $"/decks/{d.Id}"), bad with { Cards = [new(foreign.Id, 1)] })).StatusCode);
        await stranger.PostAsync(P(t, "/join"), null);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync(P(t, $"/decks/{d.Id}"), bad)).StatusCode);
        var second = await c.PostAsJsonAsync(P(t, "/decks"), new DeckRequest("Second", []));
        second.EnsureSuccessStatusCode();
        var d2 = (await second.Content.ReadFromJsonAsync<DeckDto>())!;
        await Task.WhenAll(c.PutAsync(P(t, $"/decks/{d.Id}/primary"), null), c.PutAsync(P(t, $"/decks/{d2.Id}/primary"), null));
        Assert.Single((await c.GetFromJsonAsync<DeckDto[]>(P(t, "/decks")))!, x => x.IsPrimary);
    }

    [Fact]
    public async Task QuotaUsesUtcMidnightAndIsIndependent()
    {
        using var c = await Player();
        var t = await Create(c);
        await Card(c, t);
        await Open(c, t);
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = (await c.GetFromJsonAsync<UserDto>("/api/users/me"))!.Id;
        var instant = new DateTimeOffset(2030, 2, 1, 23, 59, 59, TimeSpan.Zero);
        await db.Set<BoosterOpening>().Where(x => x.TcgId == t.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.OpenedAt, instant));
        var clock = new Clock(instant);
        var service = new BoosterOpeningService(db, Options.Create(new BoosterOptions()), clock);
        Assert.Equal(2, (await service.BoosterStatus(user, t.Id)).Remaining);
        clock.Instant = instant.AddSeconds(1);
        Assert.Equal(3, (await service.BoosterStatus(user, t.Id)).Remaining);
        var b = await Create(c);
        Assert.Equal(3, (await c.GetFromJsonAsync<BoosterStatusDto>(P(b, "/boosters/status")))!.Remaining);
    }

    [Fact]
    public async Task FailedSaveRollsBackRewardAndQuota()
    {
        using var c = await Player();
        var t = await Create(c);
        await Card(c, t);
        var user = (await c.GetFromJsonAsync<UserDto>("/api/users/me"))!.Id;
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var original = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = original.Database.GetConnectionString();
        var settings = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).AddInterceptors(new FailSave()).Options;
        await using (var db = new AppDbContext(settings))
        {
            var service = new BoosterOpeningService(db, Options.Create(new BoosterOptions()), TimeProvider.System);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.Open(user, t.Id, Guid.NewGuid().ToString()));
        }

        Assert.Equal(3, (await c.GetFromJsonAsync<BoosterStatusDto>(P(t, "/boosters/status")))!.Remaining);
        Assert.Equal(0, (await c.GetFromJsonAsync<CollectionProgressDto>(P(t, "/collection/progress")))!.Copies);
    }

    [Fact]
    public async Task ImagesRejectFakeContentAndRequireAdmin()
    {
        using var c = await Player();
        using var other = await Player();
        var t = await Create(c);
        using var fake = new ByteArrayContent([1, 2, 3, 4]);
        fake.Headers.ContentType = new("image/png");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync(P(t, "/media"), fake)).StatusCode);
        using var another = new ByteArrayContent([1]);
        another.Headers.ContentType = new("image/png");
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync(P(t, "/media"), another)).StatusCode);
    }

    [Fact]
    public async Task SixteenCardsReadyAndCopiesReusableAcrossDecks()
    {
        using var c = await Player();
        var t = await Create(c);
        var card = await Card(c, t);
        await Open(c, t);
        await Open(c, t);
        await Open(c, t);
        await using (var scope = fixture.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Set<BoosterOpening>().Where(x => x.TcgId == t.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.OpenedAt, DateTimeOffset.UtcNow.AddDays(-1)));
        }

        await Open(c, t);
        var r = new DeckRequest("Ready", [new(card.Id, 16)]);
        var first = await c.PostAsJsonAsync(P(t, "/decks"), r);
        first.EnsureSuccessStatusCode();
        Assert.True((await first.Content.ReadFromJsonAsync<DeckDto>())!.IsValid);
        var second = await c.PostAsJsonAsync(P(t, "/decks"), r with { Name = "Also ready" });
        second.EnsureSuccessStatusCode();
        Assert.True((await second.Content.ReadFromJsonAsync<DeckDto>())!.IsValid);
        await c.PatchAsJsonAsync(P(t, $"/cards/{card.Id}/status"), new StatusRequest(false));
        Assert.All((await c.GetFromJsonAsync<DeckDto[]>(P(t, "/decks")))!, d => Assert.True(d.IsValid));
    }

    [Fact]
    public async Task ImageUploadDecodesReencodesAndRequiresMembershipForRead()
    {
        using var c = await Player();
        using var other = await Player();
        var t = await Create(c, true);
        using var bitmap = new SkiaSharp.SKBitmap(2, 2);
        bitmap.Erase(SkiaSharp.SKColors.Blue);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var body = new ByteArrayContent(png.ToArray());
        body.Headers.ContentType = new("image/png");
        var response = await c.PostAsync(P(t, "/media"), body);
        response.EnsureSuccessStatusCode();
        var url = (await response.Content.ReadFromJsonAsync<MediaDto>())!.Url;
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client().GetAsync(url)).StatusCode);
    }

    private sealed record MediaDto(string Url);
    private sealed record CodeDto(string Code);
    private sealed class Clock(DateTimeOffset instant) : TimeProvider
    {
        public DateTimeOffset Instant = instant;
        public override DateTimeOffset GetUtcNow() => Instant;
    }

    private sealed class FailSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData e, int result, CancellationToken ct = default) => throw new InvalidOperationException("Injected failure");
    }
}
