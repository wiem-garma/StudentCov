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

    public IEnumerable<Reservation> GetReservationsByPassenger(string passengerId)
    {
        return _context.Reservations
            .Include(r => r.Trip)
            .ThenInclude(t => t!.Driver)
            .Where(r => r.PassengerId == passengerId)
            .OrderByDescending(r => r.ReservationDate)
            .ToList();
    }

    public IEnumerable<Reservation> GetReservationsByTrip(int tripId)
    {
        return _context.Reservations
            .Include(r => r.Passenger)
            .Where(r => r.TripId == tripId)
            .ToList();
    }

    public Reservation? GetReservationById(int id)
    {
        return _context.Reservations
            .Include(r => r.Trip)
            .Include(r => r.Passenger)
            .FirstOrDefault(r => r.Id == id);
    }

    public bool CreateReservation(Reservation reservation)
    {
        var trip = _context.Trips.Find(reservation.TripId);
        if (trip == null) return false;

        reservation.TotalPrice = trip.PricePerSeat * reservation.NumberOfSeats;
        reservation.Status = "Pending";

        _context.Reservations.Add(reservation);
        _context.SaveChanges();
        return true;
    }

    public void ConfirmReservation(int id)
    {
        var reservation = _context.Reservations.Find(id);
        if (reservation != null)
        {
            reservation.Status = "Confirmed";
            _context.SaveChanges();
        }
    }

    public void CancelReservation(int id)
    {
        var reservation = _context.Reservations.Find(id);
        if (reservation != null)
        {
            reservation.Status = "Cancelled";
            _context.SaveChanges();
        }
    }

    public void CompleteReservation(int id)
    {
        var reservation = _context.Reservations.Find(id);
        if (reservation != null)
        {
            reservation.Status = "Completed";
            _context.SaveChanges();
        }
    }
}