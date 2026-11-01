using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Room_match_Lab.Migrations
{
    /// <inheritdoc />
    public partial class LinkHousingToLandlord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LandlordId",
                table: "Housings",
                type: "uuid",
                nullable: true);

            // Existing listings predate ownership. Associate them with a profile
            // in the same tenant where possible, then fall back to any profile.
            migrationBuilder.Sql(
                "UPDATE \"Housings\" AS housing " +
                "SET \"LandlordId\" = COALESCE(" +
                "(SELECT profile.\"Id\" FROM \"Profiles\" AS profile " +
                "WHERE profile.\"TenantId\" = housing.\"TenantId\" ORDER BY profile.\"Id\" LIMIT 1), " +
                "(SELECT profile.\"Id\" FROM \"Profiles\" AS profile ORDER BY profile.\"Id\" LIMIT 1))");

            migrationBuilder.AlterColumn<Guid>(
                name: "LandlordId",
                table: "Housings",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Housings_LandlordId",
                table: "Housings",
                column: "LandlordId");

            migrationBuilder.AddForeignKey(
                name: "FK_Housings_Profiles_LandlordId",
                table: "Housings",
                column: "LandlordId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Housings_Profiles_LandlordId",
                table: "Housings");

            migrationBuilder.DropIndex(
                name: "IX_Housings_LandlordId",
                table: "Housings");

            migrationBuilder.DropColumn(
                name: "LandlordId",
                table: "Housings");
        }
    }
}
