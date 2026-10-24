using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyTCG.Domain;
namespace MyTCG.Infrastructure;
public sealed class AppUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<UserSession> Sessions => Set<UserSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        TcgModelConfiguration.Configure(b);
        b.Entity<AppUser>().Property(x => x.NormalizedEmail).IsRequired();
        b.Entity<AppUser>().HasIndex(x => x.NormalizedEmail).IsUnique();
        b.Entity<UserSession>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<RefreshToken>().HasIndex(x => x.Hash).IsUnique();
        b.Entity<RefreshToken>().Property(x => x.Hash).HasMaxLength(64);
        b.Entity<RefreshToken>().HasOne<UserSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<UserSession>().HasIndex(x => x.ExpiresAt);
    }
}
public sealed class JwtSettings
{
    public string Issuer { get; set; } = "MyTCG";
    public string Audience { get; set; } = "MyTCG.Client";
    public string Key { get; set; } = "";
    public int AccessMinutes { get; set; } = 10;
    public int SessionDays { get; set; } = 30;
    public void Validate()
    {
        if (System.Text.Encoding.UTF8.GetByteCount(Key) < 32 || AccessMinutes is < 1 or > 30 || SessionDays is < 1 or > 90)
            throw new InvalidOperationException("Configurer Jwt:Key (32 octets minimum), AccessMinutes (1–30) et SessionDays (1–90).");
    }
}
