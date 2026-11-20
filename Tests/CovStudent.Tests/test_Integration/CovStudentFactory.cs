using CovStudent.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CovStudent.Tests;

/// <summary>
/// Démarre l'application complète en mémoire, avec une base SQLite en mémoire
/// et l'authentification de test.
/// </summary>
public class CovStudentFactory : WebApplicationFactory<Program>
{
    public const int NbTrajetsTunisSousse = 20;
    public const int NbTrajetsSfax = 5;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public CovStudentFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // SQL Server → SQLite en mémoire
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));

            // Cookies Identity → authentification de test
            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.Scheme;
                    options.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
                    options.DefaultChallengeScheme = TestAuthHandler.Scheme;
                    options.DefaultForbidScheme = TestAuthHandler.Scheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated();
        InitialiserDonnees(context);

        return host;
    }

    private static void InitialiserDonnees(ApplicationDbContext context)
    {
        context.Users.AddRange(
            Donnees.Utilisateur("conducteur"),
            Donnees.Utilisateur("passager"),
            Donnees.Utilisateur("admin"));

        // 20 trajets Tunis → Sousse à venir
        for (var i = 0; i < NbTrajetsTunisSousse; i++)
            context.Trips.Add(Donnees.Trajet("conducteur", depart: DateTime.Now.AddDays(1 + i)));

        // 5 trajets Sfax → Tunis à venir
        for (var i = 0; i < NbTrajetsSfax; i++)
        {
            var trajet = Donnees.Trajet("conducteur", depart: DateTime.Now.AddDays(1 + i));
            trajet.Departure = "Sfax";
            trajet.Destination = "Tunis";
            context.Trips.Add(trajet);
        }

        // 1 trajet passé : ne doit jamais apparaître dans la recherche
        context.Trips.Add(Donnees.Trajet("conducteur", depart: DateTime.Now.AddDays(-1)));

        context.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}