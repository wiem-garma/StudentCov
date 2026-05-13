using CovStudent.Data;
using CovStudent.Models;
using Microsoft.EntityFrameworkCore;

namespace CovStudent.Services;

public class TripService : ITripService
{
    private readonly ApplicationDbContext _context;

    public TripService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Trip>> GetAllTripsAsync()
        => await _context.Trips
            .Include(t => t.Driver)
            .Include(t => t.Reservations)
            .ToListAsync();

    public async Task<IEnumerable<Trip>> GetAvailableTripsAsync()
        => await _context.Trips
            .Include(t => t.Driver)
            .Include(t => t.Reservations)
            .Where(t => t.Status == TripStatus.Active)
            .ToListAsync();

    public async Task<IEnumerable<Trip>> GetTripsByDriverAsync(string driverId)
        => await _context.Trips
            .Include(t => t.Reservations)
            .ThenInclude(r => r.Passenger)
            .Where(t => t.DriverId == driverId)
            .OrderByDescending(t => t.DepartureTime)
            .ToListAsync();

    public async Task<Trip?> GetTripByIdAsync(int id)
        => await _context.Trips
            .Include(t => t.Driver)
            .Include(t => t.Reservations)
            .ThenInclude(r => r.Passenger)
            .FirstOrDefaultAsync(t => t.Id == id);

    public async Task AddTripAsync(Trip trip)
    {
        _context.Trips.Add(trip);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateTripAsync(Trip trip)
    {
        _context.Trips.Update(trip);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteTripAsync(int id)
    {
        var trip = await _context.Trips.FindAsync(id);
        if (trip != null)
        {
            _context.Trips.Remove(trip);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> HasAvailableSeatsAsync(int tripId, int requestedSeats)
    {
        var trip = await _context.Trips.FindAsync(tripId);

        if (trip == null)
            return false;

        var reservedSeats = await _context.Reservations
            .Where(r => r.TripId == tripId && r.Status != ReservationStatus.Cancelled)
            .SumAsync(r => r.NumberOfSeats);

        return trip.AvailableSeats - reservedSeats >= requestedSeats;
    }

    public async Task<IEnumerable<Trip>> SearchTripsAsync(string? departure, string? destination, DateTime? date)
    {
        var query = _context.Trips
            .Include(t => t.Driver)
            .Include(t => t.Reservations)
            .AsQueryable();

        if (!string.IsNullOrEmpty(departure))
            query = query.Where(t => t.Departure.Contains(departure));

        if (!string.IsNullOrEmpty(destination))
            query = query.Where(t => t.Destination.Contains(destination));

        if (date.HasValue)
            query = query.Where(t => t.DepartureTime.Date == date.Value.Date);

        return await query.ToListAsync();
    }
}
