using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Room_match_Lab.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleBasedAccessControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Profiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Student");

            migrationBuilder.Sql("UPDATE \"Profiles\" SET \"IsApproved\" = TRUE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Profiles");
        }
    }
}
