using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Room_match_Lab.Migrations;

public partial class AddTenantAndPremiumFeatures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "TenantId",
            table: "Housings",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TenantId",
            table: "Profiles",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsPremium",
            table: "Profiles",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("UPDATE \"Housings\" SET \"TenantId\" = 'KNU' WHERE \"TenantId\" IS NULL;");
        migrationBuilder.Sql("UPDATE \"Profiles\" SET \"TenantId\" = 'KNU' WHERE \"TenantId\" IS NULL;");

        migrationBuilder.AlterColumn<string>(
            name: "TenantId",
            table: "Housings",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "TenantId",
            table: "Profiles",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "TenantId", table: "Housings");
        migrationBuilder.DropColumn(name: "IsPremium", table: "Profiles");
        migrationBuilder.DropColumn(name: "TenantId", table: "Profiles");
    }
}
