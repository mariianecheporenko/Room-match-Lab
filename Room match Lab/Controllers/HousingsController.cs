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
    public async Task<ActionResult<PagedResult<Housing>>> GetHousings(
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        skip = Math.Max(0, skip);
        limit = Math.Clamp(limit, 1, 100);
        var tenantId = User.FindFirstValue("TenantId");
        var isCrossUniversity = bool.TryParse(User.FindFirstValue("IsCrossUniversity"), out var crossUniversityClaim)
            && crossUniversityClaim;
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
    public async Task<ActionResult<Housing>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = User.FindFirstValue("TenantId");
        var isCrossUniversity = bool.TryParse(User.FindFirstValue("IsCrossUniversity"), out var crossUniversityClaim)
            && crossUniversityClaim;

        var housing = await GetVisibleHousings(tenantId, isCrossUniversity).AsNoTracking()
            .Include(x => x.CustomRequirements).ThenInclude(x => x.LifestyleCriterion)
            .Include(x => x.HousingTags).ThenInclude(x => x.Tag)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return housing is null ? NotFound() : Ok(housing);
    }

    [HttpPost]
    public async Task<ActionResult<Housing>> Create(Housing housing, CancellationToken cancellationToken)
    {
        var tenantId = User.FindFirstValue("TenantId");

        housing.Id = Guid.NewGuid();
        housing.TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
        dbContext.Housings.Add(housing);
        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateHousingPages();
        return CreatedAtAction(nameof(GetById), new { id = housing.Id }, housing);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, Housing housing, CancellationToken cancellationToken)
    {
        var tenantId = User.FindFirstValue("TenantId");
        if (id != housing.Id) return BadRequest("Route id must match the housing id.");
        if (!await dbContext.Housings.AnyAsync(
                x => x.Id == id && x.TenantId == tenantId, cancellationToken)) return NotFound();
        housing.TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
        dbContext.Entry(housing).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateHousingPages();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = User.FindFirstValue("TenantId");
        var housing = await dbContext.Housings
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);
        if (housing is null) return NotFound();
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
        if (!isCrossUniversity && !string.IsNullOrWhiteSpace(tenantId))
            housings = housings.Where(housing => housing.TenantId == tenantId || housing.TenantId == null);

        return housings;
    }
}
