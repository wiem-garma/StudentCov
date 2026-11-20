using CovStudent.Data;
using CovStudent.Exceptions;
using CovStudent.Models;
using CovStudent.Services;

namespace CovStudent.Tests;

[Trait("Categorie", "Unitaire")]
public class ReservationServiceTests : IDisposable
{
    private const string Conducteur = "conducteur";
    private const string Passager = "passager";
    private const string Passager2 = "passager2";

    private readonly TestDatabase _db = new();

    public ReservationServiceTests()
    {
        using var ctx = _db.CreateContext();
        ctx.Users.AddRange(
            Donnees.Utilisateur(Conducteur),
            Donnees.Utilisateur(Passager),
            Donnees.Utilisateur(Passager2));
        ctx.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ---------- Helpers ----------

    private int CreerTrajet(int places = 3, DateTime? depart = null, TripStatus statut = TripStatus.Active)
    {
        using var ctx = _db.CreateContext();
        var trajet = Donnees.Trajet(Conducteur, places, depart, statut);
        ctx.Trips.Add(trajet);
        ctx.SaveChanges();
        return trajet.Id;
    }

    private int AjouterReservation(int tripId, string passagerId, ReservationStatus statut, int places = 1)
    {
        using var ctx = _db.CreateContext();
        var reservation = new Reservation
        {
            TripId = tripId,
            PassengerId = passagerId,
            NumberOfSeats = places,
            TotalPrice = 10m * places,
            Status = statut
        };
        ctx.Reservations.Add(reservation);
        ctx.SaveChanges();
        return reservation.Id;
    }

    private static Reservation Demande(int tripId, string passagerId, int places = 1)
        => new() { TripId = tripId, PassengerId = passagerId, NumberOfSeats = places };

    private async Task<BusinessRuleException> ReserverDoitEchouer(Reservation demande)
    {
        using var ctx = _db.CreateContext();
        return await Assert.ThrowsAsync<BusinessRuleException>(
            () => new ReservationService(ctx).CreateReservationAsync(demande));
    }

    // ---------- Création : cas nominal ----------

    [Fact]
    public async Task RM04_Reservation_valide_calcule_le_prix_cote_serveur_et_passe_en_attente()
    {
        var tripId = CreerTrajet();
        var demande = Demande(tripId, Passager, places: 2);
        demande.TotalPrice = 999m; // tentative de prix falsifié

        using var ctx = _db.CreateContext();
        var ok = await new ReservationService(ctx).CreateReservationAsync(demande);

        Assert.True(ok);
        Assert.Equal(20m, demande.TotalPrice);           // 2 × 10 TND
        Assert.Equal(ReservationStatus.Pending, demande.Status);
    }

    // ---------- RM-01 : places ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task RM01_Nombre_de_places_inferieur_a_1_est_refuse(int places)
    {
        var tripId = CreerTrajet();

        var ex = await ReserverDoitEchouer(Demande(tripId, Passager, places));

        Assert.Equal("RM-01", ex.RuleId);
    }

    [Fact]
    public async Task RM01_Plus_de_places_que_disponibles_est_refuse()
    {
        var tripId = CreerTrajet(places: 2);
        AjouterReservation(tripId, Passager, ReservationStatus.Confirmed, places: 2);

        var ex = await ReserverDoitEchouer(Demande(tripId, Passager2, places: 1));

        Assert.Equal("RM-01", ex.RuleId);
    }

    [Theory]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.Rejected)]
    public async Task RM01_Reservation_annulee_ou_refusee_libere_les_places(ReservationStatus statut)
    {
        var tripId = CreerTrajet(places: 2);
        AjouterReservation(tripId, Passager, statut, places: 2);

        using var ctx = _db.CreateContext();
        var ok = await new ReservationService(ctx).CreateReservationAsync(Demande(tripId, Passager2, places: 2));

        Assert.True(ok);
    }

    // ---------- RM-02 : conducteur ----------

    [Fact]
    public async Task RM02_Le_conducteur_ne_peut_pas_reserver_son_propre_trajet()
    {
        var tripId = CreerTrajet();

        var ex = await ReserverDoitEchouer(Demande(tripId, Conducteur));

        Assert.Equal("RM-02", ex.RuleId);
    }

    // ---------- RM-03 : double réservation ----------

    [Fact]
    public async Task RM03_Deux_reservations_actives_sur_le_meme_trajet_sont_refusees()
    {
        var tripId = CreerTrajet();
        AjouterReservation(tripId, Passager, ReservationStatus.Pending);

        var ex = await ReserverDoitEchouer(Demande(tripId, Passager));

        Assert.Equal("RM-03", ex.RuleId);
    }

    // ---------- RM-05 : trajet disponible ----------

    [Fact]
    public async Task RM05_Reservation_sur_trajet_annule_est_refusee()
    {
        var tripId = CreerTrajet(statut: TripStatus.Cancelled);

        var ex = await ReserverDoitEchouer(Demande(tripId, Passager));

        Assert.Equal("RM-05", ex.RuleId);
    }

    [Fact]
    public async Task RM05_Reservation_sur_trajet_deja_parti_est_refusee()
    {
        var tripId = CreerTrajet(depart: DateTime.Now.AddHours(-1));

        var ex = await ReserverDoitEchouer(Demande(tripId, Passager));

        Assert.Equal("RM-05", ex.RuleId);
    }

    // ---------- RM-06 : délai d'annulation ----------

    [Fact]
    public async Task RM06_Annuler_une_reservation_confirmee_moins_de_2h_avant_est_refuse()
    {
        var tripId = CreerTrajet(depart: DateTime.Now.AddHours(1));
        var reservationId = AjouterReservation(tripId, Passager, ReservationStatus.Confirmed);

        using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new ReservationService(ctx).CancelReservationAsync(reservationId, Passager));

        Assert.Equal("RM-06", ex.RuleId);
    }

    [Fact]
    public async Task RM06_Annuler_une_reservation_confirmee_plus_de_2h_avant_est_accepte()
    {
        var tripId = CreerTrajet(depart: DateTime.Now.AddHours(5));
        var reservationId = AjouterReservation(tripId, Passager, ReservationStatus.Confirmed);

        using var ctx = _db.CreateContext();
        var ok = await new ReservationService(ctx).CancelReservationAsync(reservationId, Passager);

        Assert.True(ok);
        Assert.Equal(ReservationStatus.Cancelled, (await ctx.Reservations.FindAsync(reservationId))!.Status);
    }

    // ---------- RM-10 : workflow ----------

    [Theory]
    [InlineData(ReservationStatus.Pending, ReservationStatus.Confirmed, true)]
    [InlineData(ReservationStatus.Pending, ReservationStatus.Rejected, true)]
    [InlineData(ReservationStatus.Pending, ReservationStatus.Cancelled, true)]
    [InlineData(ReservationStatus.Confirmed, ReservationStatus.Cancelled, true)]
    [InlineData(ReservationStatus.Confirmed, ReservationStatus.Completed, true)]
    [InlineData(ReservationStatus.Pending, ReservationStatus.Completed, false)]
    [InlineData(ReservationStatus.Cancelled, ReservationStatus.Confirmed, false)]
    [InlineData(ReservationStatus.Rejected, ReservationStatus.Confirmed, false)]
    [InlineData(ReservationStatus.Completed, ReservationStatus.Cancelled, false)]
    public void RM10_Table_des_transitions(ReservationStatus depuis, ReservationStatus vers, bool attendu)
    {
        Assert.Equal(attendu, ReservationService.TransitionAutorisee(depuis, vers));
    }

    [Fact]
    public async Task RM10_Confirmer_une_reservation_annulee_est_refuse()
    {
        var tripId = CreerTrajet();
        var reservationId = AjouterReservation(tripId, Passager, ReservationStatus.Cancelled);

        using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new ReservationService(ctx).ConfirmReservationAsync(reservationId, Conducteur));

        Assert.Equal("RM-10", ex.RuleId);
    }

    // ---------- Sécurité : droits ----------

    [Fact]
    public async Task SEC_Seul_le_conducteur_du_trajet_peut_confirmer()
    {
        var tripId = CreerTrajet();
        var reservationId = AjouterReservation(tripId, Passager, ReservationStatus.Pending);

        using var ctx = _db.CreateContext();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => new ReservationService(ctx).ConfirmReservationAsync(reservationId, Passager2));
    }

    [Fact]
    public async Task SEC_Un_passager_ne_peut_pas_annuler_la_reservation_d_un_autre()
    {
        var tripId = CreerTrajet();
        var reservationId = AjouterReservation(tripId, Passager, ReservationStatus.Pending);

        using var ctx = _db.CreateContext();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => new ReservationService(ctx).CancelReservationAsync(reservationId, Passager2));
    }
}