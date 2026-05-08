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

    public DbSet<Trip> Trips { get; set; }
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Review> Reviews { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuration des décimales (corrige les warnings)
        modelBuilder.Entity<Reservation>()
            .Property(r => r.TotalPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Trip>()
            .Property(t => t.PricePerSeat)
            .HasPrecision(18, 2);

        // Relations - IMPORTANT: OnDeleteBehavior.Restrict pour éviter les cycles
        modelBuilder.Entity<Trip>()
            .HasOne(t => t.Driver)
            .WithMany(u => u.PublishedTrips)
            .HasForeignKey(t => t.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.Trip)
            .WithMany(t => t.Reservations)
            .HasForeignKey(r => r.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.Passenger)
            .WithMany(u => u.Reservations)
            .HasForeignKey(r => r.PassengerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Correction POUR MESSAGE - PAS de cascade !
        modelBuilder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany(u => u.SentMessages)
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);  // ← Restrict au lieu de Cascade

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Receiver)
            .WithMany()
            .HasForeignKey(m => m.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);  // ← Restrict au lieu de Cascade

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Trip)
            .WithMany()
            .HasForeignKey(m => m.TripId)
            .OnDelete(DeleteBehavior.SetNull);  // ← SetNull pour Trip

        // Configurations pour Review
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

        // Index pour optimiser les recherches
        modelBuilder.Entity<Trip>()
            .HasIndex(t => t.DepartureTime);

        modelBuilder.Entity<Trip>()
            .HasIndex(t => new { t.Departure, t.Destination });

        modelBuilder.Entity<Message>()
            .HasIndex(m => m.SentAt);
    }
}