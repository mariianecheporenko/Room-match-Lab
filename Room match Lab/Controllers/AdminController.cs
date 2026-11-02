using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomMates.Data;
using RoomMates.Models;
using RoomMates.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace RoomMates.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public sealed class AdminController(
    RoomMatesDbContext dbContext,
    IHubContext<BookingHub>? hubContext = null) : ControllerBase
{
    [HttpGet("profiles")]
    public async Task<ActionResult<IReadOnlyList<Profile>>> GetProfiles(CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        return Ok(await dbContext.Profiles.AsNoTracking().OrderBy(profile => profile.Email)
            .ToListAsync(cancellationToken));
    }

    [HttpGet("pending-landlords")]
    public async Task<ActionResult<IReadOnlyList<Profile>>> GetPendingLandlords(CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        return Ok(await dbContext.Profiles.AsNoTracking()
            .Where(profile => profile.Role == "Landlord" && !profile.IsApproved)
            .OrderBy(profile => profile.Email)
            .ToListAsync(cancellationToken));
    }

    [HttpPost("approve-landlord/{id:guid}")]
    public async Task<IActionResult> ApproveLandlord(Guid id, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        var profile = await dbContext.Profiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
        if (profile is null) return NotFound();
        if (profile.Role != "Landlord") return BadRequest(new { error = "The selected profile is not a landlord." });

        profile.IsApproved = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (hubContext is not null)
        {
            var statusUpdate = new LandlordStatusUpdatedEvent(
                profile.Id, profile.Email, profile.Role, profile.IsApproved, profile.FullName);
            await hubContext.Clients.Group(BookingHub.AdminsGroup)
                .SendAsync("LandlordStatusUpdated", statusUpdate, cancellationToken);
            await hubContext.Clients.User(profile.Id.ToString())
                .SendAsync("LandlordStatusUpdated", statusUpdate, cancellationToken);
        }
        return NoContent();
    }

    [HttpPost("revoke-landlord/{id:guid}")]
    public async Task<IActionResult> RevokeLandlord(Guid id, CancellationToken cancellationToken)
    {
        var profile = await dbContext.Profiles.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null) return NotFound();
        if (profile.Role != "Landlord")
            return BadRequest(new { error = "The selected profile is not a landlord." });

        profile.Role = "Student";
        profile.IsApproved = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (hubContext is not null)
        {
            var statusUpdate = new LandlordStatusUpdatedEvent(
                profile.Id, profile.Email, profile.Role, profile.IsApproved, profile.FullName);
            await hubContext.Clients.Group(BookingHub.AdminsGroup)
                .SendAsync("LandlordStatusUpdated", statusUpdate, cancellationToken);
            await hubContext.Clients.User(profile.Id.ToString())
                .SendAsync("LandlordStatusUpdated", statusUpdate, cancellationToken);
        }
        return NoContent();
    }
}
