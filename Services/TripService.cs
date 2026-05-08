using CovStudent.Data;
using CovStudent.Models;
using CovStudent.Services;
using Microsoft.EntityFrameworkCore;

namespace CovStudent.Services;

public class TripService : ITripService
{
    private readonly ApplicationDbContext _context;

    public TripService(ApplicationDbContext context)
    {
        _context = context;
    }

    public IEnumerable<Trip> GetAllTrips()
    {
        return _context.Trips
            .Include(t => t.Driver)
            .Include(t => t.Reservations)
            .OrderBy(t => t.DepartureTime)
            .ToList();
    }

    public IEnumerable<Trip> GetAvailableTrips()
    {
        return _context.Trips
            .Include(t => t.Driver)
            .Where(t => t.Status == "Active" &&
                       t.DepartureTime > DateTime.Now &&
                       t.AvailableSeats > 0)
            .OrderBy(t => t.DepartureTime)
            .ToList();
    }

    public IEnumerable<Trip> GetTripsByDriver(string driverId)
    {
        return _context.Trips
            .Include(t => t.Reservations)
            .Where(t => t.DriverId == driverId)
            .OrderByDescending(t => t.CreatedAt)
            .ToList();
    }

    public IEnumerable<Trip> SearchTrips(string departure, string destination, DateTime? date)
    {
        var query = _context.Trips
            .Include(t => t.Driver)
            .Where(t => t.Status == "Active" && t.AvailableSeats > 0);

        if (!string.IsNullOrEmpty(departure))
            query = query.Where(t => t.Departure.Contains(departure));

        if (!string.IsNullOrEmpty(destination))
            query = query.Where(t => t.Destination.Contains(destination));

        if (date.HasValue)
            query = query.Where(t => t.DepartureTime.Date == date.Value.Date);

        return query.OrderBy(t => t.DepartureTime).ToList();
    }

    public Trip? GetTripById(int id)
    {
        return _context.Trips
            .Include(t => t.Driver)
            .Include(t => t.Reservations)
            .ThenInclude(r => r.Passenger)
            .FirstOrDefault(t => t.Id == id);
    }

    public void AddTrip(Trip trip)
    {
        _context.Trips.Add(trip);
        _context.SaveChanges();
    }

    public void UpdateTrip(Trip trip)
    {
        _context.Trips.Update(trip);
        _context.SaveChanges();
    }

    public void DeleteTrip(int id)
    {
        var trip = _context.Trips.Find(id);
        if (trip != null)
        {
            _context.Trips.Remove(trip);
            _context.SaveChanges();
        }
    }

    public bool HasAvailableSeats(int tripId, int requestedSeats)
    {
        var trip = _context.Trips.Find(tripId);
        if (trip == null) return false;

        var reservedSeats = _context.Reservations
            .Where(r => r.TripId == tripId && r.Status == "Confirmed")
            .Sum(r => r.NumberOfSeats);

        return (trip.AvailableSeats - reservedSeats) >= requestedSeats;
    }
}