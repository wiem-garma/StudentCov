using CovStudent.Models;

namespace CovStudent.Services;

public interface IReservationService
{
    Task<IEnumerable<Reservation>> GetReservationsByPassengerAsync(string passengerId);
    Task<IEnumerable<Reservation>> GetReservationsByTripAsync(int tripId);
    Task<IEnumerable<Reservation>> GetReservationsForDriverAsync(string driverId);
    Task<Reservation?> GetReservationByIdAsync(int id);

    Task<bool> CreateReservationAsync(Reservation reservation);

    Task<bool> ConfirmReservationAsync(int id);
    Task<bool> CancelReservationAsync(int id);
    Task<bool> CompleteReservationAsync(int id);
}
