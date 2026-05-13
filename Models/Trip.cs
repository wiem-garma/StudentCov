using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CovStudent.Models;

public class Trip
{
    [Key]
    public int Id { get; set; }

    public string Departure { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;

    public DateTime DepartureTime { get; set; }
    public DateTime? ArrivalTime { get; set; }

    public int AvailableSeats { get; set; }
    public decimal PricePerSeat { get; set; }

    public string? Description { get; set; }
    public string? MeetingPoint { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public TripStatus Status { get; set; } = TripStatus.Active;

    public string DriverId { get; set; } = string.Empty;

    public AppUser? Driver { get; set; }

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

}