using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomMates.Data;
using RoomMates.Hubs;
using RoomMates.Models;
using RoomMates.Services;
using Microsoft.AspNetCore.SignalR;

namespace RoomMates.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class BookingRequestsController(
    RoomMatesDbContext dbContext,
    ICompatibilityService compatibilityService,
    IHubContext<BookingHub> hubContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BookingRequest>>> GetAll(
        CancellationToken cancellationToken)
    {
        if (!CurrentProfileId(out var profileId)) return Unauthorized();
        var tenantId = User.FindFirstValue("TenantId");
        var requests = await dbContext.BookingRequests
            .AsNoTracking()
            .Include(request => request.Housing)
            .Where(request => request.ProfileId == profileId ||
                (!string.IsNullOrWhiteSpace(tenantId) && request.Housing.TenantId == tenantId))
            .OrderByDescending(request => request.CreatedAt)
            .ThenBy(request => request.Id)
            .ToListAsync(cancellationToken);

        return Ok(requests);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BookingRequest>> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!CurrentProfileId(out var profileId)) return Unauthorized();
        var tenantId = User.FindFirstValue("TenantId");
        var request = await dbContext.BookingRequests
            .AsNoTracking()
            .Include(request => request.Housing)
            .FirstOrDefaultAsync(request => request.Id == id, cancellationToken);
        if (request is null) return NotFound();
        if (request.ProfileId != profileId &&
            (string.IsNullOrWhiteSpace(tenantId) || request.Housing.TenantId != tenantId)) return Forbid();
        return Ok(request);
    }

    [HttpGet("profile/{profileId:guid}")]
    public async Task<ActionResult<IReadOnlyList<BookingRequest>>> GetByProfile(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        if (!CurrentProfileId(out var currentProfileId)) return Unauthorized();
        if (profileId != currentProfileId) return Forbid();
        var profileExists = await dbContext.Profiles
            .AnyAsync(profile => profile.Id == profileId, cancellationToken);
        if (!profileExists) return NotFound();

        var requests = await dbContext.BookingRequests
            .AsNoTracking()
            .Where(request => request.ProfileId == profileId)
            .OrderByDescending(request => request.CreatedAt)
            .ThenBy(request => request.Id)
            .ToListAsync(cancellationToken);

        return Ok(requests);
    }

    [HttpPost]
    public async Task<ActionResult<MatchResultDto>> Create(
        CreateBookingRequestDto body,
        CancellationToken cancellationToken)
    {
        if (body.HousingId == Guid.Empty)
            return BadRequest(new { error = "HousingId is required." });

        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var profileId))
            return Unauthorized(new { error = "The authenticated user has no valid profile identifier." });

        var profile = await dbContext.Profiles
            .Include(profile => profile.ProfileTags)
                .ThenInclude(profileTag => profileTag.Tag)
            .Include(profile => profile.CustomCriterionValues)
            .FirstOrDefaultAsync(profile => profile.Id == profileId, cancellationToken);
        if (profile is null) return Unauthorized(new { error = "No profile is associated with this account." });

        var housing = await dbContext.Housings
            .Include(housing => housing.HousingTags)
                .ThenInclude(housingTag => housingTag.Tag)
            .Include(housing => housing.CustomRequirements)
            .FirstOrDefaultAsync(housing => housing.Id == body.HousingId, cancellationToken);
        if (housing is null) return NotFound(new { error = "Housing was not found." });
        var tenantId = User.FindFirstValue("TenantId");
        var canBrowseAcrossUniversities = bool.TryParse(
            User.FindFirstValue("IsCrossUniversity"), out var crossUniversity) && crossUniversity;
        if (!canBrowseAcrossUniversities && !string.IsNullOrWhiteSpace(tenantId) &&
            housing.TenantId is not null && housing.TenantId != tenantId)
            return NotFound(new { error = "Housing was not found." });

        var request = new BookingRequest
        {
            Id = Guid.NewGuid(),
            ProfileId = profile.Id,
            HousingId = housing.Id,
            MatchScore = compatibilityService.CalculateMatchScore(profile, housing),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        dbContext.BookingRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);
        await hubContext.Clients.All.SendAsync(
            "ReceiveNewBooking",
            new BookingCreatedEvent(
                request.Id,
                housing.Title,
                profile.FullName,
                request.MatchScore,
                request.Status,
                request.CreatedAt),
            cancellationToken);

        var enableDetailedAnalytics = bool.TryParse(
            User?.FindFirstValue("EnableDetailedAnalytics"), out var analyticsClaim) && analyticsClaim;
        var matchResult = new MatchResultDto
        {
            MatchScore = request.MatchScore,
            DetailedStats = enableDetailedAnalytics
                ? ["Cleanliness: +5%", "Tag Synergy: +4%"]
                : null
        };

        return CreatedAtAction(nameof(GetById), new { id = request.Id }, matchResult);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        UpdateBookingRequestStatusDto body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.Status))
            return BadRequest(new { error = "Status must be Pending, Accepted, or Rejected." });
        var normalizedStatus = body.Status.Trim().ToLowerInvariant() switch
        {
            "pending" => "Pending",
            "accepted" => "Accepted",
            "rejected" => "Rejected",
            _ => null
        };
        if (normalizedStatus is null)
            return BadRequest(new { error = "Status must be Pending, Accepted, or Rejected." });

        var tenantId = User.FindFirstValue("TenantId");
        if (string.IsNullOrWhiteSpace(tenantId)) return Forbid();
        var request = await dbContext.BookingRequests
            .Include(request => request.Housing)
            .FirstOrDefaultAsync(request => request.Id == id, cancellationToken);
        if (request is null) return NotFound();
        if (request.Housing.TenantId != tenantId) return Forbid();

        request.Status = normalizedStatus;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!CurrentProfileId(out var profileId)) return Unauthorized();
        var tenantId = User.FindFirstValue("TenantId");
        var request = await dbContext.BookingRequests
            .Include(request => request.Housing)
            .FirstOrDefaultAsync(request => request.Id == id, cancellationToken);
        if (request is null) return NotFound();
        if (request.ProfileId != profileId &&
            (string.IsNullOrWhiteSpace(tenantId) || request.Housing.TenantId != tenantId)) return Forbid();

        dbContext.BookingRequests.Remove(request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private bool CurrentProfileId(out Guid profileId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out profileId);
}

public sealed class CreateBookingRequestDto
{
    public Guid HousingId { get; init; }
}

public sealed class UpdateBookingRequestStatusDto
{
    [Required] public string Status { get; init; } = string.Empty;
}
