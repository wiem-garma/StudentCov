using CovStudent.Models;

namespace CovStudent.ViewModels;

public class DashboardViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? University { get; set; }
    public DateTime MemberSince { get; set; }
    public int MyTripsCount { get; set; }
    public int MyReservationsCount { get; set; }
    public int PendingReservationsCount { get; set; }
    public int CompletedTripsCount { get; set; }
    public List<Reservation> RecentReservations { get; set; } = new();
    public string? ProfilePicture { get; set; }
}