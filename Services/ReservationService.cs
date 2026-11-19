using System.Data;
using System.Data.Common;
using CovStudent.Data;
using CovStudent.Exceptions;
using CovStudent.Models;
using Microsoft.EntityFrameworkCore;

namespace CovStudent.Services;

public class ReservationService : IReservationService
{
    // RM-06 : délai minimum avant le départ pour annuler une réservation confirmée
    public static readonly TimeSpan DelaiAnnulation = TimeSpan.FromHours(2);

    // RM-10 : transitions autorisées du workflow de réservation
    private static readonly Dictionary<ReservationStatus, ReservationStatus[]> TransitionsAutorisees = new()
    {
        [ReservationStatus.Pending]   = new[] { ReservationStatus.Confirmed, ReservationStatus.Rejected, ReservationStatus.Cancelled },
        [ReservationStatus.Confirmed] = new[] { ReservationStatus.Cancelled, ReservationStatus.Completed },
        [ReservationStatus.Cancelled] = Array.Empty<ReservationStatus>(),
        [ReservationStatus.Rejected]  = Array.Empty<ReservationStatus>(),
        [ReservationStatus.Completed] = Array.Empty<ReservationStatus>()
    };

    private readonly ApplicationDbContext _context;

    public ReservationService(ApplicationDbContext context)
    {
        _context = context;
    }

    // ================= RÈGLES =================

    public static bool TransitionAutorisee(ReservationStatus depuis, ReservationStatus vers)
        => TransitionsAutorisees.TryGetValue(depuis, out var cibles) && cibles.Contains(vers);

    private static void VerifierTransition(ReservationStatus depuis, ReservationStatus vers)
    {
        if (!TransitionAutorisee(depuis, vers))
            throw new BusinessRuleException("RM-10", $"Transition interdite : {depuis} → {vers}.");
    }

    private static bool EstActive(ReservationStatus statut)
        => statut == ReservationStatus.Pending || statut == ReservationStatus.Confirmed;

    // ================= GET BY PASSENGER =================

    public async Task<IEnumerable<Reservation>> GetReservationsByPassengerAsync(string passengerId)
    {
        return await _context.Reservations
            .Include(r => r.Trip)
            .ThenInclude(t => t!.Driver)
            .Where(r => r.PassengerId == passengerId)
            .OrderByDescending(r => r.ReservationDate)
            .ToListAsync();
    }

    // ================= GET BY TRIP =================

    public async Task<IEnumerable<Reservation>> GetReservationsByTripAsync(int tripId)
    {
        return await _context.Reservations
            .Include(r => r.Passenger)
            .Include(r => r.Trip)
            .Where(r => r.TripId == tripId)
            .OrderByDescending(r => r.ReservationDate)
            .ToListAsync();
    }

    // ================= GET BY DRIVER =================

    public async Task<IEnumerable<Reservation>> GetReservationsForDriverAsync(string driverId)
    {
        return await _context.Reservations
            .Include(r => r.Passenger)
            .Include(r => r.Trip)
            .Where(r => r.Trip != null && r.Trip.DriverId == driverId)
            .OrderByDescending(r => r.ReservationDate)
            .ToListAsync();
    }

    // ================= GET BY ID =================

