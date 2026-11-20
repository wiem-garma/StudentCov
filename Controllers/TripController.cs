using CovStudent.Data;
using CovStudent.Exceptions;
using CovStudent.Models;
using CovStudent.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CovStudent.Controllers;

public class TripController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ITripService _tripService;
    private readonly INotificationService _notificationService;
    private readonly IAuditService _auditService;

    public TripController(
        ApplicationDbContext context,
        ITripService tripService,
        INotificationService notificationService,
        IAuditService auditService)
    {
        _context = context;
        _tripService = tripService;
        _notificationService = notificationService;
        _auditService = auditService;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    // ================= RECHERCHE (paginée) =================

    [HttpGet]
    public async Task<IActionResult> Index(string? departure, string? destination, DateTime? date, int page = 1)
    {
        var resultat = await _tripService.SearchTripsAsync(departure, destination, date, page);

        ViewBag.Departure = departure;
        ViewBag.Destination = destination;
        ViewBag.Date = date?.ToString("yyyy-MM-dd");
        ViewBag.Page = resultat.Page;
        ViewBag.TotalPages = resultat.TotalPages;
        ViewBag.TotalCount = resultat.TotalCount;

        return View(resultat.Items);
    }

    // ================= SUGGESTIONS =================

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetSmartSuggestions()
    {
        var currentUserId = CurrentUserId;
        if (currentUserId == null)
            return Json(new { success = false });

        var user = await _context.Users.FindAsync(currentUserId);
        var university = user?.University;
        var hasUniversity = !string.IsNullOrWhiteSpace(university);

        var pastDestinations = await _context.Reservations
            .Where(r => r.PassengerId == currentUserId && r.Trip != null)
            .Select(r => r.Trip!.Destination)
            .Distinct()
            .ToListAsync();

        var suggestionsList = await _context.Trips
            .AsNoTracking()
            .Include(t => t.Driver)
            .Where(t => t.Status == TripStatus.Active
                && t.DepartureTime > DateTime.Now
                && t.DriverId != currentUserId)
            .Where(t => (hasUniversity && t.Destination.Contains(university!)) ||
                        pastDestinations.Contains(t.Destination))
            .OrderBy(t => t.DepartureTime)
            .Take(3)
            .Select(t => new
            {
                id = t.Id,
                route = t.Departure + " -> " + t.Destination,
                time = t.DepartureTime,
                driver = t.Driver != null ? t.Driver.FirstName + " " + t.Driver.LastName : "Inconnu",
                reason = hasUniversity && t.Destination.Contains(university!)
                    ? "Trajet vers votre université"
                    : "Basé sur vos trajets précédents"
            })
            .ToListAsync();

        // Formatage de la date en mémoire (pas traduisible en SQL)
        var data = suggestionsList.Select(s => new
        {
            s.id,
            s.route,
            time = s.time.ToString("dd/MM HH:mm"),
            s.driver,
            s.reason
        });

        return Json(new { success = true, data });
    }

    // ================= CREATE — TR-T1 =================

    [HttpGet]
    [Authorize]
    public IActionResult Create()
    {
        return View(new Trip { DepartureTime = DateTime.Now.AddHours(1), AvailableSeats = 1 });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Trip trip)
    {
        // Fixé côté serveur, jamais depuis le formulaire
        ModelState.Remove(nameof(Trip.DriverId));

        if (!ModelState.IsValid)
            return View(trip);

        trip.DriverId = CurrentUserId!;

        try
        {
            await _tripService.AddTripAsync(trip);
        }
        catch (BusinessRuleException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(trip);
        }

        await _auditService.LogAsync(AuditActions.TripCreated, "Trip",
            trip.Id.ToString(), $"{trip.Departure} → {trip.Destination}");

        TempData["Success"] = "Trajet publié avec succès.";
        return RedirectToAction("MyTrips", "Dashboard");
    }

    // ================= DETAILS =================

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var trip = await _tripService.GetTripByIdAsync(id);

        if (trip == null)
            return NotFound();

        var currentUserId = CurrentUserId;

        ViewBag.CurrentUserId = currentUserId;
        ViewBag.UserReservation = trip.Reservations
            .FirstOrDefault(r => r.PassengerId == currentUserId &&
                                 r.Status != ReservationStatus.Cancelled &&
                                 r.Status != ReservationStatus.Rejected);

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

    // ================= EDIT =================

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Edit(int id)
    {
        var trip = await _tripService.GetTripByIdAsync(id);

        if (trip == null || trip.DriverId != CurrentUserId)
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

        ModelState.Remove(nameof(Trip.DriverId));

        if (!ModelState.IsValid)
            return View(trip);

        try
        {
            var success = await _tripService.UpdateTripAsync(trip, CurrentUserId!);

            if (!success)
                return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (BusinessRuleException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(trip);
        }

        await _auditService.LogAsync(AuditActions.TripUpdated, "Trip", id.ToString());

        TempData["Success"] = "Trajet mis à jour.";
        return RedirectToAction("MyTrips", "Dashboard");
    }

    // ================= CANCEL — TR-T3 + RM-07 =================

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var trip = await _tripService.GetTripByIdAsync(id);

        if (trip == null)
            return NotFound();

        // Passagers à prévenir (réservations encore actives avant l'annulation)
        var passagersConcernes = trip.Reservations
            .Where(r => r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
            .Select(r => r.PassengerId)
            .Distinct()
            .ToList();

        try
        {
            await _tripService.CancelTripAsync(id, CurrentUserId!);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (BusinessRuleException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("MyTrips", "Dashboard");
        }

        await _auditService.LogAsync(AuditActions.TripCancelled, "Trip",
            id.ToString(), $"{passagersConcernes.Count} passager(s) prévenu(s)");

        foreach (var passagerId in passagersConcernes)
        {
            await _notificationService.SendAsync(
                passagerId,
                "Trajet annulé",
                $"Le trajet {trip.Departure} → {trip.Destination} du {trip.DepartureTime:dd/MM HH:mm} a été annulé.",
                Url.Action("MyReservations", "Dashboard"));
        }

        TempData["Success"] = "Trajet annulé. Les passagers ont été prévenus.";
        return RedirectToAction("MyTrips", "Dashboard");
    }

    // ================= COMPLETE — TR-T2 + TR-R6 =================

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        try
        {
            var success = await _tripService.CompleteTripAsync(id, CurrentUserId!);

            if (!success)
                return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (BusinessRuleException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("MyTrips", "Dashboard");
        }

        await _auditService.LogAsync(AuditActions.TripCompleted, "Trip", id.ToString());

        TempData["Success"] = "Trajet marqué comme terminé.";
        return RedirectToAction("MyTrips", "Dashboard");
    }

    // ================= DELETE =================

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var trip = await _tripService.GetTripByIdAsync(id);

        if (trip == null || trip.DriverId != CurrentUserId)
            return NotFound();

        return View(trip);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            var success = await _tripService.DeleteTripAsync(id, CurrentUserId!);

            if (!success)
                return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (BusinessRuleException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("MyTrips", "Dashboard");
        }

        await _auditService.LogAsync(AuditActions.TripDeleted, "Trip", id.ToString());

        TempData["Success"] = "Trajet supprimé.";
        return RedirectToAction("MyTrips", "Dashboard");
    }
}