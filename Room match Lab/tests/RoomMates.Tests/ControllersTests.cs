using FluentAssertions;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RoomMates.Controllers;
using RoomMates.Models;
using RoomMates.Services;
using Moq;

namespace RoomMates.Tests;

public class ProfilesControllerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_BudgetAtOrBelowZero_ReturnsBadRequest(decimal budget)
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var controller = new ProfilesController(db);

        var result = await controller.Create(ValidProfileRequest(budget), CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        (await db.Profiles.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Create_CleanlinessOutsideOneToFive_ReturnsBadRequest(int cleanliness)
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var controller = new ProfilesController(db);
        var request = ValidProfileRequest(cleanliness: cleanliness);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), validationResults, validateAllProperties: true);
        foreach (var error in validationResults)
            foreach (var member in error.MemberNames)
                controller.ModelState.AddModelError(member, error.ErrorMessage ?? "Invalid value.");

        var result = await controller.Create(request, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        (await db.Profiles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_ValidProfile_ReturnsCreatedAndPersistsProfile()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var controller = new ProfilesController(db);

        var result = await controller.Create(ValidProfileRequest(), CancellationToken.None);

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var profile = created.Value.Should().BeOfType<Profile>().Subject;
        profile.Id.Should().NotBeEmpty();
        profile.FullName.Should().Be("Avery Example");
        var persisted = await db.Profiles.SingleAsync();
        persisted.Id.Should().Be(profile.Id);
        persisted.Email.Should().Be("avery@example.com");
    }

    private static ProfileUpsertRequest ValidProfileRequest(decimal budget = 1_000, int cleanliness = 3) => new()
    {
        FullName = "Avery Example",
        Email = "avery@example.com",
        AvatarUrl = "https://example.com/avatar.png",
        Budget = budget,
        Cleanliness = cleanliness
    };
}

public class BookingRequestsControllerTests
{
    [Fact]
    public async Task Create_ProfileDoesNotExist_ReturnsNotFoundWithoutScoring()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var compatibility = new Mock<ICompatibilityService>();
        var controller = new BookingRequestsController(db, compatibility.Object);

        var result = await controller.Create(
            new CreateBookingRequestDto { ProfileId = Guid.NewGuid(), HousingId = Guid.NewGuid() },
            CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
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
        var controller = new BookingRequestsController(db, compatibility.Object);

        var result = await controller.Create(
            new CreateBookingRequestDto { ProfileId = profile.Id, HousingId = Guid.NewGuid() },
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
        var controller = new BookingRequestsController(db, compatibility.Object);

        var result = await controller.Create(
            new CreateBookingRequestDto { ProfileId = profile.Id, HousingId = housing.Id },
            CancellationToken.None);

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var booking = created.Value.Should().BeOfType<BookingRequest>().Subject;
        booking.Status.Should().Be("Pending");
        booking.MatchScore.Should().Be(85.5);
        booking.ProfileId.Should().Be(profile.Id);
        booking.HousingId.Should().Be(housing.Id);
        compatibility.Verify(
            x => x.CalculateMatchScore(
                It.Is<Profile>(p => p.Id == profile.Id), It.Is<Housing>(h => h.Id == housing.Id)),
            Times.Once);
        var persisted = await db.BookingRequests.SingleAsync();
        persisted.Id.Should().Be(booking.Id);
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
        var controller = new BookingRequestsController(db, new Mock<ICompatibilityService>().Object);

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
        var controller = new BookingRequestsController(db, new Mock<ICompatibilityService>().Object);

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
        Id = Guid.NewGuid(), Title = "Sunny apartment", City = "Kyiv", Street = "Main Street",
        BuildingNumber = "10", PricePerMonth = 800
    };
}

public class HousingsControllerTests
{
    [Fact]
    public async Task GetHousings_WithPagination_ReturnsCorrectSubsetAndNextLink()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var housings = Enumerable.Range(1, 5).Select(index => new Housing
        {
            Id = Guid.NewGuid(), Title = $"Housing {index}", City = "Kyiv", Street = $"Street {index}",
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
        var originalHousing = MakeHousing("Housing 1");
        db.Housings.Add(originalHousing);
        await db.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = CreateController(db, cache);

        (await ReadPage(controller)).TotalCount.Should().Be(1);
        var newHousing = MakeHousing("Housing 2");
        var createResult = await controller.Create(newHousing, CancellationToken.None);
        createResult.Result.Should().BeOfType<CreatedAtActionResult>();
        (await ReadPage(controller)).TotalCount.Should().Be(2);

        var deleteResult = await controller.Delete(newHousing.Id, CancellationToken.None);
        deleteResult.Should().BeOfType<NoContentResult>();
        (await ReadPage(controller)).TotalCount.Should().Be(1);
    }

    private static HousingsController CreateController(RoomMates.Data.RoomMatesDbContext db, IMemoryCache cache)
    {
        var controller = new HousingsController(db, cache)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
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
        Id = Guid.NewGuid(), Title = title, City = "Kyiv", Street = "Street 1",
        BuildingNumber = "1", PricePerMonth = 500
    };
}
