using CovStudent.Data;
using CovStudent.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CovStudent.Tests;

/// <summary>
/// Base SQLite en mémoire, recréée pour chaque classe de test.
/// Rapide, isolée, et elle supporte les transactions et les contraintes (clés étrangères).
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new ApplicationDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}

/// <summary>
/// Fabrique de données de test.
/// </summary>
public static class Donnees
{
    public static AppUser Utilisateur(string id) => new()
    {
        Id = id,
        UserName = id,
        Email = $"{id}@test.tn",
        FirstName = id,
        LastName = "Test"
    };

    public static Trip Trajet(string driverId, int places = 3, DateTime? depart = null,
                              TripStatus statut = TripStatus.Active) => new()
    {
        Departure = "Tunis",
        Destination = "Sousse",
        DepartureTime = depart ?? DateTime.Now.AddDays(1),
        AvailableSeats = places,
        PricePerSeat = 10m,
        DriverId = driverId,
        Status = statut
    };
}