    public async Task<Reservation?> GetReservationByIdAsync(int id)
    {
        return await _context.Reservations
            .Include(r => r.Trip)
            .Include(r => r.Passenger)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    // ================= CREATE (TR-R1) =================

    public async Task<bool> CreateReservationAsync(Reservation reservation)
    {
        // Serializable : deux passagers ne peuvent pas prendre la dernière place en même temps (EQ-FIA-01)
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            var trip = await _context.Trips.FirstOrDefaultAsync(t => t.Id == reservation.TripId);

            if (trip == null)
                return false;

            // RM-01 : au moins une place
            if (reservation.NumberOfSeats < 1)
                throw new BusinessRuleException("RM-01", "Le nombre de places doit être au moins 1.");

            // RM-05 : trajet actif et pas encore parti
            if (trip.Status != TripStatus.Active || trip.DepartureTime <= DateTime.Now)
                throw new BusinessRuleException("RM-05", "Ce trajet n'est plus disponible à la réservation.");

            // RM-02 : le conducteur ne réserve pas son propre trajet
            if (trip.DriverId == reservation.PassengerId)
                throw new BusinessRuleException("RM-02", "Vous ne pouvez pas réserver votre propre trajet.");

            // RM-03 : une seule réservation active par passager et par trajet
            var dejaReserve = await _context.Reservations.AnyAsync(r =>
                r.TripId == trip.Id &&
                r.PassengerId == reservation.PassengerId &&
                (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed));

            if (dejaReserve)
                throw new BusinessRuleException("RM-03", "Vous avez déjà une réservation active sur ce trajet.");

            // RM-01 : places disponibles
            var placesReservees = await _context.Reservations
                .Where(r => r.TripId == trip.Id &&
                            (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed))
                .SumAsync(r => r.NumberOfSeats);

            if (placesReservees + reservation.NumberOfSeats > trip.AvailableSeats)
                throw new BusinessRuleException("RM-01",
                    $"Places insuffisantes : il reste {trip.AvailableSeats - placesReservees} place(s).");

            // RM-04 : prix calculé côté serveur
            reservation.TotalPrice = trip.PricePerSeat * reservation.NumberOfSeats;
            reservation.Status = ReservationStatus.Pending;
            reservation.ReservationDate = DateTime.Now;

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return true;
        }
        catch (Exception ex) when (ex is DbUpdateException or DbException)
        {
            // Conflit de concurrence : la base a refusé une des deux réservations simultanées
            await transaction.RollbackAsync();
            throw new BusinessRuleException("RM-01",
                "Une autre réservation a été enregistrée au même moment. Veuillez réessayer.");
        }
    }

    // ================= CONFIRM (TR-R2) =================

    public async Task<bool> ConfirmReservationAsync(int id, string driverId)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Trip)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
            return false;

        if (reservation.Trip!.DriverId != driverId)
            throw new UnauthorizedAccessException("Seul le conducteur du trajet peut confirmer cette réservation.");

        // RM-05 : on ne confirme pas une réservation sur un trajet annulé ou déjà parti
        if (reservation.Trip.Status != TripStatus.Active || reservation.Trip.DepartureTime <= DateTime.Now)
            throw new BusinessRuleException("RM-05", "Ce trajet n'est plus actif.");

        VerifierTransition(reservation.Status, ReservationStatus.Confirmed);

        reservation.Status = ReservationStatus.Confirmed;
        await _context.SaveChangesAsync();
        return true;
    }

    // ================= REJECT (TR-R3) =================

    public async Task<bool> RejectReservationAsync(int id, string driverId)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Trip)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
            return false;

        if (reservation.Trip!.DriverId != driverId)
            throw new UnauthorizedAccessException("Seul le conducteur du trajet peut refuser cette réservation.");

        VerifierTransition(reservation.Status, ReservationStatus.Rejected);

        reservation.Status = ReservationStatus.Rejected;
        await _context.SaveChangesAsync();
        return true;
    }

    // ================= CANCEL (TR-R4 / TR-R5) =================

    public async Task<bool> CancelReservationAsync(int id, string passengerId)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Trip)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
            return false;

        if (reservation.PassengerId != passengerId)
            throw new UnauthorizedAccessException("Vous ne pouvez annuler que vos propres réservations.");

        VerifierTransition(reservation.Status, ReservationStatus.Cancelled);

        // RM-06 : une réservation confirmée s'annule au moins 2h avant le départ
        if (reservation.Status == ReservationStatus.Confirmed &&
            reservation.Trip!.DepartureTime - DateTime.Now < DelaiAnnulation)
        {
            throw new BusinessRuleException("RM-06",
                $"Une réservation confirmée ne peut plus être annulée moins de {DelaiAnnulation.TotalHours} h avant le départ.");
        }

        reservation.Status = ReservationStatus.Cancelled;
        await _context.SaveChangesAsync();
        return true;
    }

    // ================= COMPLETE (TR-R6) =================

    public async Task<bool> CompleteReservationAsync(int id)
    {
        var reservation = await _context.Reservations.FindAsync(id);

        if (reservation == null)
            return false;

        VerifierTransition(reservation.Status, ReservationStatus.Completed);

        reservation.Status = ReservationStatus.Completed;
        await _context.SaveChangesAsync();
        return true;
    }
}