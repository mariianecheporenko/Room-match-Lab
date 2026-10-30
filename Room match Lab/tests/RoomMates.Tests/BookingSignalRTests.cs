using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using RoomMates.Controllers;
using RoomMates.Hubs;
using RoomMates.Models;
using RoomMates.Services;

namespace RoomMates.Tests;

public sealed class BookingSignalRTests
{
    [Fact]
    public async Task Create_ValidBooking_BroadcastsNewBookingExactlyOnce()
    {
        using var db = TestDbContextFactory.CreateInMemory();
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
        db.AddRange(profile, housing);
        await db.SaveChangesAsync();

        var clientProxy = new Mock<IClientProxy>();
        clientProxy
            .Setup(client => client.SendCoreAsync(
                "ReceiveNewBooking",
                It.Is<object?[]>(arguments =>
                    arguments.Length == 1 &&
                    arguments[0] != null &&
                    ((BookingCreatedEvent)arguments[0]!).HousingTitle == housing.Title &&
                    ((BookingCreatedEvent)arguments[0]!).ProfileName == profile.FullName),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var hubClients = new Mock<IHubClients>();
        hubClients.SetupGet(clients => clients.All).Returns(clientProxy.Object);
        var hubContext = new Mock<IHubContext<BookingHub>>();
        hubContext.SetupGet(context => context.Clients).Returns(hubClients.Object);

        var compatibility = new Mock<ICompatibilityService>();
        compatibility.Setup(service => service.CalculateMatchScore(
                It.IsAny<Profile>(), It.IsAny<Housing>()))
            .Returns(85.5);
        var controller = new BookingRequestsController(db, compatibility.Object, hubContext.Object);

        var result = await controller.Create(
            new CreateBookingRequestDto { ProfileId = profile.Id, HousingId = housing.Id },
            CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.CreatedAtActionResult>();
        clientProxy.Verify(client => client.SendCoreAsync(
            "ReceiveNewBooking",
            It.Is<object?[]>(arguments =>
                arguments.Length == 1 &&
                arguments[0] != null &&
                ((BookingCreatedEvent)arguments[0]!).ProfileName == profile.FullName &&
                ((BookingCreatedEvent)arguments[0]!).HousingTitle == housing.Title &&
                ((BookingCreatedEvent)arguments[0]!).MatchScore == 85.5 &&
                ((BookingCreatedEvent)arguments[0]!).Status == "Pending"),
            It.IsAny<CancellationToken>()), Times.Once);
        (await db.BookingRequests.CountAsync()).Should().Be(1);
    }
}
