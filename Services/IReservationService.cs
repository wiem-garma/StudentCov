using CovStudent.Models;

namespace CovStudent.Services;

public interface IReservationService
{
    Task<IEnumerable<Reservation>> GetReservationsByPassengerAsync(string passengerId);
    Task<IEnumerable<Reservation>> GetReservationsByTripAsync(int tripId);
    Task<IEnumerable<Reservation>> GetReservationsForDriverAsync(string driverId);
    Task<Reservation?> GetReservationByIdAsync(int id);

    Task<bool> CreateReservationAsync(Reservation reservation);

    Task<bool> ConfirmReservationAsync(int id, string driverId);
    Task<bool> RejectReservationAsync(int id, string driverId);
    Task<bool> CancelReservationAsync(int id, string passengerId);
    Task<bool> CompleteReservationAsync(int id);
}