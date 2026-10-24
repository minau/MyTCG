using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace MyTCG.Infrastructure.Migrations;
public partial class AutomaticCardNumbersAndUploadedImages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX "IX_Card_TcgId_CardNumber";
            WITH parsed AS (
                SELECT "Id", "TcgId", "CreatedAt",
                    CASE WHEN "CardNumber" ~ '^[0-9]{1,40}$'
                         THEN CASE WHEN "CardNumber"::numeric BETWEEN 1 AND 2147483647
                              THEN "CardNumber"::integer END END AS number
                FROM "Card"
            ), valid_sets AS (
                SELECT "TcgId", bool_and(number IS NOT NULL) AND count(DISTINCT number) = count(*) AS valid
                FROM parsed GROUP BY "TcgId"
            ), numbered AS (
                SELECT p."Id", CASE WHEN v.valid THEN p.number
                    ELSE row_number() OVER (PARTITION BY p."TcgId" ORDER BY p."CreatedAt", p."Id")::integer END AS number
                FROM parsed p JOIN valid_sets v USING ("TcgId")
            )
            UPDATE "Card" c SET "CardNumber" = n.number::text FROM numbered n WHERE c."Id" = n."Id";
            ALTER TABLE "Card" ALTER COLUMN "CardNumber" TYPE integer USING "CardNumber"::integer;
            CREATE UNIQUE INDEX "IX_Card_TcgId_CardNumber" ON "Card" ("TcgId", "CardNumber");
            ALTER TABLE "Card" ADD COLUMN "ImageId" uuid NULL;
            UPDATE "Card" SET "ImageId" = substring("ImageUrl" from '/media/([a-f0-9]{32})[.]png$')::uuid
            WHERE "ImageUrl" ~ ('^/api/tcgs/' || "TcgId"::text || '/media/[a-f0-9]{32}[.]png$');
            ALTER TABLE "Card" DROP COLUMN "ImageUrl";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Card" ADD COLUMN "ImageUrl" text NULL;
            UPDATE "Card" SET "ImageUrl" = '/api/tcgs/' || "TcgId"::text || '/media/' || replace("ImageId"::text, '-', '') || '.png'
            WHERE "ImageId" IS NOT NULL;
            ALTER TABLE "Card" DROP COLUMN "ImageId";
            ALTER TABLE "Card" ALTER COLUMN "CardNumber" TYPE text USING "CardNumber"::text;
            """);
    }
}
