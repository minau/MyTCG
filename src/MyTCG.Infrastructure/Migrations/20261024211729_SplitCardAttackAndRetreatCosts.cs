using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace MyTCG.Infrastructure.Migrations;
public partial class SplitCardAttackAndRetreatCosts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "CK_Card_Cost", table: "Card");
        migrationBuilder.RenameColumn(name: "ActionCost", table: "Card", newName: "AttackCost");
        migrationBuilder.AddColumn<int>(name: "RetreatCost", table: "Card", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.Sql("""UPDATE "Card" SET "AttackCost" = LEAST("AttackCost", 10);""");
        migrationBuilder.AddCheckConstraint(name: "CK_Card_AttackCost", table: "Card", sql: "\"AttackCost\" BETWEEN 0 AND 10");
        migrationBuilder.AddCheckConstraint(name: "CK_Card_RetreatCost", table: "Card", sql: "\"RetreatCost\" BETWEEN 0 AND 10");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "CK_Card_AttackCost", table: "Card");
        migrationBuilder.DropCheckConstraint(name: "CK_Card_RetreatCost", table: "Card");
        migrationBuilder.DropColumn(name: "RetreatCost", table: "Card");
        migrationBuilder.Sql("""UPDATE "Card" SET "AttackCost" = GREATEST("AttackCost", 1);""");
        migrationBuilder.RenameColumn(name: "AttackCost", table: "Card", newName: "ActionCost");
        migrationBuilder.AddCheckConstraint(name: "CK_Card_Cost", table: "Card", sql: "\"ActionCost\" BETWEEN 1 AND 12");
    }
}
