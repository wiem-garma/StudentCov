using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CovStudent.Models;

public class Trip
{
    [Key]
    public int Id { get; set; }

    [Required(ErrorMessage = "Le départ est requis")]
    [Display(Name = "Lieu de départ")]
    public string Departure { get; set; } = string.Empty;

    [Required(ErrorMessage = "La destination est requise")]
    [Display(Name = "Destination")]
    public string Destination { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Date et heure de départ")]
    public DateTime DepartureTime { get; set; }

    [Display(Name = "Date d'arrivée estimée")]
    public DateTime? ArrivalTime { get; set; }

    [Required]
    [Range(1, 8)]
    [Display(Name = "Nombre de places")]
    public int AvailableSeats { get; set; }

    [Required]
    [Range(0, 1000)]
    [Display(Name = "Prix par place (TND)")]
    public decimal PricePerSeat { get; set; }

    [Display(Name = "Description")]
    [StringLength(500)]
    public string? Description { get; set; }

    [Display(Name = "Points de rencontre")]
    public string? MeetingPoint { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Status { get; set; } = "Active";
    public string DriverId { get; set; } = string.Empty;

    [Display(Name = "Véhicule")]
    public string? VehicleInfo { get; set; }

    [ForeignKey("DriverId")]
    public virtual AppUser? Driver { get; set; }  // ← AppUser

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}