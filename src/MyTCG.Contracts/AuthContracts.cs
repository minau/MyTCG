using System.ComponentModel.DataAnnotations;
namespace MyTCG.Contracts;
public sealed record RegisterRequest(
    [property: Required, RegularExpression(@"^[a-zA-Z0-9_]{3,32}$", ErrorMessage = "Le username doit contenir 3 à 32 lettres, chiffres ou underscores.")] string Username,
    [property: Required, EmailAddress, MaxLength(254)] string Email,
    [property: Required, MinLength(12), MaxLength(128)] string Password,
    [property: Required] string ConfirmPassword);
public sealed record LoginRequest([property: Required, MaxLength(254)] string Login, [property: Required, MaxLength(128)] string Password);
public sealed record RefreshRequest(string? RefreshToken);
public sealed record UpdateUserRequest(
    [property: Required, RegularExpression(@"^[a-zA-Z0-9_]{3,32}$")] string Username,
    [property: Required, EmailAddress, MaxLength(254)] string Email);
public sealed record UserDto(Guid Id, string Username, string Email, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? LastLoginAt);
public sealed record SessionResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User, string? RefreshToken = null);
public sealed record ApiError(string Code, string[] Errors);
