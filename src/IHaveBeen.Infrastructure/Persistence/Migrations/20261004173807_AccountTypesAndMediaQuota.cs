using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IHaveBeen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountTypesAndMediaQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserType",
                table: "AspNetUsers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "user");

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_Type",
                table: "AspNetUsers",
                sql: "\"UserType\" IN ('user', 'manager')");

            migrationBuilder.Sql("""
                CREATE FUNCTION enforce_user_media_quota() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE owner_id text; account_type text; media_count bigint;
                BEGIN
                    SELECT "OwnerId" INTO owner_id FROM "TravelLogs" WHERE "Id" = NEW."TravelLogId";
                    -- Serialize quota decisions across all journals for this account.
                    -- Separate queries in this volatile trigger see commits after the lock wait.
                    SELECT "UserType" INTO account_type FROM "AspNetUsers" WHERE "Id" = owner_id FOR UPDATE;
                    IF account_type = 'user' THEN
                        SELECT count(*) INTO media_count FROM "MediaAssets" m
                        JOIN "TravelLogs" l ON l."Id" = m."TravelLogId"
                        WHERE l."OwnerId" = owner_id AND m."Id" <> NEW."Id";
                        IF media_count >= 100 THEN
                            RAISE check_violation USING MESSAGE = 'Account media quota exceeded',
                                CONSTRAINT = 'CK_MediaAssets_UserQuota';
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER user_media_quota BEFORE INSERT OR UPDATE OF "TravelLogId" ON "MediaAssets"
                FOR EACH ROW EXECUTE FUNCTION enforce_user_media_quota();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER user_media_quota ON \"MediaAssets\"; DROP FUNCTION enforce_user_media_quota();");
            migrationBuilder.DropCheckConstraint(
                name: "CK_User_Type",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UserType",
                table: "AspNetUsers");
        }
    }
}
