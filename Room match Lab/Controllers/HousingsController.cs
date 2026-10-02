using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RoomMates.Data;
using RoomMates.Models;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace RoomMates.Controllers;

[ApiController]
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
        var cacheKey = $"housings_skip_{skip}_limit_{limit}";
        if (cache.TryGetValue(cacheKey, out PagedResult<Housing>? cachedPage))
            return Ok(cachedPage);

        var totalCount = await dbContext.Housings.CountAsync(cancellationToken);
        var housings = await dbContext.Housings.AsNoTracking()
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
        var housing = await dbContext.Housings.AsNoTracking()
            .Include(x => x.CustomRequirements).ThenInclude(x => x.LifestyleCriterion)
            .Include(x => x.HousingTags).ThenInclude(x => x.Tag)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return housing is null ? NotFound() : Ok(housing);
    }

    [HttpPost]
    public async Task<ActionResult<Housing>> Create(Housing housing, CancellationToken cancellationToken)
    {
        housing.Id = Guid.NewGuid();
        dbContext.Housings.Add(housing);
        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateHousingPages();
        return CreatedAtAction(nameof(GetById), new { id = housing.Id }, housing);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, Housing housing, CancellationToken cancellationToken)
    {
        if (id != housing.Id) return BadRequest("Route id must match the housing id.");
        if (!await dbContext.Housings.AnyAsync(x => x.Id == id, cancellationToken)) return NotFound();
        dbContext.Entry(housing).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateHousingPages();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var housing = await dbContext.Housings.FindAsync([id], cancellationToken);
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
}
