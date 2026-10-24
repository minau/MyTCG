using MyTCG.Contracts;

namespace MyTCG.Application;
public sealed class TcgException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public interface ITcgService
{
    Task<TcgDto> Create(Guid user, TcgRequest request);
    Task<TcgDto[]> Mine(Guid user);
    Task<TcgDto[]> Public(Guid user, string? search);
    Task<TcgDto> Get(Guid user, Guid tcg);
    Task Join(Guid user, Guid tcg);
    Task JoinCode(Guid user, string code);
    Task<string> Invitation(Guid user, Guid tcg, bool regenerate);
    Task<CardDto[]> Cards(Guid user, Guid tcg);
    Task<CardDto> SaveCard(Guid user, Guid tcg, Guid? card, CardRequest request);
    Task SetCardStatus(Guid user, Guid tcg, Guid card, bool active);
    Task<CollectionItem[]> Collection(Guid user, Guid tcg);
    Task<CollectionProgressDto> Progress(Guid user, Guid tcg);
    Task<BoosterStatusDto> BoosterStatus(Guid user, Guid tcg);
    Task<OpeningDto> Open(Guid user, Guid tcg, string key);
    Task<OpeningDto[]> History(Guid user, Guid tcg);
    Task<DeckDto[]> Decks(Guid user, Guid tcg);
    Task<DeckDto> GetDeck(Guid user, Guid tcg, Guid deck);
    Task<DeckDto> SaveDeck(Guid user, Guid tcg, Guid? deck, DeckRequest request);
    Task DeleteDeck(Guid user, Guid tcg, Guid deck);
    Task Primary(Guid user, Guid tcg, Guid deck);
    Task AuthorizeAdmin(Guid user, Guid tcg);
}

public interface IMediaStorage
{
    Task<string> Save(Guid tcg, Stream stream, string contentType, CancellationToken cancellationToken);
    Task<(Stream Stream, string ContentType)> Read(Guid tcg, string name, CancellationToken cancellationToken);
}
