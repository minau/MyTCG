using System.Security.Claims;
using MyTCG.Application;
using MyTCG.Contracts;

namespace MyTCG.Api;
public static class TcgEndpoints
{
    private static Guid User(HttpContext c) => Guid.Parse(c.User.FindFirstValue("sub")!);
    public static void MapTcgEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/tcgs").RequireAuthorization();
        g.MapPost("/", (TcgRequest r, ITcgService s, HttpContext c) => s.Create(User(c), r));
        g.MapGet("/mine", (ITcgService s, HttpContext c) => s.Mine(User(c)));
        g.MapGet("/public", (string? search, ITcgService s, HttpContext c) => s.Public(User(c), search));
        g.MapPost("/join-by-code", (JoinCodeRequest r, ITcgService s, HttpContext c) => s.JoinCode(User(c), r.Code));
        g.MapGet("/{tcgId:guid}", (Guid tcgId, ITcgService s, HttpContext c) => s.Get(User(c), tcgId));
        g.MapPost("/{tcgId:guid}/join", (Guid tcgId, ITcgService s, HttpContext c) => s.Join(User(c), tcgId));
        g.MapGet("/{tcgId:guid}/invitation", async (Guid tcgId, ITcgService s, HttpContext c) => new { Code = await s.Invitation(User(c), tcgId, false) });
        g.MapPost("/{tcgId:guid}/invitation/regenerate", async (Guid tcgId, ITcgService s, HttpContext c) => new { Code = await s.Invitation(User(c), tcgId, true) });
        g.MapGet("/{tcgId:guid}/cards", (Guid tcgId, ITcgService s, HttpContext c) => s.Cards(User(c), tcgId));
        g.MapPost("/{tcgId:guid}/cards", (Guid tcgId, CardRequest r, ITcgService s, HttpContext c) => s.SaveCard(User(c), tcgId, null, r));
        g.MapPut("/{tcgId:guid}/cards/{cardId:guid}", (Guid tcgId, Guid cardId, CardRequest r, ITcgService s, HttpContext c) => s.SaveCard(User(c), tcgId, cardId, r));
        g.MapPatch("/{tcgId:guid}/cards/{cardId:guid}/status", (Guid tcgId, Guid cardId, StatusRequest r, ITcgService s, HttpContext c) => s.SetCardStatus(User(c), tcgId, cardId, r.IsActive));
        g.MapGet("/{tcgId:guid}/collection", (Guid tcgId, ITcgService s, HttpContext c) => s.Collection(User(c), tcgId));
        g.MapGet("/{tcgId:guid}/collection/progress", (Guid tcgId, ITcgService s, HttpContext c) => s.Progress(User(c), tcgId));
        g.MapGet("/{tcgId:guid}/boosters/status", (Guid tcgId, ITcgService s, HttpContext c) => s.BoosterStatus(User(c), tcgId));
        g.MapPost("/{tcgId:guid}/boosters/open", (Guid tcgId, OpenBoosterRequest r, ITcgService s, HttpContext c) => s.Open(User(c), tcgId, r.IdempotencyKey));
        g.MapGet("/{tcgId:guid}/boosters/history", (Guid tcgId, ITcgService s, HttpContext c) => s.History(User(c), tcgId));
        g.MapGet("/{tcgId:guid}/decks", (Guid tcgId, ITcgService s, HttpContext c) => s.Decks(User(c), tcgId));
        g.MapGet("/{tcgId:guid}/decks/{deckId:guid}", (Guid tcgId, Guid deckId, ITcgService s, HttpContext c) => s.GetDeck(User(c), tcgId, deckId));
        g.MapPost("/{tcgId:guid}/decks", (Guid tcgId, DeckRequest r, ITcgService s, HttpContext c) => s.SaveDeck(User(c), tcgId, null, r));
        g.MapPut("/{tcgId:guid}/decks/{deckId:guid}", (Guid tcgId, Guid deckId, DeckRequest r, ITcgService s, HttpContext c) => s.SaveDeck(User(c), tcgId, deckId, r));
        g.MapDelete("/{tcgId:guid}/decks/{deckId:guid}", (Guid tcgId, Guid deckId, ITcgService s, HttpContext c) => s.DeleteDeck(User(c), tcgId, deckId));
        g.MapPut("/{tcgId:guid}/decks/{deckId:guid}/primary", (Guid tcgId, Guid deckId, ITcgService s, HttpContext c) => s.Primary(User(c), tcgId, deckId));
        g.MapPost("/{tcgId:guid}/media", async (Guid tcgId, ITcgService s, IMediaStorage storage, HttpContext c, CancellationToken ct) =>
        {
            await s.AuthorizeAdmin(User(c), tcgId);
            var imageId = await storage.Save(tcgId, c.Request.Body, c.Request.ContentType ?? "", ct);
            return Results.Ok(new { ImageId = imageId });
        });
        g.MapGet("/{tcgId:guid}/media/{name}", async (Guid tcgId, string name, ITcgService s, IMediaStorage storage, HttpContext c, CancellationToken ct) =>
        {
            await s.Get(User(c), tcgId);
            var file = await storage.Read(tcgId, name, ct);
            c.Response.Headers.CacheControl = "private, no-store";
            return Results.File(file.Stream, file.ContentType);
        });
    }
}

