using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using RoomMates.Controllers;
using RoomMates.Models;
using RoomMates.Services;

namespace RoomMates.Tests;

public class ProfilesControllerTests
{
    [Fact]
    public async Task CreateProfile_WithNegativeBudget_ReturnsBadRequest()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.CreateInMemory();
        var controller = new ProfilesController(dbContext);
        var request = new ProfileUpsertRequest
        {
            FullName = "Avery Example",
            Email = "avery@example.com",
            AvatarUrl = "https://example.com/avatar.png",
            Budget = -1
        };

        // Act
        var result = await controller.Create(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }
}

public class BookingRequestsControllerTests
{
    [Fact]
    public async Task CreateBookingRequest_WithInvalidProfileId_ReturnsNotFound()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.CreateInMemory();
        var compatibilityService = new Mock<ICompatibilityService>();
        var controller = new BookingRequestsController(dbContext, compatibilityService.Object);
        var request = new CreateBookingRequestDto
        {
            ProfileId = Guid.NewGuid(),
            HousingId = Guid.NewGuid()
        };

        // Act
        var result = await controller.Create(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        compatibilityService.Verify(
            service => service.CalculateMatchScore(It.IsAny<Profile>(), It.IsAny<Housing>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateBookingRequest_Success_CalculatesScoreAndSetsPendingStatus()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.CreateInMemory();
        var profile = new Profile
        {
            Id = Guid.NewGuid(),
            FullName = "Avery Example",
            Email = "avery@example.com",
            AvatarUrl = "https://example.com/avatar.png",
            Budget = 1_000
        };
        var housing = new Housing
        {
            Id = Guid.NewGuid(),
            Title = "Sunny apartment",
            City = "Kyiv",
            Street = "Main Street",
            BuildingNumber = "10",
            PricePerMonth = 800
        };
        dbContext.Profiles.Add(profile);
        dbContext.Housings.Add(housing);
        await dbContext.SaveChangesAsync();

        var compatibilityService = new Mock<ICompatibilityService>();
        compatibilityService
            .Setup(service => service.CalculateMatchScore(It.IsAny<Profile>(), It.IsAny<Housing>()))
            .Returns(85.5);
        var controller = new BookingRequestsController(dbContext, compatibilityService.Object);
        var request = new CreateBookingRequestDto { ProfileId = profile.Id, HousingId = housing.Id };

        // Act
        var result = await controller.Create(request, CancellationToken.None);

        // Assert
        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var bookingRequest = created.Value.Should().BeOfType<BookingRequest>().Subject;
        bookingRequest.Status.Should().Be("Pending");
        bookingRequest.MatchScore.Should().Be(85.5);
        compatibilityService.Verify(
            service => service.CalculateMatchScore(
                It.Is<Profile>(candidate => candidate.Id == profile.Id),
                It.Is<Housing>(candidate => candidate.Id == housing.Id)),
            Times.Once);

        (await dbContext.BookingRequests.SingleAsync()).Should().BeEquivalentTo(bookingRequest);
    }
}

public class HousingsControllerTests
{
    [Fact]
    public async Task GetHousings_WithPagination_ReturnsCorrectSubset()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.CreateInMemory();
        var housings = Enumerable.Range(1, 5)
            .Select(index => new Housing
            {
                Id = Guid.NewGuid(),
                Title = $"Housing {index}",
                City = "Kyiv",
                Street = $"Street {index}",
                BuildingNumber = index.ToString(),
                PricePerMonth = 500 + index
            })
            .ToList();
        dbContext.Housings.AddRange(housings);
        await dbContext.SaveChangesAsync();

        var controller = new HousingsController(dbContext);

        // Act
        var result = await controller.GetAll(skip: 2, limit: 2, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedHousings = okResult.Value.Should()
            .BeAssignableTo<IReadOnlyList<Housing>>()
            .Subject;

        returnedHousings.Should().HaveCount(2);
        returnedHousings.Select(housing => housing.Id)
            .Should().Equal(housings[2].Id, housings[3].Id);
        returnedHousings.Select(housing => housing.Title)
            .Should().Equal("Housing 3", "Housing 4");
    }
}
