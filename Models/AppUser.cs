using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace CovStudent.Models;

public class AppUser : IdentityUser
{
    [Required]
    [StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? University { get; set; }

    [StringLength(20)]
    public string? StudentId { get; set; }

    public DateTime RegistrationDate { get; set; } = DateTime.Now;

    public DateTime? LastLoginDate { get; set; }

    public double Rating { get; set; } = 0;

    public bool IsBlocked { get; set; } = false;

    [StringLength(150)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    // Relations
    public virtual ICollection<Trip> PublishedTrips { get; set; } = new List<Trip>();

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public virtual ICollection<Message> SentMessages { get; set; } = new List<Message>();

    public virtual ICollection<Review> ReceivedReviews { get; set; } = new List<Review>();

    public virtual ICollection<Notification> ReceivedNotifications { get; set; } = new List<Notification>();
}