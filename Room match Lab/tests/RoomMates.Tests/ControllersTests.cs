using FluentAssertions;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RoomMates.Controllers;
using RoomMates.Hubs;
using RoomMates.Models;
using RoomMates.Services;
using Moq;

namespace RoomMates.Tests;

public class ProfilesControllerTests
{
    [Fact]
    public void Create_ReturnsForbidBecauseRegistrationOwnsProfileCreation()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var controller = new ProfilesController(db);

        var result = controller.Create();

        result.Should().BeOfType<ForbidResult>();
        db.Profiles.Should().BeEmpty();
    }
}

public class BookingRequestsControllerTests
{
    [Fact]
    public async Task Create_ProfileDoesNotExist_ReturnsNotFoundWithoutScoring()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var compatibility = new Mock<ICompatibilityService>();
        var controller = new BookingRequestsController(db, compatibility.Object, new Mock<IHubContext<BookingHub>>().Object);
        Authenticate(controller, Guid.NewGuid());

        var result = await controller.Create(
            new CreateBookingRequestDto { HousingId = Guid.NewGuid() },
            CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        compatibility.Verify(x => x.CalculateMatchScore(It.IsAny<Profile>(), It.IsAny<Housing>()), Times.Never);
    }

    [Fact]
    public async Task Create_HousingDoesNotExist_ReturnsNotFoundWithoutScoring()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var profile = ValidProfile();
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();
        var compatibility = new Mock<ICompatibilityService>();
        var controller = new BookingRequestsController(db, compatibility.Object, new Mock<IHubContext<BookingHub>>().Object);
        Authenticate(controller, profile.Id);

        var result = await controller.Create(
            new CreateBookingRequestDto { HousingId = Guid.NewGuid() },
            CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
        compatibility.Verify(x => x.CalculateMatchScore(It.IsAny<Profile>(), It.IsAny<Housing>()), Times.Never);
    }

    [Fact]
    public async Task Create_ValidRequestScoresPersistsPendingBookingAndReturnsCreated()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var profile = ValidProfile();
        var housing = ValidHousing();
        db.AddRange(profile, housing);
        await db.SaveChangesAsync();
        var compatibility = new Mock<ICompatibilityService>();
        compatibility.Setup(x => x.CalculateMatchScore(It.IsAny<Profile>(), It.IsAny<Housing>())).Returns(85.5);
        var hubClients = new Mock<IHubClients>();
        hubClients.SetupGet(clients => clients.All).Returns(Mock.Of<IClientProxy>());
        var hubContext = new Mock<IHubContext<BookingHub>>();
        hubContext.SetupGet(context => context.Clients).Returns(hubClients.Object);
        var controller = new BookingRequestsController(db, compatibility.Object, hubContext.Object);
        Authenticate(controller, profile.Id);

        var result = await controller.Create(
            new CreateBookingRequestDto { HousingId = housing.Id },
            CancellationToken.None);

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var match = created.Value.Should().BeOfType<MatchResultDto>().Subject;
        match.MatchScore.Should().Be(85.5);
        match.DetailedStats.Should().BeNull();
        compatibility.Verify(
            x => x.CalculateMatchScore(
                It.Is<Profile>(p => p.Id == profile.Id), It.Is<Housing>(h => h.Id == housing.Id)),
            Times.Once);
        var persisted = await db.BookingRequests.SingleAsync();
        persisted.ProfileId.Should().Be(profile.Id);
        persisted.HousingId.Should().Be(housing.Id);
        persisted.Status.Should().Be("Pending");
        persisted.MatchScore.Should().Be(85.5);
    }

    [Fact]
    public async Task UpdateStatus_ValidAcceptedValue_UpdatesAndReturnsNoContent()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var profile = ValidProfile();
        var housing = ValidHousing();
        var booking = new BookingRequest { Id = Guid.NewGuid(), Profile = profile, Housing = housing, Status = "Pending" };
        db.BookingRequests.Add(booking);
        await db.SaveChangesAsync();
        var controller = new BookingRequestsController(
            db,
            new Mock<ICompatibilityService>().Object,
            new Mock<IHubContext<BookingHub>>().Object);
        Authenticate(controller, Guid.NewGuid(), "KNU");

        var result = await controller.UpdateStatus(booking.Id, new UpdateBookingRequestStatusDto { Status = "Accepted" }, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        (await db.BookingRequests.SingleAsync()).Status.Should().Be("Accepted");
    }

    [Fact]
    public async Task UpdateStatus_InvalidString_ReturnsBadRequestAndDoesNotChangeStatus()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var booking = NewBooking();
        db.BookingRequests.Add(booking);
        await db.SaveChangesAsync();
        var controller = new BookingRequestsController(
            db,
            new Mock<ICompatibilityService>().Object,
            new Mock<IHubContext<BookingHub>>().Object);

        var result = await controller.UpdateStatus(booking.Id, new UpdateBookingRequestStatusDto { Status = "UnknownStatus" }, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.BookingRequests.SingleAsync()).Status.Should().Be("Pending");
    }

    private static BookingRequest NewBooking() => new()
    {
        Id = Guid.NewGuid(), Profile = ValidProfile(), Housing = ValidHousing(), Status = "Pending"
    };

    private static Profile ValidProfile() => new()
    {
        Id = Guid.NewGuid(), FullName = "Avery Example", Email = $"{Guid.NewGuid():N}@example.com",
        AvatarUrl = "https://example.com/avatar.png", Budget = 1_000
    };

    private static Housing ValidHousing() => new()
    {
        Id = Guid.NewGuid(), Title = "Sunny apartment", TenantId = "KNU", City = "Kyiv", Street = "Main Street",
        BuildingNumber = "10", PricePerMonth = 800
    };

    private static void Authenticate(BookingRequestsController controller, Guid profileId, string? tenantId = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, profileId.ToString()) };
        if (tenantId is not null) claims.Add(new Claim("TenantId", tenantId));
        var identity = new ClaimsIdentity(claims, "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }
}

