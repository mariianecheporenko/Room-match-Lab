using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Room_match_Lab.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Housings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    District = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BuildingNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ApartmentNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    PricePerMonth = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PetPolicy = table.Column<string>(type: "text", nullable: false),
                    HasExistingPets = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsSmoking = table.Column<bool>(type: "boolean", nullable: false),
                    RequiredCleanliness = table.Column<int>(type: "integer", nullable: false),
                    RequiredSleepSchedule = table.Column<int>(type: "integer", nullable: false),
                    RequiredPartyTolerance = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Housings", x => x.Id);
                    table.CheckConstraint("CK_Housings_RequiredCleanliness", "\"RequiredCleanliness\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_Housings_RequiredPartyTolerance", "\"RequiredPartyTolerance\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_Housings_RequiredSleepSchedule", "\"RequiredSleepSchedule\" BETWEEN 1 AND 5");
                });

            migrationBuilder.CreateTable(
                name: "LifestyleCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ScaleMin = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ScaleMax = table.Column<int>(type: "integer", nullable: false, defaultValue: 5)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LifestyleCriteria", x => x.Id);
                    table.CheckConstraint("CK_LifestyleCriteria_Scale", "\"ScaleMin\" < \"ScaleMax\"");
                });

            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    AvatarUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Budget = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PetTolerance = table.Column<string>(type: "text", nullable: false),
                    OwnPets = table.Column<bool>(type: "boolean", nullable: false),
                    IsSmoker = table.Column<bool>(type: "boolean", nullable: false),
                    Cleanliness = table.Column<int>(type: "integer", nullable: false),
                    SleepSchedule = table.Column<int>(type: "integer", nullable: false),
                    PartyTolerance = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                    table.CheckConstraint("CK_Profiles_Cleanliness", "\"Cleanliness\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_Profiles_PartyTolerance", "\"PartyTolerance\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_Profiles_SleepSchedule", "\"SleepSchedule\" BETWEEN 1 AND 5");
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HousingCriterionRequirement",
                columns: table => new
                {
                    HousingId = table.Column<Guid>(type: "uuid", nullable: false),
                    LifestyleCriterionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetValue = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousingCriterionRequirement", x => new { x.HousingId, x.LifestyleCriterionId });
                    table.ForeignKey(
                        name: "FK_HousingCriterionRequirement_Housings_HousingId",
                        column: x => x.HousingId,
                        principalTable: "Housings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HousingCriterionRequirement_LifestyleCriteria_LifestyleCrit~",
                        column: x => x.LifestyleCriterionId,
                        principalTable: "LifestyleCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    HousingId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchScore = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingRequests_Housings_HousingId",
                        column: x => x.HousingId,
                        principalTable: "Housings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingRequests_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileCriterionValue",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    LifestyleCriterionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileCriterionValue", x => new { x.ProfileId, x.LifestyleCriterionId });
                    table.ForeignKey(
                        name: "FK_ProfileCriterionValue_LifestyleCriteria_LifestyleCriterionId",
                        column: x => x.LifestyleCriterionId,
                        principalTable: "LifestyleCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfileCriterionValue_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HousingTag",
                columns: table => new
                {
                    HousingId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousingTag", x => new { x.HousingId, x.TagId });
                    table.ForeignKey(
                        name: "FK_HousingTag_Housings_HousingId",
                        column: x => x.HousingId,
                        principalTable: "Housings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HousingTag_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileTag",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileTag", x => new { x.ProfileId, x.TagId });
                    table.ForeignKey(
                        name: "FK_ProfileTag_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfileTag_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingRequests_HousingId",
                table: "BookingRequests",
                column: "HousingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingRequests_ProfileId",
                table: "BookingRequests",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_HousingCriterionRequirement_LifestyleCriterionId",
                table: "HousingCriterionRequirement",
                column: "LifestyleCriterionId");

            migrationBuilder.CreateIndex(
                name: "IX_HousingTag_TagId",
                table: "HousingTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_LifestyleCriteria_Name",
                table: "LifestyleCriteria",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfileCriterionValue_LifestyleCriterionId",
                table: "ProfileCriterionValue",
                column: "LifestyleCriterionId");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_Email",
                table: "Profiles",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfileTag_TagId",
                table: "ProfileTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingRequests");

            migrationBuilder.DropTable(
                name: "HousingCriterionRequirement");

            migrationBuilder.DropTable(
                name: "HousingTag");

            migrationBuilder.DropTable(
                name: "ProfileCriterionValue");

            migrationBuilder.DropTable(
                name: "ProfileTag");

            migrationBuilder.DropTable(
                name: "Housings");

            migrationBuilder.DropTable(
                name: "LifestyleCriteria");

            migrationBuilder.DropTable(
                name: "Profiles");

            migrationBuilder.DropTable(
                name: "Tags");
        }
    }
}
