using CovStudent.Models;
namespace CovStudent.Services;

public interface ITripService
{
    IEnumerable<Trip> GetAllTrips();
    IEnumerable<Trip> GetAvailableTrips();
    IEnumerable<Trip> GetTripsByDriver(string driverId);
    IEnumerable<Trip> SearchTrips(string departure, string destination, DateTime? date);
    Trip? GetTripById(int id);
    void AddTrip(Trip trip);
    void UpdateTrip(Trip trip);
    void DeleteTrip(int id);
    bool HasAvailableSeats(int tripId, int requestedSeats);
}