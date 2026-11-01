using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomMates.Data;
using RoomMates.Models;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public class HousingsMvcController(RoomMatesDbContext context) : Controller
{
    private readonly RoomMatesDbContext _context = context;

    public async Task<IActionResult> Index()
    {
        return View(await VisibleHousings()
            .OrderBy(housing => housing.Title)
            .ToListAsync());
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var housing = await VisibleHousings()
            .FirstOrDefaultAsync(item => item.Id == id);

        return housing is null ? NotFound() : View(housing);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var housing = await VisibleHousings()
            .FirstOrDefaultAsync(item => item.Id == id);
        return housing is null ? NotFound() : View(housing);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [Bind("Id,Title,City,District,Street,BuildingNumber,ApartmentNumber,Latitude,Longitude,PricePerMonth,PetPolicy,HasExistingPets,AllowsSmoking,RequiredCleanliness,RequiredSleepSchedule,RequiredPartyTolerance")] Housing input)
    {
        if (id != input.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var housing = await VisibleHousings(asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (housing is null)
        {
            return NotFound();
        }

        housing.Title = input.Title;
        housing.City = input.City;
        housing.District = input.District;
        housing.Street = input.Street;
        housing.BuildingNumber = input.BuildingNumber;
        housing.ApartmentNumber = input.ApartmentNumber;
        housing.Latitude = input.Latitude;
        housing.Longitude = input.Longitude;
        housing.PricePerMonth = input.PricePerMonth;
        housing.PetPolicy = input.PetPolicy;
        housing.HasExistingPets = input.HasExistingPets;
        housing.AllowsSmoking = input.AllowsSmoking;
        housing.RequiredCleanliness = input.RequiredCleanliness;
        housing.RequiredSleepSchedule = input.RequiredSleepSchedule;
        housing.RequiredPartyTolerance = input.RequiredPartyTolerance;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid id)
    {
        var housing = await VisibleHousings()
            .FirstOrDefaultAsync(item => item.Id == id);
        return housing is null ? NotFound() : View(housing);
    }

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Title,City,District,Street,BuildingNumber,ApartmentNumber,Latitude,Longitude,PricePerMonth,PetPolicy,HasExistingPets,AllowsSmoking,RequiredCleanliness,RequiredSleepSchedule,RequiredPartyTolerance")] Housing housing)
    {
        if (!ModelState.IsValid)
        {
            return View(housing);
        }

        housing.Id = Guid.NewGuid();
        housing.TenantId = User.FindFirstValue("TenantId");
        _context.Housings.Add(housing);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("HousingsMvc/Delete/{id:guid}")]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var housing = await VisibleHousings().FirstOrDefaultAsync(item => item.Id == id);
        if (housing is not null)
        {
            _context.Housings.Remove(housing);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private IQueryable<Housing> VisibleHousings(bool asNoTracking = true)
    {
        IQueryable<Housing> housings = _context.Housings;
        if (asNoTracking)
            housings = housings.AsNoTracking();
        var tenantId = User.FindFirstValue("TenantId");
        var isCrossUniversity = bool.TryParse(
            User.FindFirstValue("IsCrossUniversity"),
            out var parsedCrossUniversity) && parsedCrossUniversity;

        if (!isCrossUniversity && !string.IsNullOrWhiteSpace(tenantId))
            housings = housings.Where(housing => housing.TenantId == tenantId || housing.TenantId == null);

        return housings;
    }
}
