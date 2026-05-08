using Microsoft.AspNetCore.Identity;

namespace CovStudent.Models;

public class AppUser : IdentityUser
{
    public string? FullName { get; set; }
    public string? StudentId { get; set; }
    public string? University { get; set; }
    public string? ProfilePicture { get; set; }
    public DateTime RegistrationDate { get; set; } = DateTime.Now;
    public double Rating { get; set; } = 0;

    // Navigation properties
    public virtual ICollection<Trip> PublishedTrips { get; set; } = new List<Trip>();
    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    public virtual ICollection<Message> SentMessages { get; set; } = new List<Message>();
    public virtual ICollection<Review> ReceivedReviews { get; set; } = new List<Review>();
}