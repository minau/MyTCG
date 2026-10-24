using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyTCG.Application;
using MyTCG.Contracts;
using MyTCG.Domain;

namespace MyTCG.Infrastructure;
public sealed class BoosterOpeningService(AppDbContext db, IOptions<BoosterOptions> options, TimeProvider clock)
{
    private DateTimeOffset Now => clock.GetUtcNow();

    private static TcgException Error(int status, string message) => new(status, "booster", message);
    private static CardDto CardDto(Card c) => TcgService.CardDto(c);
    private async Task Member(Guid user, Guid tcg)
    {
        if (!await db.Set<TcgMembership>().AnyAsync(x => x.TcgId == tcg && x.UserId == user))
            throw Error(403, "Adhésion requise.");
    }

    private async Task LockMember(Guid user, Guid tcg)
    {
        _ = await db.Set<TcgMembership>().FromSqlInterpolated($"SELECT * FROM \"TcgMembership\" WHERE \"TcgId\"={tcg} AND \"UserId\"={user} FOR UPDATE").SingleOrDefaultAsync() ?? throw Error(403, "Adhésion requise.");
    }

    private static DateTimeOffset Midnight(DateTimeOffset instant) => new(instant.UtcDateTime.Date, TimeSpan.Zero);
    public async Task<BoosterStatusDto> BoosterStatus(Guid user, Guid tcg)
    {
        await Member(user, tcg);
        var start = Midnight(Now);
        var count = await db.Set<BoosterOpening>().CountAsync(x => x.UserId == user && x.TcgId == tcg && x.OpenedAt >= start && x.OpenedAt < start.AddDays(1));
        return new(Math.Max(0, options.Value.DailyLimit - count), options.Value.DailyLimit, start.AddDays(1));
    }

    private async Task<OpeningDto> OpeningDto(BoosterOpening o)
    {
        var ids = o.Cards.Select(x => x.CardId).ToArray();
        var cards = await db.Set<Card>().Where(x => x.TcgId == o.TcgId && ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        return new(o.Id, o.OpenedAt, o.Cards.OrderBy(x => x.Position).Select(x => CardDto(cards[x.CardId])).ToArray());
    }

    public async Task<OpeningDto> Open(Guid user, Guid tcg, string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length is < 8 or > 128)
            throw Error(400, "Clé d’idempotence requise (8–128 caractères).");
        await using var tx = await db.Database.BeginTransactionAsync();
        await LockMember(user, tcg);
        var previous = await db.Set<BoosterOpening>().Include(x => x.Cards).SingleOrDefaultAsync(x => x.TcgId == tcg && x.UserId == user && x.IdempotencyKey == key);
        if (previous is not null)
            return await OpeningDto(previous);
        var openedAt = Now;
        var start = Midnight(openedAt);
        if (await db.Set<BoosterOpening>().CountAsync(x => x.UserId == user && x.TcgId == tcg && x.OpenedAt >= start && x.OpenedAt < start.AddDays(1)) >= options.Value.DailyLimit)
            throw Error(409, "Quota quotidien atteint.");
        var eligible = await db.Set<Card>().Where(x => x.TcgId == tcg && x.IsActive && x.DropWeight > 0).ToListAsync();
        if (eligible.Count == 0)
            throw Error(409, "Aucune carte active : aucun booster consommé.");
        var opening = new BoosterOpening
        {
            TcgId = tcg,
            UserId = user,
            OpenedAt = openedAt,
            IdempotencyKey = key
        };
        var drawn = Enumerable.Range(0, 5).Select(i => BoosterDrawing.Pick(eligible, i == 4 ? options.Value.FifthMultipliers : [1, 1, 1, 1, 1], BoosterDrawing.Unit())).ToArray();
        opening.Cards = drawn.Select((c, i) => new BoosterOpeningCard { BoosterOpeningId = opening.Id, TcgId = tcg, CardId = c.Id, Position = i }).ToList();
        db.Add(opening);
        foreach (var g in drawn.GroupBy(x => x.Id))
        {
            var owned = await db.Set<UserCard>().SingleOrDefaultAsync(x => x.TcgId == tcg && x.UserId == user && x.CardId == g.Key);
            if (owned is null)
                db.Add(new UserCard { TcgId = tcg, UserId = user, CardId = g.Key, Quantity = g.Count() });
            else
                owned.Quantity += g.Count();
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(opening.Id, opening.OpenedAt, drawn.Select(CardDto).ToArray());
    }

    public async Task<OpeningDto[]> History(Guid user, Guid tcg)
    {
        await Member(user, tcg);
        var list = await db.Set<BoosterOpening>().Include(x => x.Cards).Where(x => x.UserId == user && x.TcgId == tcg).OrderByDescending(x => x.OpenedAt).Take(100).ToListAsync();
        var result = new List<OpeningDto>();
        foreach (var o in list)
            result.Add(await OpeningDto(o));
        return result.ToArray();
    }
}
