using CovStudent.Models;

namespace CovStudent.Services;

public interface ITripService
{
    Task<IEnumerable<Trip>> GetAllTripsAsync();
    Task<IEnumerable<Trip>> GetAvailableTripsAsync();
    Task<IEnumerable<Trip>> GetTripsByDriverAsync(string driverId);
    Task<IEnumerable<Trip>> SearchTripsAsync(string? departure, string? destination, DateTime? date);
    Task<Trip?> GetTripByIdAsync(int id);

    Task AddTripAsync(Trip trip);
    Task<bool> UpdateTripAsync(Trip trip, string driverId);
    Task<bool> CancelTripAsync(int id, string driverId);
    Task<bool> CompleteTripAsync(int id, string driverId);
    Task<bool> DeleteTripAsync(int id, string driverId);

    Task<bool> HasAvailableSeatsAsync(int tripId, int requestedSeats);
}