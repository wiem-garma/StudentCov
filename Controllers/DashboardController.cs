using CovStudent.Models;
using CovStudent.Services;
using CovStudent.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CovStudent.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ITripService _tripService;
    private readonly IReservationService _reservationService;

    public DashboardController(
        UserManager<AppUser> userManager,
        ITripService tripService,
        IReservationService reservationService)
    {
        _userManager = userManager;
        _tripService = tripService;
        _reservationService = reservationService;
    }

    // ================= DASHBOARD =================

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        var trips = (await _tripService.GetTripsByDriverAsync(user.Id)).ToList();
        var reservations = (await _reservationService.GetReservationsByPassengerAsync(user.Id)).ToList();

        var model = new DashboardViewModel
        {
            FullName = $"{user.FirstName} {user.LastName}",
            Email = user.Email ?? string.Empty,
            University = user.University,
            MemberSince = user.RegistrationDate,

            MyTripsCount = trips.Count,
            MyReservationsCount = reservations.Count,

            PendingReservationsCount = reservations.Count(r => r.Status == ReservationStatus.Pending),
            CompletedTripsCount = trips.Count(t => t.Status == TripStatus.Completed),

            RecentReservations = reservations
                .OrderByDescending(r => r.ReservationDate)
                .Take(5)
                .ToList()
        };

        return View(model);
    }

    // ================= TRIPS =================

    public async Task<IActionResult> MyTrips()
    {
        var userId = _userManager.GetUserId(User);

        if (userId == null)
            return RedirectToAction("Login", "Account");

        var trips = await _tripService.GetTripsByDriverAsync(userId);

        return View(trips);
    }

    // ================= RESERVATIONS =================

    public async Task<IActionResult> MyReservations()
    {
        var userId = _userManager.GetUserId(User);

        if (userId == null)
            return RedirectToAction("Login", "Account");

        var reservations = await _reservationService.GetReservationsByPassengerAsync(userId);

        return View(reservations);
    }

    public async Task<IActionResult> ReceivedReservations()
    {
        var userId = _userManager.GetUserId(User);

        if (userId == null)
            return RedirectToAction("Login", "Account");

        var reservations = await _reservationService.GetReservationsForDriverAsync(userId);

        return View(reservations);
    }
}
