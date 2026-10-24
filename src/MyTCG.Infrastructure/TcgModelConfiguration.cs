using Microsoft.EntityFrameworkCore;
using MyTCG.Domain;

namespace MyTCG.Infrastructure;
internal static class TcgModelConfiguration
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<Tcg>().HasKey(x => x.Id);
        b.Entity<Tcg>().Property(x => x.Name).HasMaxLength(100);
        b.Entity<Tcg>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatorUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TcgMembership>().HasKey(x => new { x.TcgId, x.UserId });
        b.Entity<TcgMembership>().HasOne<Tcg>().WithMany().HasForeignKey(x => x.TcgId);
        b.Entity<TcgMembership>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId);
        b.Entity<TcgInvitation>().HasKey(x => x.Id);
        b.Entity<TcgInvitation>().HasIndex(x => x.CodeHash).IsUnique();
        b.Entity<TcgInvitation>().HasIndex(x => x.TcgId).IsUnique().HasFilter("\"RevokedAt\" IS NULL");
        b.Entity<TcgInvitation>().HasOne<Tcg>().WithMany().HasForeignKey(x => x.TcgId);
        b.Entity<Card>().HasKey(x => x.Id);
        b.Entity<Card>().HasAlternateKey(x => new { x.TcgId, x.Id });
        b.Entity<Card>().HasIndex(x => new { x.TcgId, x.CardNumber }).IsUnique();
        b.Entity<Card>().HasOne<Tcg>().WithMany().HasForeignKey(x => x.TcgId);
        b.Entity<Card>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Card_Cost", "\"ActionCost\" BETWEEN 1 AND 12");
            t.HasCheckConstraint("CK_Card_Stats", "(\"Attack\" IS NULL OR \"Attack\" >= 0) AND (\"Defense\" IS NULL OR \"Defense\" >= 0)");
        });
        b.Entity<UserCard>().HasKey(x => new { x.TcgId, x.UserId, x.CardId });
        b.Entity<UserCard>().HasOne<TcgMembership>().WithMany().HasForeignKey(x => new { x.TcgId, x.UserId });
        b.Entity<UserCard>().HasOne<Card>().WithMany().HasForeignKey(x => new { x.TcgId, x.CardId }).HasPrincipalKey(x => new { x.TcgId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<UserCard>().ToTable(t => t.HasCheckConstraint("CK_UserCard_Quantity", "\"Quantity\" > 0"));
        b.Entity<BoosterOpening>().HasKey(x => x.Id);
        b.Entity<BoosterOpening>().HasAlternateKey(x => new { x.TcgId, x.Id });
        b.Entity<BoosterOpening>().HasIndex(x => new { x.TcgId, x.UserId, x.IdempotencyKey }).IsUnique();
        b.Entity<BoosterOpening>().HasIndex(x => new { x.TcgId, x.UserId, x.OpenedAt });
        b.Entity<BoosterOpening>().HasOne<TcgMembership>().WithMany().HasForeignKey(x => new { x.TcgId, x.UserId });
        b.Entity<BoosterOpening>().HasMany(x => x.Cards).WithOne().HasForeignKey(x => new { x.TcgId, x.BoosterOpeningId }).HasPrincipalKey(x => new { x.TcgId, x.Id });
        b.Entity<BoosterOpeningCard>().HasKey(x => new { x.BoosterOpeningId, x.Position });
        b.Entity<BoosterOpeningCard>().HasOne<Card>().WithMany().HasForeignKey(x => new { x.TcgId, x.CardId }).HasPrincipalKey(x => new { x.TcgId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Deck>().HasKey(x => x.Id);
        b.Entity<Deck>().HasAlternateKey(x => new { x.TcgId, x.Id });
        b.Entity<Deck>().HasOne<TcgMembership>().WithMany().HasForeignKey(x => new { x.TcgId, x.UserId });
        b.Entity<Deck>().HasIndex(x => new { x.TcgId, x.UserId }).IsUnique().HasFilter("\"IsPrimary\" = TRUE");
        b.Entity<Deck>().HasMany(x => x.Cards).WithOne().HasForeignKey(x => new { x.TcgId, x.DeckId }).HasPrincipalKey(x => new { x.TcgId, x.Id });
        b.Entity<DeckCard>().HasKey(x => new { x.DeckId, x.CardId });
        b.Entity<DeckCard>().HasOne<Card>().WithMany().HasForeignKey(x => new { x.TcgId, x.CardId }).HasPrincipalKey(x => new { x.TcgId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<DeckCard>().ToTable(t => t.HasCheckConstraint("CK_DeckCard_Quantity", "\"Quantity\" > 0"));
    }
}
