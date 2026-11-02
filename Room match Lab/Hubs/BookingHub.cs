using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RoomMates.Hubs;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public sealed class BookingHub : Hub
{
    public const string AdminsGroup = "roommates-admins";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole("Admin") == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminsGroup);

        await base.OnConnectedAsync();
    }
}

public sealed record BookingCreatedEvent(
    Guid Id,
    string HousingTitle,
    string ProfileName,
    double MatchScore,
    string Status,
    DateTime CreatedAt);

public sealed record LandlordStatusUpdatedEvent(
    Guid UserId,
    string Email,
    string Role,
    bool IsApproved,
    string Name);

public sealed record NewLandlordApplicationEvent(
    Guid UserId,
    string Email,
    string Name,
    string Role,
    bool IsApproved);
