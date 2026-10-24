using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyTCG.Application;
using MyTCG.Contracts;
using MyTCG.Domain;

namespace MyTCG.Infrastructure;
public sealed class BoosterOptions
{
    public bool DailyLimitEnabled { get; set; } = true;
    public int DailyLimit { get; set; } = 3;
    public double[] FifthMultipliers { get; set; } = [1, 2, 4, 8, 16];
}

public static class BoosterDrawing
{
    public static Card Pick(IReadOnlyList<Card> cards, double[] multipliers, double unit)
    {
        if (cards.Count == 0 || unit is < 0 or >= 1)
            throw new ArgumentException("Tirage invalide.");
        var weights = cards.Select(c => c.DropWeight * multipliers[(int)c.Rarity]).ToArray();
        // Normalize first to keep finite totals even for large catalogues.
        var maximum = weights.Max();
        var total = weights.Sum(w => w / maximum);
        var target = unit * total;
        for (var i = 0; i < cards.Count; i++)
        {
            target -= weights[i] / maximum;
            if (target < 0)
                return cards[i];
        }

        return cards[^1];
    }

    public static double Unit() => RandomNumberGenerator.GetInt32(int.MaxValue) / (double)int.MaxValue;
}

public sealed class TcgService(AppDbContext db, IDataProtectionProvider protection, BoosterOpeningService boosters, TimeProvider clock, IMediaStorage media) : ITcgService
{
    private readonly IDataProtector protector = protection.CreateProtector("MyTCG.Invitation.v1");
    private static TcgException Error(int status, string message) => new(status, status == 403 ? "forbidden" : status == 404 ? "not_found" : "validation", message);
    private DateTimeOffset Now => clock.GetUtcNow();

    private async Task<TcgMembership> Member(Guid user, Guid tcg) => await db.Set<TcgMembership>().SingleOrDefaultAsync(x => x.TcgId == tcg && x.UserId == user) ?? throw Error(403, "Vous devez être membre de cet univers.");
    public async Task AuthorizeAdmin(Guid user, Guid tcg)
    {
        var m = await Member(user, tcg);
        if (m.Role != MembershipRole.Administrator || !await db.Set<Tcg>().AnyAsync(x => x.Id == tcg && x.CreatorUserId == user))
            throw Error(403, "Seul le créateur peut gérer cet univers.");
    }

    private async Task LockMember(Guid user, Guid tcg)
    {
        // PostgreSQL row lock is shared by all API replicas and released at transaction end.
        _ = await db.Set<TcgMembership>().FromSqlInterpolated($"SELECT * FROM \"TcgMembership\" WHERE \"TcgId\"={tcg} AND \"UserId\"={user} FOR UPDATE").SingleOrDefaultAsync() ?? throw Error(403, "Adhésion requise.");
    }

