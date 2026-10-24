namespace MyTCG.Domain;
public enum TcgVisibility
{
    Public,
    Private
}

public enum MembershipRole
{
    Member,
    Administrator,
    Moderator
}

public enum CardType
{
    Character = 0,
    Effect = 1,
    Equipment = 2
}

public enum Rarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public sealed class Tcg
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? CoverImage { get; set; }
    public TcgVisibility Visibility { get; set; }
    public Guid CreatorUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TcgMembership
{
    public Guid TcgId { get; set; }
    public Guid UserId { get; set; }
    public MembershipRole Role { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TcgInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TcgId { get; set; }
    public string CodeHash { get; set; } = "";
    public string ProtectedCode { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class Card
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TcgId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string CardNumber { get; set; } = "";
    public CardType CardType { get; set; }
    public Rarity Rarity { get; set; }
    public int ActionCost { get; set; }
    public int? Attack { get; set; }
    public int? Defense { get; set; }
    public double DropWeight { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class UserCard
{
    public Guid TcgId { get; set; }
    public Guid UserId { get; set; }
    public Guid CardId { get; set; }
    public int Quantity { get; set; }
}

public sealed class BoosterOpening
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TcgId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public List<BoosterOpeningCard> Cards { get; set; } = [];
}

public sealed class BoosterOpeningCard
{
    public Guid BoosterOpeningId { get; set; }
    public Guid TcgId { get; set; }
    public Guid CardId { get; set; }
    public int Position { get; set; }
}

public sealed class Deck
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TcgId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public bool IsPrimary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<DeckCard> Cards { get; set; } = [];
}

public sealed class DeckCard
{
    public Guid DeckId { get; set; }
    public Guid TcgId { get; set; }
    public Guid CardId { get; set; }
    public int Quantity { get; set; }
}
