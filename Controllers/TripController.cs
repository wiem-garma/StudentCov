using Microsoft.AspNetCore.Mvc;
using CovStudent.Data;
using CovStudent.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace CovStudent.Controllers;

public class TripController : Controller
{
    private readonly ApplicationDbContext _context;

    public TripController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? departure, string? destination, DateTime? date)
    {
        var query = _context.Trips
            .Include(t => t.Driver)
            .Include(t => t.Reservations)
            .Where(t => t.Status == TripStatus.Active && t.DepartureTime > DateTime.Now);

        if (!string.IsNullOrWhiteSpace(departure))
            query = query.Where(t => t.Departure.Contains(departure));

        if (!string.IsNullOrWhiteSpace(destination))
            query = query.Where(t => t.Destination.Contains(destination));

        if (date.HasValue)
            query = query.Where(t => t.DepartureTime.Date == date.Value.Date);

        var trips = await query.OrderBy(t => t.DepartureTime).ToListAsync();

        ViewBag.Departure = departure;
        ViewBag.Destination = destination;
        ViewBag.Date = date?.ToString("yyyy-MM-dd");

        return View(trips);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetSmartSuggestions()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null)
            return Json(new { success = false });

        var user = await _context.Users.FindAsync(currentUserId);
        var university = user?.University;
        var hasUniversity = !string.IsNullOrWhiteSpace(university);

        var pastDestinations = await _context.Reservations
            .Include(r => r.Trip)
            .Where(r => r.PassengerId == currentUserId && r.Trip != null)
            .Select(r => r.Trip!.Destination)
            .Distinct()
            .ToListAsync();

        var query = _context.Trips
            .Include(t => t.Driver)
            .Where(t => t.Status == TripStatus.Active
                && t.DepartureTime > DateTime.Now
                && t.DriverId != currentUserId);

        var suggestionsQuery = query.Where(t =>
            (hasUniversity && t.Destination.Contains(university!)) ||
            pastDestinations.Contains(t.Destination));

        var suggestionsList = await suggestionsQuery
            .OrderBy(t => t.DepartureTime)
            .Take(3)
            .Select(t => new
            {
                id = t.Id,
                route = $"{t.Departure} -> {t.Destination}",
                time = t.DepartureTime.ToString("dd/MM HH:mm"),
                driver = t.Driver != null ? t.Driver.FullName : "Inconnu",
                reason = hasUniversity && t.Destination.Contains(university!)
                    ? "Trajet vers votre universite"
                    : "Base sur vos trajets precedents"
            })
            .ToListAsync();

        return Json(new { success = true, data = suggestionsList });
    }

    [Authorize]
    public IActionResult Create()
    {
        return View(new Trip { DepartureTime = DateTime.Now.AddHours(1) });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Trip trip)
    {
        if (ModelState.IsValid)
        {
            trip.CreatedAt = DateTime.Now;
            trip.Status = TripStatus.Active;
            trip.DriverId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            _context.Add(trip);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Trajet publie avec succes.";
            return RedirectToAction("MyTrips", "Dashboard");
        }

        return View(trip);
    }

    public async Task<IActionResult> Details(int id)
    {
        var trip = await _context.Trips
            .Include(t => t.Driver)
            .Include(t => t.Reservations)
            .ThenInclude(r => r.Passenger)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (trip == null)
            return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        ViewBag.CurrentUserId = currentUserId;
        ViewBag.UserReservation = trip.Reservations
            .FirstOrDefault(r => r.PassengerId == currentUserId && r.Status != ReservationStatus.Cancelled);

        ViewBag.DriverRating = await _context.Reviews
            .Where(r => r.ReviewedUserId == trip.DriverId)
            .Select(r => (double?)r.Rating)
            .AverageAsync() ?? trip.Driver?.Rating ?? 0;

        ViewBag.DriverReviews = await _context.Reviews
            .Include(r => r.Reviewer)
            .Where(r => r.ReviewedUserId == trip.DriverId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .ToListAsync();

        ViewBag.HasReviewed = currentUserId != null && await _context.Reviews.AnyAsync(r =>
            r.TripId == id &&
            r.ReviewerId == currentUserId &&
            r.ReviewedUserId == trip.DriverId);

        return View(trip);
    }

    [Authorize]
    public async Task<IActionResult> Edit(int id)
    {
        var trip = await _context.Trips.FindAsync(id);
        if (trip == null || trip.DriverId != User.FindFirstValue(ClaimTypes.NameIdentifier))
            return NotFound();

        return View(trip);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Trip trip)
    {
        if (id != trip.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                var existing = await _context.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
                if (existing == null || existing.DriverId != User.FindFirstValue(ClaimTypes.NameIdentifier))
                    return NotFound();

                trip.DriverId = existing.DriverId;
                trip.CreatedAt = existing.CreatedAt;
                trip.Status = existing.Status;

                _context.Update(trip);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TripExists(id))
                    return NotFound();

                throw;
            }

            TempData["Success"] = "Trajet mis a jour.";
            return RedirectToAction("MyTrips", "Dashboard");
        }

        return View(trip);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        var trip = await _context.Trips
            .Include(t => t.Reservations)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (trip == null || trip.DriverId != User.FindFirstValue(ClaimTypes.NameIdentifier))
            return NotFound();

        trip.Status = TripStatus.Completed;

        foreach (var reservation in trip.Reservations.Where(r => r.Status == ReservationStatus.Confirmed))
            reservation.Status = ReservationStatus.Completed;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Trajet marque comme termine.";
        return RedirectToAction("MyTrips", "Dashboard");
    }

    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var trip = await _context.Trips
            .Include(t => t.Reservations)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (trip == null || trip.DriverId != User.FindFirstValue(ClaimTypes.NameIdentifier))
            return NotFound();

        return View(trip);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var trip = await _context.Trips.FindAsync(id);
        if (trip != null && trip.DriverId == User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            _context.Trips.Remove(trip);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Trajet supprime.";
        }

        return RedirectToAction("MyTrips", "Dashboard");
    }

    private bool TripExists(int id)
    {
        return _context.Trips.Any(e => e.Id == id);
    }
}
