using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomMates.Data;
using RoomMates.Models;
using Room_match_Lab.Data;

namespace RoomMates.Tests;

public class HousingOwnershipTests
{
    [Fact]
    public async Task MvcCreate_AssignsAuthenticatedProfileAndIgnoresSubmittedOwner()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var landlord = MakeProfile("owner@example.com", "KNU");
        var forgedLandlord = MakeProfile("forged@example.com", "KNU");
        db.Profiles.AddRange(landlord, forgedLandlord);
        await db.SaveChangesAsync();
        var controller = CreateMvcController(db, landlord);
        var housing = MakeHousing("MVC owner test");
        housing.LandlordId = forgedLandlord.Id;
        housing.Landlord = forgedLandlord;

        var result = await controller.Create(housing);

        result.Should().BeOfType<RedirectToActionResult>();
        var stored = await db.Housings.SingleAsync();
        stored.LandlordId.Should().Be(landlord.Id);
        stored.LandlordId.Should().NotBe(forgedLandlord.Id);
    }

    [Fact]
    public async Task MvcDetails_IncludesLandlordProfileForDisplay()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var landlord = MakeProfile("details-owner@example.com", "KNU");
        var housing = MakeHousing("Details owner test");
        housing.LandlordId = landlord.Id;
        housing.Landlord = landlord;
        db.AddRange(landlord, housing);
        await db.SaveChangesAsync();
        var controller = CreateMvcController(db, landlord);

        var result = await controller.Details(housing.Id);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<Housing>().Subject;
        model.Landlord.Id.Should().Be(landlord.Id);
        model.Landlord.Email.Should().Be(landlord.Email);
    }

    [Fact]
    public void Seed_IsIdempotentAndAssignsExistingOwnersToAllDemoHousings()
    {
        using var db = TestDbContextFactory.CreateInMemory();

        DbInitializer.Seed(db);
        DbInitializer.Seed(db);

        var profiles = db.Profiles.ToDictionary(profile => profile.Id);
        var housings = db.Housings.Include(housing => housing.Landlord).ToList();
        db.Profiles.Should().HaveCount(7);
        housings.Should().HaveCount(6);
        housings.Should().OnlyContain(housing => profiles.ContainsKey(housing.LandlordId));
        housings.Select(housing => housing.LandlordId).Distinct().Should().HaveCount(6);
        db.Tags.Should().HaveCount(5);
        db.Set<ProfileTag>().Select(link => new { link.ProfileId, link.TagId })
            .Should().OnlyHaveUniqueItems(link => $"{link.ProfileId}:{link.TagId}");
        db.Set<HousingTag>().Select(link => new { link.HousingId, link.TagId })
            .Should().OnlyHaveUniqueItems(link => $"{link.HousingId}:{link.TagId}");
    }

    private static HousingsMvcController CreateMvcController(RoomMatesDbContext db, Profile userProfile)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userProfile.Id.ToString()),
            new Claim("TenantId", userProfile.TenantId ?? "KNU")
        ], "Test");

        return new HousingsMvcController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static Profile MakeProfile(string email, string? tenantId) => new()
    {
        Id = Guid.NewGuid(),
        FullName = "Test Owner",
        Email = email,
        TenantId = tenantId,
        AvatarUrl = "https://example.com/avatar.png"
    };

    private static Housing MakeHousing(string title) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        TenantId = "KNU",
        City = "Kyiv",
        Street = "Example Street",
        BuildingNumber = "1",
        PricePerMonth = 500
    };
}
