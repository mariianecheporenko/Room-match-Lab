
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
        var isAdmin = User.IsInRole("Admin");
        var isLandlord = User.IsInRole("Landlord");
        return View(await _context.BookingRequests
            .Include(request => request.Profile)
            .Include(request => request.Housing)
            .AsNoTracking()
            .Where(request => isAdmin || request.ProfileId == profileId ||
                (isLandlord && request.Housing.LandlordId == profileId &&
                    request.Housing.Landlord.Role == "Landlord" && request.Housing.Landlord.IsApproved))
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
        if (!await CanViewAsync(bookingrequest, profileId)) return Forbid();

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
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Landlord,Admin")]
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
        if (!await CanManageHousingAsync(bookingrequest.Housing)) return Forbid();
        return View(bookingrequest);
    }

    // POST: BOOKINGREQUESTS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Landlord,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(System.Guid? id, [Bind("Id,ProfileId,Profile,HousingId,Housing,MatchScore,Status,CreatedAt")] BookingRequest bookingrequest)
    {
        if (id != bookingrequest.Id) return NotFound();
        if (!CanManageRequests()) return Forbid();
        var existing = await _context.BookingRequests.Include(request => request.Housing)
            .FirstOrDefaultAsync(request => request.Id == id);
        if (existing is null) return NotFound();
        if (!await CanManageHousingAsync(existing.Housing)) return Forbid();
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
        if (!await CanViewAsync(bookingrequest, profileId)) return Forbid();

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
            if (!await CanViewAsync(bookingrequest, profileId)) return Forbid();
            _context.BookingRequests.Remove(bookingrequest);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool CurrentProfileId(out Guid profileId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out profileId);

    private async Task<bool> CanViewAsync(BookingRequest request, Guid profileId) =>
        User.IsInRole("Admin") || request.ProfileId == profileId ||
        (User.IsInRole("Landlord") && await CanManageHousingAsync(request.Housing));

    private bool CanManageRequests() => User.IsInRole("Admin") || User.IsInRole("Landlord");

    private async Task<bool> CanManageHousingAsync(Housing housing)
    {
        if (User.IsInRole("Admin")) return true;
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var profileId) ||
            housing.LandlordId != profileId) return false;
        return await _context.Profiles.AnyAsync(profile =>
            profile.Id == profileId && profile.Role == "Landlord" && profile.IsApproved);
    }

}
