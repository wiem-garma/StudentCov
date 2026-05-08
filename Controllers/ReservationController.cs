using CovStudent.Models;
using CovStudent.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CovStudent.Controllers
{
    [Authorize]
    public class ReservationController : Controller
    {
        private readonly IReservationService _reservationService;
        private readonly ITripService _tripService;

        public ReservationController(IReservationService reservationService, ITripService tripService)
        {
            _reservationService = reservationService;
            _tripService = tripService;
        }

        public IActionResult Create(int tripId)
        {
            var trip = _tripService.GetTripById(tripId);
            if (trip == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (trip.DriverId == userId)
            {
                TempData["Error"] = "Vous ne pouvez pas réserver votre propre trajet";
                return RedirectToAction("Details", "Trip", new { id = tripId });
            }

            ViewBag.Trip = trip;
            return View(new Reservation { TripId = tripId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Reservation reservation)
        {
            if (!_tripService.HasAvailableSeats(reservation.TripId, reservation.NumberOfSeats))
            {
                ModelState.AddModelError("NumberOfSeats", "Nombre de places insuffisant");
            }

            if (ModelState.IsValid)
            {
                reservation.PassengerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
                reservation.ReservationDate = DateTime.Now;

                if (_reservationService.CreateReservation(reservation))
                {
                    TempData["Success"] = "Réservation envoyée avec succès";
                    return RedirectToAction("MyReservations", "Trip");
                }
            }

            ViewBag.Trip = _tripService.GetTripById(reservation.TripId);
            return View(reservation);
        }

        public IActionResult Confirm(int id)
        {
            _reservationService.ConfirmReservation(id);
            TempData["Success"] = "Réservation confirmée";
            return RedirectToAction("MyTrips", "Trip");
        }

        public IActionResult Cancel(int id)
        {
            _reservationService.CancelReservation(id);
            TempData["Success"] = "Réservation annulée";
            return RedirectToAction("MyReservations", "Trip");
        }
    }
}