public class HousingsControllerTests
{
    [Fact]
    public async Task GetHousings_WithPagination_ReturnsCorrectSubsetAndNextLink()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var housings = Enumerable.Range(1, 5).Select(index => new Housing
        {
            Id = Guid.NewGuid(), Title = $"Housing {index}", TenantId = "KNU", City = "Kyiv", Street = $"Street {index}",
            BuildingNumber = index.ToString(), PricePerMonth = 500 + index
        }).ToList();
        db.Housings.AddRange(housings);
        await db.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = CreateController(db, cache);

        var firstPageResult = await controller.GetHousings(skip: 0, limit: 2, CancellationToken.None);
        var firstPage = firstPageResult.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<PagedResult<Housing>>().Subject;
        firstPage.TotalCount.Should().Be(5);
        firstPage.Skip.Should().Be(0);
        firstPage.Limit.Should().Be(2);
        firstPage.Items.Select(h => h.Id).Should().Equal(housings.Take(2).Select(h => h.Id));
        firstPage.NextLink.Should().Be("https://roommates.example/api/Housings?skip=2&limit=2");

        var secondPageResult = await controller.GetHousings(skip: 2, limit: 2, CancellationToken.None);
        var secondPage = secondPageResult.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<PagedResult<Housing>>().Subject;
        secondPage.Items.Select(h => h.Id).Should().Equal(housings.Skip(2).Take(2).Select(h => h.Id));
        secondPage.NextLink.Should().Be("https://roommates.example/api/Housings?skip=4&limit=2");

        var beyondEndResult = await controller.GetHousings(skip: 10, limit: 2, CancellationToken.None);
        var beyondEnd = beyondEndResult.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<PagedResult<Housing>>().Subject;
        beyondEnd.Items.Should().BeEmpty();
        beyondEnd.NextLink.Should().BeNull();
    }

    [Fact]
    public async Task GetHousings_UsesCache_OnSubsequentCalls()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var originalHousing = MakeHousing("Housing 1");
        db.Housings.Add(originalHousing);
        await db.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = CreateController(db, cache);

        var firstResult = await controller.GetHousings(skip: 0, limit: 10, CancellationToken.None);
        var firstPage = firstResult.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<PagedResult<Housing>>().Subject;

        db.Housings.Add(MakeHousing("Housing 2"));
        await db.SaveChangesAsync();

        var secondResult = await controller.GetHousings(skip: 0, limit: 10, CancellationToken.None);
        var secondPage = secondResult.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<PagedResult<Housing>>().Subject;

        secondPage.Should().BeSameAs(firstPage);
        secondPage.TotalCount.Should().Be(1);
        secondPage.Items.Select(h => h.Id).Should().Equal(originalHousing.Id);
    }

    [Fact]
    public async Task CreateAndDelete_InvalidateHousingPageCache()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var landlord = ValidLandlord();
        var originalHousing = MakeHousing("Housing 1");
        db.AddRange(landlord, originalHousing);
        await db.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = CreateController(db, cache, landlord.Id);

        (await ReadPage(controller)).TotalCount.Should().Be(1);
        var newHousing = MakeHousing("Housing 2");
        newHousing.LandlordId = Guid.NewGuid(); // Simulate a forged owner supplied by the client.
        var createResult = await controller.Create(newHousing, CancellationToken.None);
        createResult.Result.Should().BeOfType<CreatedAtActionResult>();
        newHousing.LandlordId.Should().Be(landlord.Id);
        (await ReadPage(controller)).TotalCount.Should().Be(2);

        var deleteResult = await controller.Delete(newHousing.Id, CancellationToken.None);
        deleteResult.Should().BeOfType<NoContentResult>();
        (await ReadPage(controller)).TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Create_WithoutMatchingAuthenticatedProfile_ReturnsForbid()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = CreateController(db, cache, Guid.NewGuid());

        var result = await controller.Create(MakeHousing("Housing without profile"), CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
        db.Housings.Should().BeEmpty();
    }

    private static Profile ValidLandlord() => new()
    {
        Id = Guid.NewGuid(), FullName = "Landlord Example", Email = $"{Guid.NewGuid():N}@example.com",
        AvatarUrl = "https://example.com/landlord.png", TenantId = "KNU"
    };

    private static HousingsController CreateController(
        RoomMates.Data.RoomMatesDbContext db,
        IMemoryCache cache,
        Guid? profileId = null)
    {
        var claims = new List<Claim> { new("TenantId", "KNU") };
        if (profileId.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, profileId.Value.ToString()));
        var controller = new HousingsController(db, cache)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            }
        };
        controller.Request.Scheme = "https";
        controller.Request.Host = new HostString("roommates.example");
        controller.Request.Path = "/api/Housings";
        return controller;
    }

    private static async Task<PagedResult<Housing>> ReadPage(HousingsController controller)
    {
        var result = await controller.GetHousings(skip: 0, limit: 10, CancellationToken.None);
        return result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<PagedResult<Housing>>().Subject;
    }

    private static Housing MakeHousing(string title) => new()
    {
        Id = Guid.NewGuid(), Title = title, TenantId = "KNU", City = "Kyiv", Street = "Street 1",
        BuildingNumber = "1", PricePerMonth = 500
    };
}
