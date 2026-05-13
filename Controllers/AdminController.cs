using CovStudent.Data;
using CovStudent.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CovStudent.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;

    public AdminController(ApplicationDbContext context)
    {
        _context = context;
    }

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
        var user = await _context.Users.FindAsync(id);

        if (user != null)
        {
            user.IsBlocked = !user.IsBlocked;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Dashboard));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelTrip(int id)
    {
        var trip = await _context.Trips.FindAsync(id);

        if (trip != null)
        {
            trip.Status = TripStatus.Cancelled;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Dashboard));
    }
}
