using System.ComponentModel.DataAnnotations;

namespace MyTCG.Contracts;
public sealed record TcgRequest([property: Required, StringLength(100, MinimumLength = 1)] string Name, [property: StringLength(4000)] string Description, int Visibility, string? CoverImage = null);
public sealed record TcgDto(Guid Id, string Name, string Description, string? CoverImage, int Visibility, int Members, bool IsAdmin);
public sealed record JoinCodeRequest([property: Required, StringLength(128)] string Code);
public sealed record CardRequest(string Name, string Description, string CardNumber, int CardType, int Rarity, int ActionCost, int? Attack, int? Defense, double DropWeight, string? ImageUrl, bool IsActive = true);
public sealed record CardDto(Guid Id, string Name, string Description, string CardNumber, int CardType, int Rarity, int ActionCost, int? Attack, int? Defense, double DropWeight, string? ImageUrl, bool IsActive);
public sealed record CollectionItem(CardDto Card, int Quantity);
public sealed record CollectionProgressDto(int Discovered, int Copies, int Total);
public sealed record BoosterStatusDto(int Remaining, int DailyLimit, DateTimeOffset ResetsAt);
public sealed record OpenBoosterRequest([property: Required, StringLength(128, MinimumLength = 8)] string IdempotencyKey);
public sealed record OpeningDto(Guid Id, DateTimeOffset OpenedAt, CardDto[] Cards);
public sealed record DeckEntry(Guid CardId, int Quantity);
public sealed record DeckRequest(string Name, DeckEntry[] Cards);
public sealed record DeckDto(Guid Id, string Name, bool IsPrimary, bool IsValid, DeckEntry[] Cards);
public sealed record StatusRequest(bool IsActive);