    private async Task<TcgDto> Dto(Tcg t, Guid user) => new(t.Id, t.Name, t.Description, t.CoverImage, (int)t.Visibility, await db.Set<TcgMembership>().CountAsync(x => x.TcgId == t.Id), t.CreatorUserId == user);
    public async Task<TcgDto> Create(Guid user, TcgRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 100 || r.Description is null || r.Description.Length > 4000 || !Enum.IsDefined((TcgVisibility)r.Visibility))
            throw Error(400, "Univers invalide.");
        ValidateMedia(r.CoverImage, null);
        var t = new Tcg
        {
            Name = r.Name.Trim(),
            Description = r.Description,
            Visibility = (TcgVisibility)r.Visibility,
            CreatorUserId = user,
            CoverImage = r.CoverImage,
            CreatedAt = Now
        };
        db.Add(t);
        db.Add(new TcgMembership { TcgId = t.Id, UserId = user, Role = MembershipRole.Administrator });
        if (t.Visibility == TcgVisibility.Private)
            AddInvitation(t.Id);
        await db.SaveChangesAsync();
        return await Dto(t, user);
    }

    public async Task<TcgDto[]> Mine(Guid user)
    {
        var ids = db.Set<TcgMembership>().Where(x => x.UserId == user).Select(x => x.TcgId);
        var list = await db.Set<Tcg>().Where(x => ids.Contains(x.Id)).OrderBy(x => x.Name).ToListAsync();
        var result = new List<TcgDto>();
        foreach (var t in list)
            result.Add(await Dto(t, user));
        return result.ToArray();
    }

    public async Task<TcgDto[]> Public(Guid user, string? search)
    {
        var q = db.Set<Tcg>().Where(x => x.Visibility == TcgVisibility.Public);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(x => x.Name.ToLower().Contains(search.ToLower()));
        var list = await q.OrderBy(x => x.Name).Take(100).ToListAsync();
        var result = new List<TcgDto>();
        foreach (var t in list)
            result.Add(await Dto(t, user));
        return result.ToArray();
    }

    public async Task<TcgDto> Get(Guid user, Guid tcg)
    {
        await Member(user, tcg);
        return await Dto(await db.Set<Tcg>().SingleAsync(x => x.Id == tcg), user);
    }

    private async Task AddMember(Guid user, Guid tcg)
    {
        if (await db.Set<TcgMembership>().AnyAsync(x => x.TcgId == tcg && x.UserId == user))
            throw Error(409, "Vous êtes déjà membre.");
        db.Add(new TcgMembership { TcgId = tcg, UserId = user, Role = MembershipRole.Member });
        await db.SaveChangesAsync();
    }

    public async Task Join(Guid user, Guid tcg)
    {
        if (!await db.Set<Tcg>().AnyAsync(x => x.Id == tcg && x.Visibility == TcgVisibility.Public))
            throw Error(404, "Univers public introuvable.");
        await AddMember(user, tcg);
    }

    private static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
    private string AddInvitation(Guid tcg)
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        db.Add(new TcgInvitation { TcgId = tcg, CodeHash = Hash(code), ProtectedCode = protector.Protect(code), CreatedAt = Now });
        return code;
    }

    public async Task JoinCode(Guid user, string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 128)
            throw Error(400, "Code invalide.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var hash = Hash(code.Trim().ToUpperInvariant());
        var invitation = await db.Set<TcgInvitation>().FromSqlInterpolated($"SELECT * FROM \"TcgInvitation\" WHERE \"CodeHash\"={hash} AND \"RevokedAt\" IS NULL FOR UPDATE").SingleOrDefaultAsync() ?? throw Error(400, "Code invalide ou révoqué.");
        await AddMember(user, invitation.TcgId);
        await tx.CommitAsync();
    }

    public async Task<string> Invitation(Guid user, Guid tcg, bool regenerate)
    {
        await AuthorizeAdmin(user, tcg);
        if (!await db.Set<Tcg>().AnyAsync(x => x.Id == tcg && x.Visibility == TcgVisibility.Private))
            throw Error(400, "Cet univers est public.");
        await using var tx = await db.Database.BeginTransactionAsync();
        await LockMember(user, tcg);
        var old = await db.Set<TcgInvitation>().SingleOrDefaultAsync(x => x.TcgId == tcg && x.RevokedAt == null);
        if (old is not null && !regenerate)
            return protector.Unprotect(old.ProtectedCode);
        if (old is not null)
        {
            old.RevokedAt = Now;
            await db.SaveChangesAsync();
        }

        var code = AddInvitation(tcg);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return code;
    }

    internal static CardDto CardDto(Card c) => new(c.Id, c.TcgId, c.Name, c.Description, c.CardNumber, (int)c.CardType, (int)c.Rarity, c.ActionCost, c.Attack, c.Defense, c.DropWeight, c.ImageId, c.IsActive);
    public async Task<CardDto[]> Cards(Guid user, Guid tcg)
    {
        await Member(user, tcg);
        return (await db.Set<Card>().Where(x => x.TcgId == tcg).OrderBy(x => x.CardNumber).ToListAsync()).Select(CardDto).ToArray();
    }

    public static void ValidateCard(CardRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 150 || r.Description is null || r.Description.Length > 8000 || !Enum.IsDefined((CardType)r.CardType) || !Enum.IsDefined((Rarity)r.Rarity) || r.ActionCost is < 1 or > 12 || r.Attack < 0 || r.Defense < 0 || (r.CardType == 0 && (r.Attack is null || r.Defense is null)) || !double.IsFinite(r.DropWeight) || r.DropWeight < 0 || r.DropWeight > 1e12 || (r.IsActive && r.DropWeight <= 0))
            throw Error(400, "Propriétés de carte invalides (coût 1–12, statistiques positives, poids actif strictement positif).");
    }

    private static void ValidateMedia(string? url, Guid? tcg)
    {
        if (string.IsNullOrEmpty(url))
            return;
        if (tcg is not null && url.StartsWith($"/api/tcgs/{tcg}/media/", StringComparison.Ordinal) && url.Length < 200 && !url.Contains("..") && !url.Contains('?'))
            return;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && url.Length <= 2000)
            return;
        throw Error(400, "L’image doit être une URL HTTPS ou un média de cet univers.");
    }

    public async Task<CardDto> SaveCard(Guid user, Guid tcg, Guid? id, CardRequest r)
    {
        await AuthorizeAdmin(user, tcg);
        ValidateCard(r);
        if (r.ImageId is { } imageId)
        {
            try
            {
                var file = await media.Read(tcg, imageId.ToString("N") + ".png", CancellationToken.None);
                await file.Stream.DisposeAsync();
            }
            catch (TcgException ex) when (ex.Status == 404)
            {
                throw Error(400, "Importez une illustration dans cet univers avant de l’utiliser.");
            }
        }
        await using var tx = await db.Database.BeginTransactionAsync();
        // Serialize number allocation per set across all API replicas.
        _ = await db.Set<Tcg>().FromSqlInterpolated($"SELECT * FROM \"Tcg\" WHERE \"Id\"={tcg} FOR UPDATE").SingleAsync();
        var c = id is null ? new Card
        {
            TcgId = tcg
        }

        : await db.Set<Card>().SingleOrDefaultAsync(x => x.Id == id && x.TcgId == tcg) ?? throw Error(404, "Carte introuvable.");
        if (id is null)
            c.CardNumber = checked((await db.Set<Card>().Where(x => x.TcgId == tcg).MaxAsync(x => (int?)x.CardNumber) ?? 0) + 1);
        c.Name = r.Name.Trim();
        c.Description = r.Description;
        c.CardType = (CardType)r.CardType;
        c.Rarity = (Rarity)r.Rarity;
        c.ActionCost = r.ActionCost;
        c.Attack = r.Attack;
        c.Defense = r.Defense;
        c.DropWeight = r.DropWeight;
        c.ImageId = r.ImageId;
        c.IsActive = r.IsActive;
        c.UpdatedAt = Now;
        if (id is null)
            db.Add(c);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return CardDto(c);
    }

    public async Task SetCardStatus(Guid user, Guid tcg, Guid id, bool active)
    {
        await AuthorizeAdmin(user, tcg);
        var c = await db.Set<Card>().SingleOrDefaultAsync(x => x.Id == id && x.TcgId == tcg) ?? throw Error(404, "Carte introuvable.");
        if (active && c.DropWeight <= 0)
            throw Error(400, "Poids positif requis.");
        c.IsActive = active;
        c.UpdatedAt = Now;
        await db.SaveChangesAsync();
    }

    public async Task<CollectionItem[]> Collection(Guid user, Guid tcg)
    {
        await Member(user, tcg);
        var owned = await db.Set<UserCard>().Where(x => x.UserId == user && x.TcgId == tcg).ToDictionaryAsync(x => x.CardId, x => x.Quantity);
        return (await db.Set<Card>().Where(x => x.TcgId == tcg).OrderBy(x => x.CardNumber).ToListAsync()).Select(c => new CollectionItem(CardDto(c), owned.GetValueOrDefault(c.Id))).ToArray();
    }

    public async Task<CollectionProgressDto> Progress(Guid user, Guid tcg)
    {
        var c = await Collection(user, tcg);
        return new(c.Count(x => x.Quantity > 0), c.Sum(x => x.Quantity), c.Length);
    }

    public Task<BoosterStatusDto> BoosterStatus(Guid user, Guid tcg) => boosters.BoosterStatus(user, tcg);
    public Task<OpeningDto> Open(Guid user, Guid tcg, string key) => boosters.Open(user, tcg, key);
    public Task<OpeningDto[]> History(Guid user, Guid tcg) => boosters.History(user, tcg);
    private static DeckDto DeckDto(Deck d) => new(d.Id, d.Name, d.IsPrimary, d.Cards.Sum(x => x.Quantity) == 16, d.Cards.Select(x => new DeckEntry(x.CardId, x.Quantity)).ToArray());
    private async Task<Deck> OwnedDeck(Guid user, Guid tcg, Guid id) => await db.Set<Deck>().Include(x => x.Cards).SingleOrDefaultAsync(x => x.Id == id && x.TcgId == tcg && x.UserId == user) ?? throw Error(404, "Deck introuvable.");
    public async Task<DeckDto[]> Decks(Guid user, Guid tcg)
    {
        await Member(user, tcg);
        return (await db.Set<Deck>().Include(x => x.Cards).Where(x => x.UserId == user && x.TcgId == tcg).OrderBy(x => x.Name).ToListAsync()).Select(DeckDto).ToArray();
    }

    public async Task<DeckDto> GetDeck(Guid user, Guid tcg, Guid id)
    {
        await Member(user, tcg);
        return DeckDto(await OwnedDeck(user, tcg, id));
    }

    public async Task<DeckDto> SaveDeck(Guid user, Guid tcg, Guid? id, DeckRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 100 || r.Cards is null || r.Cards.Length > 16 || r.Cards.Any(x => x.Quantity is < 1 or > 16) || r.Cards.Sum(x => x.Quantity) > 16 || r.Cards.Select(x => x.CardId).Distinct().Count() != r.Cards.Length)
            throw Error(400, "Deck invalide (16 cartes maximum).");
        await using var tx = await db.Database.BeginTransactionAsync();
        await LockMember(user, tcg);
        var d = id is null ? new Deck
        {
            TcgId = tcg,
            UserId = user
        }

        : await OwnedDeck(user, tcg, id.Value);
        var owned = await db.Set<UserCard>().Where(x => x.TcgId == tcg && x.UserId == user).ToDictionaryAsync(x => x.CardId, x => x.Quantity);
        if (r.Cards.Any(x => !owned.ContainsKey(x.CardId) || owned[x.CardId] < x.Quantity))
            throw Error(400, "Carte non possédée ou quantité indisponible dans cet univers.");
        d.Name = r.Name.Trim();
        d.UpdatedAt = Now;
        if (id is null)
            db.Add(d);
        foreach (var old in d.Cards.ToArray())
            if (!r.Cards.Any(x => x.CardId == old.CardId))
            {
                db.Remove(old);
                d.Cards.Remove(old);
            }

        foreach (var entry in r.Cards)
        {
            var old = d.Cards.SingleOrDefault(x => x.CardId == entry.CardId);
            if (old is not null)
                old.Quantity = entry.Quantity;
            else
                d.Cards.Add(new DeckCard { DeckId = d.Id, TcgId = tcg, CardId = entry.CardId, Quantity = entry.Quantity });
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return DeckDto(d);
    }

    public async Task DeleteDeck(Guid user, Guid tcg, Guid id)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await LockMember(user, tcg);
        db.Remove(await OwnedDeck(user, tcg, id));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task Primary(Guid user, Guid tcg, Guid id)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await LockMember(user, tcg);
        var d = await OwnedDeck(user, tcg, id);
        await db.Set<Deck>().Where(x => x.TcgId == tcg && x.UserId == user && x.IsPrimary).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsPrimary, false));
        d.IsPrimary = true;
        db.Entry(d).Property(x => x.IsPrimary).IsModified = true;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}

