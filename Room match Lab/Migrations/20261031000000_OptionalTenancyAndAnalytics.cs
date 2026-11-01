using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Room_match_Lab.Migrations;

public partial class OptionalTenancyAndAnalytics : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "TenantId",
            table: "Housings",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100);

        migrationBuilder.AlterColumn<string>(
            name: "TenantId",
            table: "Profiles",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100);

        migrationBuilder.RenameColumn(
            name: "IsPremium",
            table: "Profiles",
            newName: "EnableDetailedAnalytics");

        migrationBuilder.AddColumn<bool>(
            name: "IsCrossUniversity",
            table: "Profiles",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsCrossUniversity", table: "Profiles");
        migrationBuilder.RenameColumn(
            name: "EnableDetailedAnalytics",
            table: "Profiles",
            newName: "IsPremium");

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
}
