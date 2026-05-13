using System.ComponentModel.DataAnnotations;

namespace CovStudent.Models;

public class Reservation
{
    public int Id { get; set; }

    public int TripId { get; set; }
    public string PassengerId { get; set; } = string.Empty;

    public int NumberOfSeats { get; set; }

    public DateTime ReservationDate { get; set; } = DateTime.Now;

    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    public decimal TotalPrice { get; set; }

    public Trip? Trip { get; set; }
    public AppUser? Passenger { get; set; }
}