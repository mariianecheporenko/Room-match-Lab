using Microsoft.AspNetCore.SignalR;

namespace RoomMates.Hubs;

public sealed class BookingHub : Hub
{
}

public sealed record BookingCreatedEvent(
    Guid Id,
    string HousingTitle,
    string ProfileName,
    double MatchScore,
    string Status,
    DateTime CreatedAt);
