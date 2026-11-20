using CovStudent.Data;
using CovStudent.Models;
using CovStudent.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CovStudent.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly IAuditService _auditService;

    public AdminController(
        ApplicationDbContext context,
        UserManager<AppUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        ViewBag.UsersCount = await _context.Users.CountAsync();
        ViewBag.TripsCount = await _context.Trips.CountAsync();
        ViewBag.ActiveTripsCount = await _context.Trips.CountAsync(t => t.Status == TripStatus.Active);
        ViewBag.ReservationsCount = await _context.Reservations.CountAsync();
        ViewBag.PendingReservationsCount = await _context.Reservations.CountAsync(r => r.Status == ReservationStatus.Pending);
        ViewBag.MessagesCount = await _context.Messages.CountAsync();
        ViewBag.NotificationsCount = await _context.Notifications.CountAsync();

        ViewBag.RecentUsers = await _context.Users
            .OrderByDescending(u => u.RegistrationDate)
            .Take(8)
            .ToListAsync();

        ViewBag.RecentTrips = await _context.Trips
            .Include(t => t.Driver)
            .OrderByDescending(t => t.CreatedAt)
            .Take(8)
            .ToListAsync();

        ViewBag.RecentReservations = await _context.Reservations
            .Include(r => r.Passenger)
            .Include(r => r.Trip)
            .OrderByDescending(r => r.ReservationDate)
            .Take(8)
            .ToListAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUserBlock(string id)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user == null)
            return NotFound();

        // Un admin ne peut pas se bloquer lui-même
        if (user.Id == _userManager.GetUserId(User))
        {
            TempData["Error"] = "Vous ne pouvez pas bloquer votre propre compte.";
            return RedirectToAction(nameof(Dashboard));
        }

        user.IsBlocked = !user.IsBlocked;
        await _userManager.UpdateAsync(user);

        // RM-09 : un utilisateur bloqué perd ses sessions ouvertes
        if (user.IsBlocked)
            await _userManager.UpdateSecurityStampAsync(user);

        await _auditService.LogAsync(
            user.IsBlocked ? AuditActions.UserBlocked : AuditActions.UserUnblocked,
            "User", user.Id, user.Email);

        TempData["Success"] = user.IsBlocked ? "Utilisateur bloqué." : "Utilisateur débloqué.";
        return RedirectToAction(nameof(Dashboard));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelTrip(int id)
    {
        var trip = await _context.Trips
            .Include(t => t.Reservations)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (trip == null)
            return NotFound();

        // RM-10 : seul un trajet actif peut être annulé
        if (trip.Status != TripStatus.Active)
        {
            TempData["Error"] = "Seul un trajet actif peut être annulé.";
            return RedirectToAction(nameof(Dashboard));
        }

        trip.Status = TripStatus.Cancelled;

        // RM-07 : les réservations actives sont annulées aussi
        foreach (var r in trip.Reservations.Where(r =>
                     r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed))
        {
            r.Status = ReservationStatus.Cancelled;
        }

        await _context.SaveChangesAsync();

        await _auditService.LogAsync(AuditActions.TripCancelled, "Trip", id.ToString(),
            "Annulé par l'administrateur");

        TempData["Success"] = "Trajet annulé.";
        return RedirectToAction(nameof(Dashboard));
    }
}