using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RoomMates.Data;

#nullable disable

namespace Room_match_Lab.Migrations;

[DbContext(typeof(RoomMatesDbContext))]
[Migration("20261104190000_AddHousingImageUrl")]
public partial class AddHousingImageUrl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ImageUrl",
            table: "Housings",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ImageUrl",
            table: "Housings");
    }
}
