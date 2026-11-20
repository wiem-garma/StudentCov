using CovStudent.Data;
using CovStudent.Exceptions;
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
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly IAuditService _auditService;

        public ReservationController(
            IReservationService reservationService,
            ITripService tripService,
            ApplicationDbContext context,
            INotificationService notificationService,
            IAuditService auditService)
        {
            _reservationService = reservationService;
            _tripService = tripService;
            _context = context;
            _notificationService = notificationService;
            _auditService = auditService;
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        // ================= CREATE (GET) =================

        [HttpGet]
        public async Task<IActionResult> Create(int tripId)
        {
            var trip = await _tripService.GetTripByIdAsync(tripId);

            if (trip == null)
                return NotFound();

            // RM-02 : affiché tôt pour l'ergonomie (la vraie vérification est dans le service)
            if (trip.DriverId == CurrentUserId)
            {
                TempData["Error"] = "Vous ne pouvez pas réserver votre propre trajet.";
                return RedirectToAction("Details", "Trip", new { id = tripId });
            }

            ViewBag.CurrentUser = await _context.Users.FindAsync(CurrentUserId);
            ViewBag.Trip = trip;
            return View(new Reservation { TripId = tripId, NumberOfSeats = 1 });
        }

        // ================= CREATE (POST) — TR-R1 =================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Reservation reservation)
        {
            var userId = CurrentUserId;

            if (userId == null)
                return RedirectToAction("Login", "Account");

            // Ces champs ne viennent pas du formulaire : ils sont fixés côté serveur
            ModelState.Remove(nameof(Reservation.PassengerId));
            ModelState.Remove(nameof(Reservation.TotalPrice));

            if (!ModelState.IsValid)
                return await RetourFormulaire(reservation, userId);

            reservation.PassengerId = userId;

            try
            {
                var success = await _reservationService.CreateReservationAsync(reservation);

                if (!success)
                    return NotFound();
            }
            catch (BusinessRuleException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return await RetourFormulaire(reservation, userId);
            }

            await _auditService.LogAsync(AuditActions.ReservationCreated, "Reservation",
                reservation.Id.ToString(),
                $"Trajet #{reservation.TripId}, {reservation.NumberOfSeats} place(s)");

            // Notification au conducteur
            var trip = await _tripService.GetTripByIdAsync(reservation.TripId);

            if (trip != null && !string.IsNullOrEmpty(trip.DriverId))
            {
                var passenger = await _context.Users.FindAsync(userId);

                await SendNotification(
                    trip.DriverId,
                    "Nouvelle réservation",
                    $"{passenger?.FullName ?? "Un utilisateur"} a réservé {reservation.NumberOfSeats} place(s).",
                    Url.Action("ReceivedReservations", "Dashboard"));
            }

            TempData["Success"] = "Réservation envoyée avec succès.";
            return RedirectToAction("MyReservations", "Dashboard");
        }

        // ================= CONFIRM — TR-R2 =================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int id)
        {
            var reservation = await _reservationService.GetReservationByIdAsync(id);

            if (reservation == null)
                return NotFound();

            try
            {
                await _reservationService.ConfirmReservationAsync(id, CurrentUserId!);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (BusinessRuleException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("ReceivedReservations", "Dashboard");
            }

            await _auditService.LogAsync(AuditActions.ReservationConfirmed, "Reservation", id.ToString());

            await SendNotification(
                reservation.PassengerId,
                "Réservation confirmée",
                "Votre réservation a été confirmée.",
                Url.Action("MyReservations", "Dashboard"));

            TempData["Success"] = "Réservation confirmée.";
            return RedirectToAction("ReceivedReservations", "Dashboard");
        }

        // ================= REJECT — TR-R3 =================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var reservation = await _reservationService.GetReservationByIdAsync(id);

            if (reservation == null)
                return NotFound();

            try
            {
                await _reservationService.RejectReservationAsync(id, CurrentUserId!);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (BusinessRuleException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("ReceivedReservations", "Dashboard");
            }

            await _auditService.LogAsync(AuditActions.ReservationRejected, "Reservation", id.ToString());

            await SendNotification(
                reservation.PassengerId,
                "Réservation refusée",
                "Le conducteur a refusé votre réservation.",
                Url.Action("MyReservations", "Dashboard"));

            TempData["Success"] = "Réservation refusée.";
            return RedirectToAction("ReceivedReservations", "Dashboard");
        }

        // ================= CANCEL — TR-R4 / TR-R5 =================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var reservation = await _reservationService.GetReservationByIdAsync(id);

            if (reservation == null || reservation.Trip == null)
                return NotFound();

            var userId = CurrentUserId;

            // Compatibilité avec les vues existantes : si c'est le conducteur
            // qui clique sur "Annuler", il s'agit en réalité d'un refus (TR-R3)
            if (reservation.Trip.DriverId == userId)
                return await Reject(id);

            try
            {
                await _reservationService.CancelReservationAsync(id, userId!);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (BusinessRuleException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("MyReservations", "Dashboard");
            }

            await _auditService.LogAsync(AuditActions.ReservationCancelled, "Reservation", id.ToString());

            await SendNotification(
                reservation.Trip.DriverId,
                "Réservation annulée",
                "Un passager a annulé sa réservation.",
                Url.Action("ReceivedReservations", "Dashboard"));

            TempData["Success"] = "Réservation annulée.";
            return RedirectToAction("MyReservations", "Dashboard");
        }

        // ================= HELPERS =================

        private async Task<IActionResult> RetourFormulaire(Reservation reservation, string userId)
        {
            ViewBag.CurrentUser = await _context.Users.FindAsync(userId);
            ViewBag.Trip = await _tripService.GetTripByIdAsync(reservation.TripId);
            return View(reservation);
        }

        private Task SendNotification(string userId, string title, string body, string? link)
            => _notificationService.SendAsync(userId, title, body, link);
    }
}