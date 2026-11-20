using System.Diagnostics.CodeAnalysis;
using CovStudent.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CovStudent.Data;

/// <summary>
/// Remplit la base avec des données de démonstration en volume
/// (recherche à fort volume + tests de performance).
/// Lancement : dotnet run -- --seed
/// </summary>
[SuppressMessage("Security", "CA5394:Do not use insecure randomness",
    Justification = "Données de démonstration uniquement : un générateur déterministe (graine fixe 42) est voulu " +
                    "pour des tests reproductibles. Aucun usage lié à la sécurité.")]
public static class DataSeeder
{
    private const int NbEtudiants = 60;
    private const int NbTrajets = 5000;

    private static readonly string[] Villes =
    {
        "Tunis", "Ariana", "Ben Arous", "La Manouba", "Bizerte", "Nabeul", "Hammamet",
        "Sousse", "Monastir", "Mahdia", "Sfax", "Kairouan", "Gabès", "Gafsa",
        "Béja", "Jendouba", "Le Kef", "Médenine", "Tozeur", "Zaghouan"
    };

    private static readonly string[] Universites =
    {
        "Université de Tunis El Manar", "Université de Carthage", "Université de Sousse",
        "Université de Monastir", "Université de Sfax", "Université de la Manouba"
    };

    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await context.Database.MigrateAsync();

        // Graine fixe : les mêmes données à chaque exécution (tests reproductibles)
        var rnd = new Random(42);

        // ---------- 1. Compte administrateur ----------
        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));

        var adminEmail = config["Seed:AdminEmail"] ?? "admin@covstudent.tn";
        var adminPassword = config["Seed:AdminPassword"]
            ?? throw new InvalidOperationException(
                "Mot de passe admin manquant. Exécutez : dotnet user-secrets set \"Seed:AdminPassword\" \"VotreMotDePasse\"");

        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Admin",
                LastName = "CovStudent",
                EmailConfirmed = true
            };

            await CreerUtilisateur(userManager, admin, adminPassword);
            await userManager.AddToRoleAsync(admin, "Admin");
            logger.LogInformation("Compte admin créé : {Email}", adminEmail);
        }

        // ---------- 2. Étudiants de démonstration ----------
        var etudiantPassword = config["Seed:StudentPassword"] ?? adminPassword;
        var etudiantsIds = new List<string>();

        for (var i = 1; i <= NbEtudiants; i++)
        {
            var email = $"etudiant{i:D3}@covstudent.tn";
            var etudiant = await userManager.FindByEmailAsync(email);

            if (etudiant == null)
            {
                etudiant = new AppUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = $"Etudiant{i:D3}",
                    LastName = "Demo",
                    University = Universites[rnd.Next(Universites.Length)],
                    City = Villes[rnd.Next(Villes.Length)],
                    EmailConfirmed = true
                };

                await CreerUtilisateur(userManager, etudiant, etudiantPassword);
            }

            etudiantsIds.Add(etudiant.Id);
        }

        logger.LogInformation("{Nb} étudiants de démonstration prêts", etudiantsIds.Count);

        // ---------- 3. Trajets ----------
        var existants = await context.Trips.CountAsync();

        if (existants >= NbTrajets)
        {
            logger.LogInformation("Déjà {Nb} trajets en base : rien à ajouter", existants);
            return;
        }

        var maintenant = DateTime.Now;
        var trajets = new List<Trip>();

        for (var i = existants; i < NbTrajets; i++)
        {
            var depart = Villes[rnd.Next(Villes.Length)];
            string destination;
            do { destination = Villes[rnd.Next(Villes.Length)]; } while (destination == depart);

            var heureDepart = maintenant.Date
                .AddDays(rnd.Next(1, 61))
                .AddHours(rnd.Next(6, 21))
                .AddMinutes(rnd.Next(0, 4) * 15);

            trajets.Add(new Trip
            {
                Departure = depart,
                Destination = destination,
                DepartureTime = heureDepart,
                ArrivalTime = heureDepart.AddMinutes(rnd.Next(45, 300)),
                AvailableSeats = rnd.Next(1, 5),
                PricePerSeat = rnd.Next(3, 26),
                MeetingPoint = $"Station centrale de {depart}",
                Description = "Trajet de démonstration (seed).",
                Status = TripStatus.Active,
                CreatedAt = maintenant,
                DriverId = etudiantsIds[rnd.Next(etudiantsIds.Count)]
            });
        }

        context.Trips.AddRange(trajets);
        await context.SaveChangesAsync();
        logger.LogInformation("{Nb} trajets ajoutés", trajets.Count);

        // ---------- 4. Réservations (sur ~30 % des trajets) ----------
        // 1 place par réservation : respecte RM-01 (chaque trajet a au moins 1 place)
        // et RM-02 (le passager n'est jamais le conducteur)
        var reservations = new List<Reservation>();

        foreach (var trajet in trajets.Where(_ => rnd.NextDouble() < 0.3))
        {
            string passagerId;
            do { passagerId = etudiantsIds[rnd.Next(etudiantsIds.Count)]; } while (passagerId == trajet.DriverId);

            reservations.Add(new Reservation
            {
                TripId = trajet.Id,
                PassengerId = passagerId,
                NumberOfSeats = 1,
                TotalPrice = trajet.PricePerSeat,
                Status = rnd.NextDouble() < 0.5 ? ReservationStatus.Pending : ReservationStatus.Confirmed,
                ReservationDate = maintenant
            });
        }

        context.Reservations.AddRange(reservations);
        await context.SaveChangesAsync();
        logger.LogInformation("{Nb} réservations ajoutées", reservations.Count);
    }

    private static async Task CreerUtilisateur(UserManager<AppUser> userManager, AppUser user, string password)
    {
        var resultat = await userManager.CreateAsync(user, password);

        if (!resultat.Succeeded)
            throw new InvalidOperationException(
                $"Création de {user.Email} impossible : " +
                string.Join(", ", resultat.Errors.Select(e => e.Description)));
    }
}