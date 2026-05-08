using CovStudent.Models;
namespace CovStudent.Services;

public interface IReservationService
{
    IEnumerable<Reservation> GetReservationsByPassenger(string passengerId);
    IEnumerable<Reservation> GetReservationsByTrip(int tripId);
    Reservation? GetReservationById(int id);
    bool CreateReservation(Reservation reservation);
    void ConfirmReservation(int id);
    void CancelReservation(int id);
    void CompleteReservation(int id);
}