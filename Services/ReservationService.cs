using CovStudent.Data;
using CovStudent.Models;
using Microsoft.EntityFrameworkCore;

namespace CovStudent.Services;

public class ReservationService : IReservationService
{
    private readonly ApplicationDbContext _context;

    public ReservationService(ApplicationDbContext context)
    {
        _context = context;
    }

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

    // ================= CREATE =================

    public async Task<bool> CreateReservationAsync(Reservation reservation)
    {
        var trip = await _context.Trips
            .FirstOrDefaultAsync(t => t.Id == reservation.TripId);

        if (trip == null)
            return false;

        // ❌ check seats available
        var reservedSeats = await _context.Reservations
            .Where(r => r.TripId == reservation.TripId && r.Status != ReservationStatus.Cancelled)
            .SumAsync(r => r.NumberOfSeats);

        if (trip.AvailableSeats < reservedSeats + reservation.NumberOfSeats)
            return false;

        reservation.TotalPrice = trip.PricePerSeat * reservation.NumberOfSeats;
        reservation.Status = ReservationStatus.Pending;
        reservation.ReservationDate = DateTime.Now;

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        return true;
    }

    // ================= CONFIRM =================

    public async Task<bool> ConfirmReservationAsync(int id)
    {
        var reservation = await _context.Reservations.FindAsync(id);

        if (reservation == null)
            return false;

        reservation.Status = ReservationStatus.Confirmed;
        await _context.SaveChangesAsync();
        return true;
    }

    // ================= CANCEL =================

    public async Task<bool> CancelReservationAsync(int id)
    {
        var reservation = await _context.Reservations.FindAsync(id);

        if (reservation == null)
            return false;

        reservation.Status = ReservationStatus.Cancelled;
        await _context.SaveChangesAsync();
        return true;
    }

    // ================= COMPLETE =================

    public async Task<bool> CompleteReservationAsync(int id)
    {
        var reservation = await _context.Reservations.FindAsync(id);

        if (reservation == null)
            return false;

        reservation.Status = ReservationStatus.Completed;
        await _context.SaveChangesAsync();
        return true;
    }
}
