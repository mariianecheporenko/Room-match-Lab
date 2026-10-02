
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using RoomMates.Models;
using RoomMates.Data;

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
        return View(await _context.BookingRequests
            .Include(request => request.Profile)
            .Include(request => request.Housing)
            .AsNoTracking()
            .ToListAsync());
    }

    // GET: BOOKINGREQUESTS/Details/5
    public async Task<IActionResult> Details(System.Guid? id)
    {
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

        return View(bookingrequest);
    }

    // GET: BOOKINGREQUESTS/Create
    public async Task<IActionResult> Create()
    {
        await PopulateCreateOptions();
        return View();
    }

    // POST: BOOKINGREQUESTS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ProfileId,HousingId,MatchScore,Status")] BookingRequest bookingrequest)
    {
        // Navigation properties are loaded from the selected IDs, not posted by the form.
        ModelState.Remove(nameof(bookingrequest.Profile));
        ModelState.Remove(nameof(bookingrequest.Housing));
        if (!await _context.Profiles.AnyAsync(profile => profile.Id == bookingrequest.ProfileId))
        {
            ModelState.AddModelError(nameof(bookingrequest.ProfileId), "Choose an existing profile.");
        }
        if (!await _context.Housings.AnyAsync(housing => housing.Id == bookingrequest.HousingId))
        {
            ModelState.AddModelError(nameof(bookingrequest.HousingId), "Choose an existing housing listing.");
        }

        if (ModelState.IsValid)
        {
            bookingrequest.Id = Guid.NewGuid();
            bookingrequest.CreatedAt = DateTime.UtcNow;
            _context.Add(bookingrequest);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        await PopulateCreateOptions(bookingrequest.ProfileId, bookingrequest.HousingId);
        return View(bookingrequest);
    }

    // GET: BOOKINGREQUESTS/Edit/5
    public async Task<IActionResult> Edit(System.Guid? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var bookingrequest = await _context.BookingRequests.FindAsync(id);
        if (bookingrequest == null)
        {
            return NotFound();
        }
        return View(bookingrequest);
    }

    // POST: BOOKINGREQUESTS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(System.Guid? id, [Bind("Id,ProfileId,Profile,HousingId,Housing,MatchScore,Status,CreatedAt")] BookingRequest bookingrequest)
    {
        if (id != bookingrequest.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(bookingrequest);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BookingRequestExists(bookingrequest.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(bookingrequest);
    }

    // GET: BOOKINGREQUESTS/Delete/5
    public async Task<IActionResult> Delete(System.Guid? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var bookingrequest = await _context.BookingRequests
            .FirstOrDefaultAsync(m => m.Id == id);
        if (bookingrequest == null)
        {
            return NotFound();
        }

        return View(bookingrequest);
    }

    // POST: BOOKINGREQUESTS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(System.Guid? id)
    {
        var bookingrequest = await _context.BookingRequests.FindAsync(id);
        if (bookingrequest != null)
        {
            _context.BookingRequests.Remove(bookingrequest);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool BookingRequestExists(System.Guid? id)
    {
        return _context.BookingRequests.Any(e => e.Id == id);
    }

    private async Task PopulateCreateOptions(Guid? selectedProfileId = null, Guid? selectedHousingId = null)
    {
        ViewBag.ProfileId = new SelectList(
            await _context.Profiles.AsNoTracking().OrderBy(profile => profile.FullName).ToListAsync(),
            "Id", "FullName", selectedProfileId);
        ViewBag.HousingId = new SelectList(
            await _context.Housings.AsNoTracking().OrderBy(housing => housing.Title).ToListAsync(),
            "Id", "Title", selectedHousingId);
    }
}
