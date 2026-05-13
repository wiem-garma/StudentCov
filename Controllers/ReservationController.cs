using CovStudent.Data;
using CovStudent.Models;
using CovStudent.Services;
using CovStudent.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace CovStudent.Controllers
{
    [Authorize]
    public class ReservationController : Controller
    {
        private readonly IReservationService _reservationService;
        private readonly ITripService _tripService;
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationsHub> _hubContext;

        public ReservationController(
            IReservationService reservationService,
            ITripService tripService,
            ApplicationDbContext context,
            IHubContext<NotificationsHub> hubContext)
        {
            _reservationService = reservationService;
            _tripService = tripService;
            _context = context;
            _hubContext = hubContext;
        }

        // ================= CREATE =================

        public async Task<IActionResult> Create(int tripId)
        {
            var trip = await _tripService.GetTripByIdAsync(tripId);

            if (trip == null)
                return NotFound();

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
        public async Task<IActionResult> Create(Reservation reservation)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var hasSeats = await _tripService.HasAvailableSeatsAsync(
                reservation.TripId,
                reservation.NumberOfSeats
            );

            if (!hasSeats)
            {
                ModelState.AddModelError("", "Nombre de places insuffisant");
            }

            if (!ModelState.IsValid)
            {
                var trip = await _tripService.GetTripByIdAsync(reservation.TripId);
                ViewBag.Trip = trip;
                return View(reservation);
            }

            reservation.PassengerId = userId;
            reservation.ReservationDate = DateTime.Now;

            var success = await _reservationService.CreateReservationAsync(reservation);

            if (!success)
            {
                ModelState.AddModelError("", "Erreur lors de la réservation");
                return View(reservation);
            }

            // ================= NOTIFICATION DRIVER =================

            var tripEntity = await _tripService.GetTripByIdAsync(reservation.TripId);

            if (tripEntity != null && !string.IsNullOrEmpty(tripEntity.DriverId))
            {
                var passenger = await _context.Users.FindAsync(userId);

                var title = "Nouvelle réservation";
                var body = $"{passenger?.FullName ?? "Un utilisateur"} a réservé {reservation.NumberOfSeats} place(s)";
                var link = Url.Action("ReceivedReservations", "Dashboard");

                await SendNotification(tripEntity.DriverId, title, body, link);
            }

            TempData["Success"] = "Réservation envoyée avec succès";
            return RedirectToAction("MyReservations", "Dashboard");
        }

        // ================= CONFIRM =================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int id)
        {
            var reservation = await _reservationService.GetReservationByIdAsync(id);

            if (reservation == null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var trip = await _tripService.GetTripByIdAsync(reservation.TripId);

            if (trip == null || trip.DriverId != userId)
                return Forbid();

            await _reservationService.ConfirmReservationAsync(id);

            await SendNotification(
                reservation.PassengerId,
                "Réservation confirmée",
                "Votre réservation a été confirmée.",
                Url.Action("MyReservations", "Dashboard")
            );

            TempData["Success"] = "Réservation confirmée";
            return RedirectToAction("ReceivedReservations", "Dashboard");
        }

        // ================= CANCEL =================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var reservation = await _reservationService.GetReservationByIdAsync(id);

            if (reservation == null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var trip = await _tripService.GetTripByIdAsync(reservation.TripId);

            if (trip == null || (trip.DriverId != userId && reservation.PassengerId != userId))
                return Forbid();

            await _reservationService.CancelReservationAsync(id);

            if (trip.DriverId == userId)
            {
                await SendNotification(
                    reservation.PassengerId,
                    "Réservation annulée",
                    "Votre réservation a été annulée.",
                    Url.Action("MyReservations", "Dashboard")
                );

                TempData["Success"] = "Réservation annulée";
                return RedirectToAction("ReceivedReservations", "Dashboard");
            }

            await SendNotification(
                trip.DriverId,
                "Réservation annulée",
                "Un passager a annulé sa réservation.",
                Url.Action("ReceivedReservations", "Dashboard")
            );

            TempData["Success"] = "Réservation annulée";
            return RedirectToAction("MyReservations", "Dashboard");
        }

        // ================= HELPER =================

        private async Task SendNotification(string userId, string title, string body, string? link)
        {
            var notif = new Notification
            {
                RecipientId = userId,
                Title = title,
                Body = body,
                Link = link,
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _context.Notifications.Add(notif);
            await _context.SaveChangesAsync();

            await _hubContext.Clients.User(userId)
                .SendAsync("ReceiveNotification", new
                {
                    title,
                    body,
                    link
                });
        }
    }
}
