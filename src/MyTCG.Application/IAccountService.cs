using MyTCG.Contracts;
namespace MyTCG.Application;
public sealed class AccountException(string code, params string[] errors) : Exception(code)
{
    public string Code { get; } = code;
    public string[] Errors { get; } = errors;
}
public sealed record IssuedSession(SessionResponse Response, string RefreshToken, DateTimeOffset RefreshExpiresAt);
public interface IAccountService
{
    Task<UserDto> Register(RegisterRequest request);
    Task<IssuedSession> Login(LoginRequest request);
    Task<IssuedSession> Refresh(string token);
    Task Logout(Guid sessionId, Guid userId);
    Task<UserDto> GetUser(Guid userId);
    Task<UserDto> UpdateUser(Guid userId, UpdateUserRequest request);
    Task<bool> IsSessionValid(Guid sessionId, Guid userId);
}
