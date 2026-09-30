using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomMates.Data;
using RoomMates.Models;
using RoomMates.Services;

namespace RoomMates.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class BookingRequestsController(
    RoomMatesDbContext dbContext,
    ICompatibilityService compatibilityService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BookingRequest>>> GetAll(
        CancellationToken cancellationToken)
    {
        var requests = await dbContext.BookingRequests
            .AsNoTracking()
            .OrderByDescending(request => request.CreatedAt)
            .ThenBy(request => request.Id)
            .ToListAsync(cancellationToken);

        return Ok(requests);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BookingRequest>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var request = await dbContext.BookingRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(request => request.Id == id, cancellationToken);

        return request is null ? NotFound() : Ok(request);
    }

    [HttpGet("profile/{profileId:guid}")]
    public async Task<ActionResult<IReadOnlyList<BookingRequest>>> GetByProfile(
        Guid profileId,
        CancellationToken cancellationToken)
    {
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
    public async Task<ActionResult<BookingRequest>> Create(
        CreateBookingRequestDto body,
        CancellationToken cancellationToken)
    {
        if (body.ProfileId == Guid.Empty || body.HousingId == Guid.Empty)
            return BadRequest(new { error = "ProfileId and HousingId are required." });

        var profile = await dbContext.Profiles
            .Include(profile => profile.ProfileTags)
                .ThenInclude(profileTag => profileTag.Tag)
            .Include(profile => profile.CustomCriterionValues)
            .FirstOrDefaultAsync(profile => profile.Id == body.ProfileId, cancellationToken);
        if (profile is null) return NotFound(new { error = "Profile was not found." });

        var housing = await dbContext.Housings
            .Include(housing => housing.HousingTags)
                .ThenInclude(housingTag => housingTag.Tag)
            .Include(housing => housing.CustomRequirements)
            .FirstOrDefaultAsync(housing => housing.Id == body.HousingId, cancellationToken);
        if (housing is null) return NotFound(new { error = "Housing was not found." });

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

        return CreatedAtAction(nameof(GetById), new { id = request.Id }, request);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        UpdateBookingRequestStatusDto body,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = body.Status.Trim().ToLowerInvariant() switch
        {
            "pending" => "Pending",
            "accepted" => "Accepted",
            "rejected" => "Rejected",
            _ => null
        };
        if (normalizedStatus is null)
            return BadRequest(new { error = "Status must be Pending, Accepted, or Rejected." });

        var request = await dbContext.BookingRequests
            .FirstOrDefaultAsync(request => request.Id == id, cancellationToken);
        if (request is null) return NotFound();

        request.Status = normalizedStatus;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var request = await dbContext.BookingRequests.FindAsync([id], cancellationToken);
        if (request is null) return NotFound();

        dbContext.BookingRequests.Remove(request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

public sealed class CreateBookingRequestDto
{
    public Guid ProfileId { get; init; }
    public Guid HousingId { get; init; }
}

public sealed class UpdateBookingRequestStatusDto
{
    [Required] public string Status { get; init; } = string.Empty;
}
