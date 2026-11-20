using System.Data.Common;
using CovStudent.Data;
using CovStudent.Exceptions;
using CovStudent.Models;
using CovStudent.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CovStudent.Tests;

/// <summary>
/// Tests de fiabilité (ISO 25010) : concurrence, intégrité des données, pannes.
/// Base SQLite dans un fichier temporaire : chaque "utilisateur" a sa propre connexion.
/// </summary>
[Trait("Categorie", "Fiabilite")]
public class FiabiliteTests : IDisposable
{
    private static readonly string[] Passagers = { "passager1", "passager2", "passager3", "passager4", "passager5" };

    private readonly string _fichier =
        Path.Combine(Path.GetTempPath(), $"covstudent_fiabilite_{Guid.NewGuid():N}.db");

    private string ConnectionString => $"Data Source={_fichier};Pooling=False";

    public FiabiliteTests()
    {
        using var ctx = NouveauContexte();
        ctx.Database.EnsureCreated();

        ctx.Users.Add(Donnees.Utilisateur("conducteur"));
        foreach (var p in Passagers)
            ctx.Users.Add(Donnees.Utilisateur(p));

        ctx.SaveChanges();
    }

    public void Dispose()
    {
        try { File.Delete(_fichier); } catch (IOException) { /* fichier temporaire, sans importance */ }
    }

    private ApplicationDbContext NouveauContexte()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(ConnectionString).Options);

    private int CreerTrajet(int places)
    {
        using var ctx = NouveauContexte();
        var trajet = Donnees.Trajet("conducteur", places);
        ctx.Trips.Add(trajet);
        ctx.SaveChanges();
        return trajet.Id;
    }

    private void AjouterReservation(int tripId, string passager, ReservationStatus statut)
    {
        using var ctx = NouveauContexte();
        ctx.Reservations.Add(new Reservation
        {
            TripId = tripId,
            PassengerId = passager,
            NumberOfSeats = 1,
            TotalPrice = 10m,
            Status = statut
        });
        ctx.SaveChanges();
    }

    // ---------- FIA-01 : concurrence ----------

    [Fact]
    public async Task FIA01_Cinq_reservations_simultanees_sur_deux_places_seules_deux_reussissent()
    {
        var tripId = CreerTrajet(places: 2);

        async Task<bool> Reserver(string passager)
        {
            // Chaque passager a son propre contexte = sa propre connexion
            await using var ctx = NouveauContexte();
            try
            {
                return await new ReservationService(ctx).CreateReservationAsync(new Reservation
                {
                    TripId = tripId,
                    PassengerId = passager,
                    NumberOfSeats = 1
                });
            }
            catch (Exception ex) when (ex is BusinessRuleException or DbException)
            {
                return false; // refusée proprement
            }
        }

        // Les 5 demandes partent en même temps
        var resultats = await Task.WhenAll(Passagers.Select(p => Task.Run(() => Reserver(p))));

        Assert.Equal(2, resultats.Count(ok => ok));

        await using var verif = NouveauContexte();
        var placesOccupees = await verif.Reservations
            .Where(r => r.TripId == tripId &&
                        (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed))
            .SumAsync(r => r.NumberOfSeats);

        Assert.Equal(2, placesOccupees); // jamais de surréservation
    }

    // ---------- FIA-02 : intégrité au niveau base ----------

    [Fact]
    public async Task FIA02_Suppression_directe_en_base_d_un_trajet_avec_reservations_est_bloquee()
    {
        var tripId = CreerTrajet(places: 3);
        AjouterReservation(tripId, "passager1", ReservationStatus.Confirmed);

        // On contourne volontairement le service (comme le ferait un bug ou un script)
        await using (var ctx = NouveauContexte())
        {
            var trajet = await ctx.Trips.FindAsync(tripId);
            ctx.Trips.Remove(trajet!);

            await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
        }

        await using var verif = NouveauContexte();
        Assert.True(await verif.Trips.AnyAsync(t => t.Id == tripId));
        Assert.Equal(1, await verif.Reservations.CountAsync(r => r.TripId == tripId));
    }

    // ---------- FIA-03 : opération répétée ----------

    [Fact]
    public async Task FIA03_Annuler_deux_fois_un_trajet_est_refuse_et_ne_modifie_pas_les_donnees()
    {
        var tripId = CreerTrajet(places: 3);
        AjouterReservation(tripId, "passager1", ReservationStatus.Confirmed);
        AjouterReservation(tripId, "passager2", ReservationStatus.Pending);

        await using (var ctx = NouveauContexte())
        {
            Assert.True(await new TripService(ctx).CancelTripAsync(tripId, "conducteur"));
        }

        await using (var ctx = NouveauContexte())
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => new TripService(ctx).CancelTripAsync(tripId, "conducteur"));
            Assert.Equal("RM-10", ex.RuleId);
        }

        await using var verif = NouveauContexte();
        Assert.Equal(TripStatus.Cancelled, (await verif.Trips.FindAsync(tripId))!.Status);
        Assert.All(await verif.Reservations.Where(r => r.TripId == tripId).ToListAsync(),
                   r => Assert.Equal(ReservationStatus.Cancelled, r.Status));
    }

    // ---------- FIA-04 : panne d'un composant ----------

    [Fact]
    public async Task FIA04_Une_panne_du_journal_d_audit_ne_bloque_pas_l_application()
    {
        await using var ctx = NouveauContexte();

        // Panne simulée : la table d'audit n'existe plus
        await ctx.Database.ExecuteSqlRawAsync("DROP TABLE AuditLogs");

        var audit = new AuditService(ctx, new HttpContextAccessor(), NullLogger<AuditService>.Instance);

        var erreur = await Record.ExceptionAsync(
            () => audit.LogAsync(AuditActions.TripCreated, "Trip", "1"));

        Assert.Null(erreur); // l'échec d'audit n'est pas propagé

        // Le contexte reste utilisable : l'application continue de fonctionner
        ctx.Trips.Add(Donnees.Trajet("conducteur"));
        await ctx.SaveChangesAsync();
        Assert.Equal(1, await ctx.Trips.CountAsync());
    }
}