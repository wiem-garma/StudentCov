using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CovStudent.Models;

public class Reservation
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int TripId { get; set; }

    [Required]
    public string PassengerId { get; set; } = string.Empty;

    [Required]
    [Range(1, 4)]
    [Display(Name = "Nombre de places réservées")]
    public int NumberOfSeats { get; set; }

    public DateTime ReservationDate { get; set; } = DateTime.Now;
    public string Status { get; set; } = "Pending";
    public decimal TotalPrice { get; set; }

    [Display(Name = "Point de montée")]
    public string? PickupPoint { get; set; }

    [Display(Name = "Note du passager")]
    [Range(1, 5)]
    public int? PassengerRating { get; set; }

    [Display(Name = "Commentaire du passager")]
    public string? PassengerComment { get; set; }

    [ForeignKey("TripId")]
    public virtual Trip? Trip { get; set; }

    [ForeignKey("PassengerId")]
    public virtual AppUser? Passenger { get; set; }  // ← AppUser
}