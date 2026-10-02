using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomMates.Data;
using RoomMates.Models;

namespace RoomMates.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ProfilesController(RoomMatesDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Profile>>> GetAll(
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        skip = Math.Max(0, skip);
        limit = Math.Clamp(limit, 1, 100);

        var profiles = await ProfilesWithDetails()
            .OrderBy(profile => profile.FullName)
            .ThenBy(profile => profile.Id)
            .Skip(skip)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return Ok(profiles);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Profile>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var profile = await ProfilesWithDetails()
            .FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);

        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPost]
    public async Task<ActionResult<Profile>> Create(
        ProfileUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var validationError = ValidateRequest(request);
        if (validationError is not null) return BadRequest(new { error = validationError });

        var relationError = await ValidateRelations(request, cancellationToken);
        if (relationError is not null) return BadRequest(new { error = relationError });

        if (await dbContext.Profiles.AnyAsync(profile => profile.Email == request.Email, cancellationToken))
            return Conflict(new { error = "A profile with this email already exists." });

        var profile = new Profile
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim(),
            AvatarUrl = request.AvatarUrl.Trim(),
            Budget = request.Budget,
            PetTolerance = request.PetTolerance,
            OwnPets = request.OwnPets,
            IsSmoker = request.IsSmoker,
            Cleanliness = request.Cleanliness,
            SleepSchedule = request.SleepSchedule,
            PartyTolerance = request.PartyTolerance,
            ProfileTags = BuildTagLinks(request.TagIds),
            CustomCriterionValues = BuildCriterionValues(request.CustomCriterionValues)
        };

        dbContext.Profiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = profile.Id }, profile);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        ProfileUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var validationError = ValidateRequest(request);
        if (validationError is not null) return BadRequest(new { error = validationError });

        var relationError = await ValidateRelations(request, cancellationToken);
        if (relationError is not null) return BadRequest(new { error = relationError });

        var profile = await dbContext.Profiles
            .Include(existing => existing.ProfileTags)
            .Include(existing => existing.CustomCriterionValues)
            .FirstOrDefaultAsync(existing => existing.Id == id, cancellationToken);
        if (profile is null) return NotFound();

        if (await dbContext.Profiles.AnyAsync(
                existing => existing.Id != id && existing.Email == request.Email,
                cancellationToken))
            return Conflict(new { error = "A profile with this email already exists." });

        profile.FullName = request.FullName.Trim();
        profile.Email = request.Email.Trim();
        profile.AvatarUrl = request.AvatarUrl.Trim();
        profile.Budget = request.Budget;
        profile.PetTolerance = request.PetTolerance;
        profile.OwnPets = request.OwnPets;
        profile.IsSmoker = request.IsSmoker;
        profile.Cleanliness = request.Cleanliness;
        profile.SleepSchedule = request.SleepSchedule;
        profile.PartyTolerance = request.PartyTolerance;

        SynchronizeTagLinks(profile, request.TagIds);
        SynchronizeCriterionValues(profile, request.CustomCriterionValues);

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var profile = await dbContext.Profiles.FindAsync([id], cancellationToken);
        if (profile is null) return NotFound();

        dbContext.Profiles.Remove(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private IQueryable<Profile> ProfilesWithDetails() => dbContext.Profiles
        .AsNoTracking()
        .Include(profile => profile.ProfileTags)
            .ThenInclude(profileTag => profileTag.Tag)
        .Include(profile => profile.CustomCriterionValues)
            .ThenInclude(value => value.LifestyleCriterion);

    private string? ValidateRequest(ProfileUpsertRequest request)
    {
        if (request.Budget <= 0) return "Budget must be greater than zero.";
        if (!Enum.IsDefined(request.PetTolerance)) return "PetTolerance is invalid.";
        if (!Uri.TryCreate(request.AvatarUrl, UriKind.Absolute, out var avatarUri) ||
            (avatarUri.Scheme != Uri.UriSchemeHttp && avatarUri.Scheme != Uri.UriSchemeHttps))
            return "AvatarUrl must be an absolute HTTP or HTTPS URL.";

        var criteria = request.CustomCriterionValues ?? [];
        if (criteria.GroupBy(value => value.LifestyleCriterionId).Any(group => group.Count() > 1))
            return "A custom criterion can only be specified once per profile.";

        return null;
    }

    private async Task<string?> ValidateRelations(
        ProfileUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var tagIds = (request.TagIds ?? []).Distinct().ToArray();
        if (tagIds.Length > 0)
        {
            var existingTagCount = await dbContext.Tags
                .CountAsync(tag => tagIds.Contains(tag.Id), cancellationToken);
            if (existingTagCount != tagIds.Length) return "One or more tag IDs do not exist.";
        }

        var criterionIds = (request.CustomCriterionValues ?? [])
            .Select(value => value.LifestyleCriterionId)
            .Distinct()
            .ToArray();
        if (criterionIds.Length > 0)
        {
            var existingCriterionCount = await dbContext.LifestyleCriteria
                .CountAsync(criterion => criterionIds.Contains(criterion.Id), cancellationToken);
            if (existingCriterionCount != criterionIds.Length)
                return "One or more lifestyle criterion IDs do not exist.";
        }

        return null;
    }

    private static List<ProfileTag> BuildTagLinks(IEnumerable<Guid>? tagIds) =>
        (tagIds ?? []).Distinct()
            .Select(tagId => new ProfileTag { TagId = tagId })
            .ToList();

    private static List<ProfileCriterionValue> BuildCriterionValues(
        IEnumerable<ProfileCriterionValueInput>? values) =>
        (values ?? [])
            .Select(value => new ProfileCriterionValue
            {
                LifestyleCriterionId = value.LifestyleCriterionId,
                Value = value.Value
            })
            .ToList();

    private static void SynchronizeTagLinks(Profile profile, IEnumerable<Guid>? requestedTagIds)
    {
        var requested = (requestedTagIds ?? []).ToHashSet();
        foreach (var link in profile.ProfileTags.Where(link => !requested.Contains(link.TagId)).ToList())
            profile.ProfileTags.Remove(link);

        var existing = profile.ProfileTags.Select(link => link.TagId).ToHashSet();
        foreach (var tagId in requested.Except(existing))
            profile.ProfileTags.Add(new ProfileTag { ProfileId = profile.Id, TagId = tagId });
    }

    private static void SynchronizeCriterionValues(
        Profile profile,
        IEnumerable<ProfileCriterionValueInput>? requestedValues)
    {
        var requested = (requestedValues ?? []).ToDictionary(value => value.LifestyleCriterionId);
        foreach (var current in profile.CustomCriterionValues.ToList())
        {
            if (!requested.TryGetValue(current.LifestyleCriterionId, out var replacement))
            {
                profile.CustomCriterionValues.Remove(current);
                continue;
            }

            current.Value = replacement.Value;
        }

        var existing = profile.CustomCriterionValues
            .Select(value => value.LifestyleCriterionId)
            .ToHashSet();
        foreach (var replacement in requested.Values.Where(value => !existing.Contains(value.LifestyleCriterionId)))
        {
            profile.CustomCriterionValues.Add(new ProfileCriterionValue
            {
                ProfileId = profile.Id,
                LifestyleCriterionId = replacement.LifestyleCriterionId,
                Value = replacement.Value
            });
        }
    }
}

public sealed class ProfileUpsertRequest
{
    [Required, StringLength(200)] public string FullName { get; init; } = string.Empty;
    [Required, EmailAddress, StringLength(320)] public string Email { get; init; } = string.Empty;
    [Required, Url, StringLength(2048)] public string AvatarUrl { get; init; } = string.Empty;
    public decimal Budget { get; init; }
    public PetPolicy PetTolerance { get; init; } = PetPolicy.Allowed;
    public bool OwnPets { get; init; }
    public bool IsSmoker { get; init; }
    [Range(1, 5)] public int Cleanliness { get; init; } = 3;
    [Range(1, 5)] public int SleepSchedule { get; init; } = 3;
    [Range(1, 5)] public int PartyTolerance { get; init; } = 3;
    public List<Guid> TagIds { get; init; } = [];
    public List<ProfileCriterionValueInput> CustomCriterionValues { get; init; } = [];
}

public sealed class ProfileCriterionValueInput
{
    public Guid LifestyleCriterionId { get; init; }
    [Range(1, 5)] public int Value { get; init; }
}
