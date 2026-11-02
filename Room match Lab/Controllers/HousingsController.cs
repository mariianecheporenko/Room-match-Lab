using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RoomMates.Data;
using RoomMates.Models;
using System.Runtime.CompilerServices;
using System.Security.Claims;

namespace RoomMates.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class HousingsController(RoomMatesDbContext dbContext, IMemoryCache cache) : ControllerBase
{
    private static readonly ConditionalWeakTable<IMemoryCache, Dictionary<string, DateTime>> HousingPageCacheKeys = new();
    private static readonly object CacheInvalidationLock = new();

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<Housing>>> GetHousings(
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        skip = Math.Max(0, skip);
        limit = Math.Clamp(limit, 1, 100);
        var tenantId = User.FindFirstValue("TenantId");
        var isCrossUniversity = User.IsInRole("Admin") ||
            (bool.TryParse(User.FindFirstValue("IsCrossUniversity"), out var crossUniversityClaim)
                && crossUniversityClaim);
        var tenantScope = isCrossUniversity || string.IsNullOrWhiteSpace(tenantId)
            ? "all"
            : $"tenant_{tenantId}";
        var cacheKey = $"housings_{tenantScope}_skip_{skip}_limit_{limit}";
        if (cache.TryGetValue(cacheKey, out PagedResult<Housing>? cachedPage))
            return Ok(cachedPage);

        var visibleHousings = GetVisibleHousings(tenantId, isCrossUniversity);
        var totalCount = await visibleHousings.CountAsync(cancellationToken);
        var housings = await visibleHousings.AsNoTracking()
            .Include(x => x.CustomRequirements).ThenInclude(x => x.LifestyleCriterion)
            .Include(x => x.HousingTags).ThenInclude(x => x.Tag)
            .OrderBy(x => x.Title)
            .Skip(skip)
            .Take(limit)
            .ToListAsync(cancellationToken);

        string? nextLink = null;
        if (skip + limit < totalCount)
        {
            var query = QueryString.Create(new[]
            {
                new KeyValuePair<string, string?>("skip", (skip + limit).ToString()),
                new KeyValuePair<string, string?>("limit", limit.ToString())
            });
            nextLink = UriHelper.BuildAbsolute(
                Request.Scheme, Request.Host, Request.PathBase, Request.Path, query);
        }

        var page = new PagedResult<Housing>
        {
            TotalCount = totalCount,
            Skip = skip,
            Limit = limit,
            NextLink = nextLink,
            Items = housings
        };

        lock (CacheInvalidationLock)
        {
            var cacheKeys = HousingPageCacheKeys.GetValue(cache, _ => new Dictionary<string, DateTime>());
            var now = DateTime.UtcNow;
            foreach (var expiredKey in cacheKeys.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray())
                cacheKeys.Remove(expiredKey);

            cache.Set(cacheKey, page, new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(60)));
            cacheKeys[cacheKey] = now.AddSeconds(60);
        }

        return Ok(page);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<Housing>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = User.FindFirstValue("TenantId");
        var isCrossUniversity = User.IsInRole("Admin") ||
            (bool.TryParse(User.FindFirstValue("IsCrossUniversity"), out var crossUniversityClaim)
                && crossUniversityClaim);

        var housing = await GetVisibleHousings(tenantId, isCrossUniversity).AsNoTracking()
            .Include(x => x.CustomRequirements).ThenInclude(x => x.LifestyleCriterion)
            .Include(x => x.HousingTags).ThenInclude(x => x.Tag)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return housing is null ? NotFound() : Ok(housing);
    }

    [HttpPost]
    [Authorize(Roles = "Landlord,Admin")]
    public async Task<ActionResult<Housing>> Create(Housing housing, CancellationToken cancellationToken)
    {
        var currentProfile = await FindCurrentProfileAsync(cancellationToken);
        if (currentProfile is null)
            return Forbid();
        if (currentProfile.Role == "Landlord" && !currentProfile.IsApproved)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Your landlord account is pending admin approval." });
        if (User.IsInRole("Admin") ? currentProfile.Role != "Admin" : currentProfile.Role != "Landlord")
            return Forbid();

        housing.Id = Guid.NewGuid();
        housing.TenantId = currentProfile.TenantId;
        housing.LandlordId = currentProfile.Id;
        housing.Landlord = currentProfile;
        dbContext.Housings.Add(housing);
        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateHousingPages();
        return CreatedAtAction(nameof(GetById), new { id = housing.Id }, housing);
    }

    private Task<Profile?> FindCurrentProfileAsync(CancellationToken cancellationToken)
    {
        var profileIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(profileIdClaim, out var profileId))
            return dbContext.Profiles.FirstOrDefaultAsync(profile => profile.Id == profileId, cancellationToken);

        var email = User.FindFirstValue(ClaimTypes.Email);
        return string.IsNullOrWhiteSpace(email)
            ? Task.FromResult<Profile?>(null)
            : dbContext.Profiles.FirstOrDefaultAsync(profile => profile.Email == email, cancellationToken);
    }

    private Task<bool> CanManageHousingAsync(Housing housing, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin")) return Task.FromResult(true);
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var profileId) ||
            housing.LandlordId != profileId)
            return Task.FromResult(false);

        return dbContext.Profiles.AnyAsync(profile =>
            profile.Id == profileId && profile.Role == "Landlord" && profile.IsApproved, cancellationToken);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Landlord,Admin")]
    public async Task<IActionResult> Update(Guid id, Housing housing, CancellationToken cancellationToken)
    {
        if (id != housing.Id) return BadRequest("Route id must match the housing id.");
        var existing = await dbContext.Housings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null) return NotFound();
        if (!await CanManageHousingAsync(existing, cancellationToken)) return Forbid();

        existing.Title = housing.Title;
        existing.City = housing.City;
        existing.District = housing.District;
        existing.Street = housing.Street;
        existing.BuildingNumber = housing.BuildingNumber;
        existing.ApartmentNumber = housing.ApartmentNumber;
        existing.Latitude = housing.Latitude;
        existing.Longitude = housing.Longitude;
        existing.PricePerMonth = housing.PricePerMonth;
        existing.PetPolicy = housing.PetPolicy;
        existing.HasExistingPets = housing.HasExistingPets;
        existing.AllowsSmoking = housing.AllowsSmoking;
        existing.RequiredCleanliness = housing.RequiredCleanliness;
        existing.RequiredSleepSchedule = housing.RequiredSleepSchedule;
        existing.RequiredPartyTolerance = housing.RequiredPartyTolerance;
        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateHousingPages();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Landlord,Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var housing = await dbContext.Housings
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (housing is null) return NotFound();
        if (!await CanManageHousingAsync(housing, cancellationToken)) return Forbid();
        dbContext.Housings.Remove(housing);
        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateHousingPages();
        return NoContent();
    }

    private void InvalidateHousingPages()
    {
        lock (CacheInvalidationLock)
        {
            var cacheKeys = HousingPageCacheKeys.GetValue(cache, _ => new Dictionary<string, DateTime>());
            foreach (var key in cacheKeys.Keys.ToArray())
                cache.Remove(key);
            cacheKeys.Clear();
        }
    }

    private IQueryable<Housing> GetVisibleHousings(string? tenantId, bool isCrossUniversity)
    {
        var housings = dbContext.Housings.AsQueryable();
        if (!User.IsInRole("Admin") && !isCrossUniversity && !string.IsNullOrWhiteSpace(tenantId))
            housings = housings.Where(housing => housing.TenantId == tenantId || housing.TenantId == null);

        return housings;
    }
}
