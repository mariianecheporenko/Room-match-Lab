using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using RoomMates.Controllers;
using RoomMates.Data;
using RoomMates.Models;

namespace RoomMates.Tests;

public sealed class AuthAndRbacTests
{
    [Fact]
    public async Task GetHousings_AnonymousUser_ReturnsOkResult()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new HousingsController(db, cache)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.GetHousings(cancellationToken: CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateHousing_UnapprovedLandlord_ReturnsForbidden()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var landlord = new Profile
        {
            Id = Guid.NewGuid(),
            FullName = "Pending Landlord",
            Email = "pending@example.com",
            AvatarUrl = "https://example.com/avatar.png",
            Role = "Landlord",
            IsApproved = false
        };
        db.Profiles.Add(landlord);
        await db.SaveChangesAsync();
        var controller = new HousingsController(db, cache)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreatePrincipal(landlord.Id, "Landlord")
                }
            }
        };

        var result = await controller.Create(new Housing(), CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        db.Housings.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveLandlord_AdminUser_SuccessfullyUpdatesIsApproved()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var landlord = new Profile
        {
            Id = Guid.NewGuid(),
            FullName = "Pending Landlord",
            Email = "pending@example.com",
            AvatarUrl = "https://example.com/avatar.png",
            Role = "Landlord",
            IsApproved = false
        };
        db.Profiles.Add(landlord);
        await db.SaveChangesAsync();
        var controller = new AdminController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(Guid.NewGuid(), "Admin") }
            }
        };

        var result = await controller.ApproveLandlord(landlord.Id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        (await db.Profiles.FindAsync(landlord.Id))!.IsApproved.Should().BeTrue();
    }

    [Fact]
    public async Task GetProfiles_NonAdmin_ReturnsForbiddenOrUnauthorized()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var controller = new AdminController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(Guid.NewGuid(), "Student") }
            }
        };

        var result = await controller.GetProfiles(CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    private static ClaimsPrincipal CreatePrincipal(Guid profileId, string role) => new(
        new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, profileId.ToString()),
            new Claim(ClaimTypes.Role, role)
        ], "Test"));
}
