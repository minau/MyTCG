using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyTCG.Application;
using MyTCG.Contracts;
using MyTCG.Domain;
namespace MyTCG.Infrastructure;
public sealed class AccountService(AppDbContext db, UserManager<AppUser> users, RoleManager<IdentityRole<Guid>> roles, JwtSettings jwt) : IAccountService
{
    public async Task<UserDto> Register(RegisterRequest r)
    {
        if (r.Password != r.ConfirmPassword) throw new AccountException("validation", "Les mots de passe ne correspondent pas.");
        var user = new AppUser { Id = Guid.NewGuid(), UserName = r.Username, Email = r.Email };
        await using var tx = await db.Database.BeginTransactionAsync();
        Check(await users.CreateAsync(user, r.Password));
        // Role is seeded by the dedicated migrator; no concurrent role creation here.
        if (!await roles.RoleExistsAsync("User")) throw new InvalidOperationException("Exécuter les migrations et initialiser les rôles.");
        Check(await users.AddToRoleAsync(user, "User"));
        await tx.CommitAsync();
        return Dto(user);
    }
    public async Task<IssuedSession> Login(LoginRequest r)
    {
        var u = await users.FindByNameAsync(r.Login) ?? await users.FindByEmailAsync(r.Login);
        if (u is null)
        {
            // Perform a real password hash to avoid a cheap missing-user path.
            _ = new PasswordHasher<AppUser>().HashPassword(new AppUser(), r.Password);
            throw InvalidLogin();
        }
        if (!u.IsActive || await users.IsLockedOutAsync(u)) throw InvalidLogin();
        if (!await users.CheckPasswordAsync(u, r.Password))
        {
            await users.AccessFailedAsync(u);
            throw InvalidLogin();
        }
        await users.ResetAccessFailedCountAsync(u);
        u.LastLoginAt = DateTimeOffset.UtcNow;
        Check(await users.UpdateAsync(u));
        var session = new UserSession { UserId = u.Id, ExpiresAt = DateTimeOffset.UtcNow.AddDays(jwt.SessionDays) };
        db.Sessions.Add(session);
        var result = await Issue(u, session);
        await db.SaveChangesAsync();
        return result;
    }
    public async Task<IssuedSession> Refresh(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 256) throw Unauthorized();
        var hash = Hash(token);
        var candidate = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(x => x.Hash == hash);
        if (candidate is null) throw Unauthorized();
        await using var tx = await db.Database.BeginTransactionAsync();
        // Serialize rotation/logout for the entire session, including concurrent refresh requests.
        var session = await db.Sessions.FromSqlInterpolated($"SELECT * FROM \"Sessions\" WHERE \"Id\" = {candidate.SessionId} FOR UPDATE").SingleAsync();
        var stored = await db.RefreshTokens.SingleAsync(x => x.Id == candidate.Id);
        var now = DateTimeOffset.UtcNow;
        if (stored.UsedAt is not null)
        {
            session.RevokedAt = now;
            await db.SaveChangesAsync(); await tx.CommitAsync();
            throw Unauthorized();
        }
        var u = await users.FindByIdAsync(session.UserId.ToString());
        if (session.RevokedAt is not null || session.ExpiresAt <= now || u is null || !u.IsActive) throw Unauthorized();
        stored.UsedAt = now;
        var result = await Issue(u, session);
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return result;
    }
    public async Task Logout(Guid sessionId, Guid userId)
    {
        await db.Sessions.Where(x => x.Id == sessionId && x.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow));
    }
    public async Task<bool> IsSessionValid(Guid sessionId, Guid userId) =>
        await db.Sessions.AnyAsync(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > DateTimeOffset.UtcNow)
        && await db.Users.AnyAsync(u => u.Id == userId && u.IsActive);
    public async Task<UserDto> GetUser(Guid userId) => Dto(await users.FindByIdAsync(userId.ToString()) ?? throw Unauthorized());
    public async Task<UserDto> UpdateUser(Guid userId, UpdateUserRequest r)
    {
        var u = await users.FindByIdAsync(userId.ToString()) ?? throw Unauthorized();
        u.UserName = r.Username; u.Email = r.Email; u.EmailConfirmed = false; u.UpdatedAt = DateTimeOffset.UtcNow;
        Check(await users.UpdateAsync(u));
        return Dto(u);
    }
    private async Task<IssuedSession> Issue(AppUser u, UserSession s)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        db.RefreshTokens.Add(new RefreshToken { SessionId = s.Id, Hash = Hash(raw) });
        var expires = DateTimeOffset.UtcNow.AddMinutes(jwt.AccessMinutes);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, u.Id.ToString()), new("sid", s.Id.ToString()), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), new("name", u.UserName!) };
        claims.AddRange((await users.GetRolesAsync(u)).Select(r => new Claim("role", r)));
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims, expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)), SecurityAlgorithms.HmacSha256));
        return new(new SessionResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, Dto(u)), raw, s.ExpiresAt);
    }
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static UserDto Dto(AppUser u) => new(u.Id, u.UserName!, u.Email!, u.CreatedAt, u.UpdatedAt, u.LastLoginAt);
    private static AccountException InvalidLogin() => new("unauthorized", "Identifiants invalides ou connexion temporairement indisponible.");
    private static AccountException Unauthorized() => new("unauthorized", "Session invalide ou expirée. Veuillez vous reconnecter.");
    private static void Check(IdentityResult r)
    {
        if (!r.Succeeded) throw new AccountException("validation", r.Errors.Select(e => e.Description).ToArray());
    }
}
