
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using RoomMates.Models;
using RoomMates.Data;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public class BookingRequestsMVCController : Controller
{
    private readonly RoomMatesDbContext _context;
    public BookingRequestsMVCController(RoomMatesDbContext context)
    {
        _context = context;
    }

    // GET: BOOKINGREQUESTS
    public async Task<IActionResult> Index()    
    {
        if (!CurrentProfileId(out var profileId)) return Unauthorized();
        var tenantId = User.FindFirstValue("TenantId");
        return View(await _context.BookingRequests
            .Include(request => request.Profile)
            .Include(request => request.Housing)
            .AsNoTracking()
            .Where(request => request.ProfileId == profileId ||
                (!string.IsNullOrWhiteSpace(tenantId) && request.Housing.TenantId == tenantId))
            .ToListAsync());
    }

    // GET: BOOKINGREQUESTS/Details/5
    public async Task<IActionResult> Details(System.Guid? id)
    {
        if (!CurrentProfileId(out var profileId)) return Unauthorized();
        if (id == null)
        {
            return NotFound();
        }

        var bookingrequest = await _context.BookingRequests
            .Include(request => request.Profile)
            .Include(request => request.Housing)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (bookingrequest == null)
        {
            return NotFound();
        }
        if (!CanView(bookingrequest, profileId)) return Forbid();

        return View(bookingrequest);
    }

    // GET: BOOKINGREQUESTS/Create
    public IActionResult Create()
    {
        return RedirectToAction("Index", "HousingsMvc");
    }

    // Booking requests are submitted through the authenticated API from the housing details page.
    [HttpPost, ActionName("Create")]
    [ValidateAntiForgeryToken]
    public IActionResult CreatePost()
    {
        return RedirectToAction("Index", "HousingsMvc");
    }

    // GET: BOOKINGREQUESTS/Edit/5
    public async Task<IActionResult> Edit(System.Guid? id)
    {
        if (!CanManageRequests()) return Forbid();
        if (id == null)
        {
            return NotFound();
        }

        var bookingrequest = await _context.BookingRequests.Include(request => request.Housing)
            .FirstOrDefaultAsync(request => request.Id == id);
        if (bookingrequest == null)
        {
            return NotFound();
        }
        if (!CanManageHousing(bookingrequest.Housing)) return Forbid();
        return View(bookingrequest);
    }

    // POST: BOOKINGREQUESTS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(System.Guid? id, [Bind("Id,ProfileId,Profile,HousingId,Housing,MatchScore,Status,CreatedAt")] BookingRequest bookingrequest)
    {
        if (id != bookingrequest.Id) return NotFound();
        if (!CanManageRequests()) return Forbid();
        var existing = await _context.BookingRequests.Include(request => request.Housing)
            .FirstOrDefaultAsync(request => request.Id == id);
        if (existing is null) return NotFound();
        if (!CanManageHousing(existing.Housing)) return Forbid();
        if (string.IsNullOrWhiteSpace(bookingrequest.Status))
        {
            ModelState.AddModelError(nameof(bookingrequest.Status), "Choose Pending, Accepted, or Rejected.");
            return View(existing);
        }
        var status = bookingrequest.Status.Trim().ToLowerInvariant() switch
        {
            "pending" => "Pending",
            "accepted" => "Accepted",
            "rejected" => "Rejected",
            _ => null
        };
        if (status is null)
        {
            ModelState.AddModelError(nameof(bookingrequest.Status), "Choose Pending, Accepted, or Rejected.");
            return View(existing);
        }
        existing.Status = status;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // GET: BOOKINGREQUESTS/Delete/5
    public async Task<IActionResult> Delete(System.Guid? id)
    {
        if (!CurrentProfileId(out var profileId)) return Unauthorized();
        if (id == null)
        {
            return NotFound();
        }

        var bookingrequest = await _context.BookingRequests.Include(request => request.Housing)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (bookingrequest == null)
        {
            return NotFound();
        }
        if (!CanView(bookingrequest, profileId)) return Forbid();

        return View(bookingrequest);
    }

    // POST: BOOKINGREQUESTS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(System.Guid? id)
    {
        if (!CurrentProfileId(out var profileId)) return Unauthorized();
        var bookingrequest = await _context.BookingRequests.Include(request => request.Housing)
            .FirstOrDefaultAsync(request => request.Id == id);
        if (bookingrequest != null)
        {
            if (!CanView(bookingrequest, profileId)) return Forbid();
            _context.BookingRequests.Remove(bookingrequest);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool CurrentProfileId(out Guid profileId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out profileId);

    private bool CanView(BookingRequest request, Guid profileId) =>
        request.ProfileId == profileId || CanManageHousing(request.Housing);

    private bool CanManageRequests() => !string.IsNullOrWhiteSpace(User.FindFirstValue("TenantId"));

    private bool CanManageHousing(Housing housing)
    {
        var tenantId = User.FindFirstValue("TenantId");
        return !string.IsNullOrWhiteSpace(tenantId) && housing.TenantId == tenantId;
    }

}
