using CovStudent.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CovStudent.Data;

public class ApplicationDbContext : IdentityDbContext<AppUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Decimal precision
        modelBuilder.Entity<Trip>()
            .Property(t => t.PricePerSeat)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Reservation>()
            .Property(r => r.TotalPrice)
            .HasPrecision(18, 2);

        // Trip
        modelBuilder.Entity<Trip>()
            .HasOne(t => t.Driver)
            .WithMany(u => u.PublishedTrips)
            .HasForeignKey(t => t.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        // Reservation
        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.Passenger)
            .WithMany(u => u.Reservations)
            .HasForeignKey(r => r.PassengerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.Trip)
            .WithMany(t => t.Reservations)
            .HasForeignKey(r => r.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        // Message
        modelBuilder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany(u => u.SentMessages)
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Receiver)
            .WithMany()
            .HasForeignKey(m => m.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Trip)
            .WithMany()
            .HasForeignKey(m => m.TripId)
            .OnDelete(DeleteBehavior.SetNull);

        // Review
        modelBuilder.Entity<Review>()
            .HasOne(r => r.Reviewer)
            .WithMany()
            .HasForeignKey(r => r.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.ReviewedUser)
            .WithMany(u => u.ReceivedReviews)
            .HasForeignKey(r => r.ReviewedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Notification
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Recipient)
            .WithMany(u => u.ReceivedNotifications)
            .HasForeignKey(n => n.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);

        // INDEXES
        modelBuilder.Entity<Trip>()
            .HasIndex(t => t.DepartureTime);

        modelBuilder.Entity<Trip>()
            .HasIndex(t => new { t.Departure, t.Destination });

        modelBuilder.Entity<Message>()
            .HasIndex(m => m.SentAt);

        modelBuilder.Entity<Notification>()
            .HasIndex(n => n.CreatedAt);

        modelBuilder.Entity<Reservation>()
            .HasIndex(r => r.ReservationDate);
    }
}