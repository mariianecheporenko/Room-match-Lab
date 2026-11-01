using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RoomMates.Hubs;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
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
