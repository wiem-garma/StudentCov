using CovStudent.Data;
using CovStudent.Exceptions;
using CovStudent.Models;
using Microsoft.EntityFrameworkCore;

namespace CovStudent.Services;

public class TripService : ITripService
{
    public const int PlacesMax = 8;

    private readonly ApplicationDbContext _context;

    public TripService(ApplicationDbContext context)
    {
        _context = context;
    }

    // ================= RÈGLES =================

    // RM-05 + RM-11 : validation d'un trajet avant création / modification
    private static void ValiderTrajet(Trip trip)
    {
        if (trip.DepartureTime <= DateTime.Now)
            throw new BusinessRuleException("RM-05", "La date de départ doit être dans le futur.");

        if (trip.ArrivalTime.HasValue && trip.ArrivalTime <= trip.DepartureTime)
            throw new BusinessRuleException("RM-05", "L'heure d'arrivée doit être après l'heure de départ.");

        if (trip.AvailableSeats < 1 || trip.AvailableSeats > PlacesMax)
            throw new BusinessRuleException("RM-11", $"Le nombre de places doit être entre 1 et {PlacesMax}.");

        if (trip.PricePerSeat < 0)
            throw new BusinessRuleException("RM-11", "Le prix par place ne peut pas être négatif.");

        if (string.Equals(trip.Departure.Trim(), trip.Destination.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("RM-11", "Le départ et la destination doivent être différents.");
    }

    private async Task<Trip> ChargerTrajetDuConducteur(int id, string driverId)
    {
        var trip = await _context.Trips
            .Include(t => t.Reservations)
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new KeyNotFoundException("Trajet introuvable.");

        if (trip.DriverId != driverId)
            throw new UnauthorizedAccessException("Vous ne pouvez gérer que vos propres trajets.");

        return trip;
    }

    private Task<int> PlacesReserveesAsync(int tripId)
        => _context.Reservations
            .Where(r => r.TripId == tripId &&
                        (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed))
            .SumAsync(r => r.NumberOfSeats);

    // ================= LECTURE =================

    public async Task<IEnumerable<Trip>> GetAllTripsAsync()
        => await _context.Trips
            .Include(t => t.Driver)
            .Include(t => t.Reservations)
            .ToListAsync();

    public async Task<IEnumerable<Trip>> GetAvailableTripsAsync()
        => await _context.Trips
            .Include(t => t.Driver)
            .Include(t => t.Reservations)
            .Where(t => t.Status == TripStatus.Active && t.DepartureTime > DateTime.Now)
            .OrderBy(t => t.DepartureTime)
            .ToListAsync();

    public async Task<IEnumerable<Trip>> GetTripsByDriverAsync(string driverId)
        => await _context.Trips
            .Include(t => t.Reservations)
            .ThenInclude(r => r.Passenger)
            .Where(t => t.DriverId == driverId)
            .OrderByDescending(t => t.DepartureTime)
            .ToListAsync();

    public async Task<Trip?> GetTripByIdAsync(int id)
        => await _context.Trips
            .Include(t => t.Driver)
            .Include(t => t.Reservations)
            .ThenInclude(r => r.Passenger)
            .FirstOrDefaultAsync(t => t.Id == id);

    // ================= CREATE (TR-T1) =================

    public async Task AddTripAsync(Trip trip)
    {
        ValiderTrajet(trip);

        trip.Status = TripStatus.Active;
        trip.CreatedAt = DateTime.Now;

        _context.Trips.Add(trip);
        await _context.SaveChangesAsync();
    }

    // ================= UPDATE =================

    public async Task<bool> UpdateTripAsync(Trip trip, string driverId)
    {
        Trip existant;
        try
        {
            existant = await ChargerTrajetDuConducteur(trip.Id, driverId);
        }
        catch (KeyNotFoundException)
        {
            return false;
        }

        // RM-10 : seul un trajet actif est modifiable
        if (existant.Status != TripStatus.Active)
            throw new BusinessRuleException("RM-10", "Seul un trajet actif peut être modifié.");

        ValiderTrajet(trip);

        // RM-01 : on ne descend pas sous le nombre de places déjà réservées
        var placesReservees = await PlacesReserveesAsync(existant.Id);
        if (trip.AvailableSeats < placesReservees)
            throw new BusinessRuleException("RM-01",
                $"{placesReservees} place(s) déjà réservée(s) : impossible de réduire à {trip.AvailableSeats}.");

        // On copie uniquement les champs modifiables (protection contre l'over-posting :
        // le formulaire ne peut pas changer DriverId, Status ou CreatedAt)
        existant.Departure = trip.Departure;
        existant.Destination = trip.Destination;
        existant.DepartureTime = trip.DepartureTime;
        existant.ArrivalTime = trip.ArrivalTime;
        existant.AvailableSeats = trip.AvailableSeats;
        existant.PricePerSeat = trip.PricePerSeat;
        existant.Description = trip.Description;
        existant.MeetingPoint = trip.MeetingPoint;

        await _context.SaveChangesAsync();
        return true;
    }

    // ================= CANCEL (TR-T3 + RM-07) =================

    public async Task<bool> CancelTripAsync(int id, string driverId)
    {
        Trip trip;
        try
        {
            trip = await ChargerTrajetDuConducteur(id, driverId);
        }
        catch (KeyNotFoundException)
        {
            return false;
        }

        if (trip.Status != TripStatus.Active)
            throw new BusinessRuleException("RM-10", $"Transition interdite : {trip.Status} → {TripStatus.Cancelled}.");

        trip.Status = TripStatus.Cancelled;

        // RM-07 : toutes les réservations actives sont annulées
        foreach (var r in trip.Reservations.Where(r =>
                     r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed))
        {
            r.Status = ReservationStatus.Cancelled;
        }

        // Un seul SaveChanges = une seule transaction : tout passe ou rien ne passe (fiabilité)
        await _context.SaveChangesAsync();
        return true;
    }

    // ================= COMPLETE (TR-T2 + TR-R6) =================

    public async Task<bool> CompleteTripAsync(int id, string driverId)
    {
        Trip trip;
        try
        {
            trip = await ChargerTrajetDuConducteur(id, driverId);
        }
        catch (KeyNotFoundException)
        {
            return false;
        }

        if (trip.Status != TripStatus.Active)
            throw new BusinessRuleException("RM-10", $"Transition interdite : {trip.Status} → {TripStatus.Completed}.");

        if (trip.DepartureTime > DateTime.Now)
            throw new BusinessRuleException("RM-10", "Un trajet ne peut être terminé qu'après l'heure de départ.");

        trip.Status = TripStatus.Completed;

        foreach (var r in trip.Reservations)
        {
            if (r.Status == ReservationStatus.Confirmed)
                r.Status = ReservationStatus.Completed;   // TR-R6
            else if (r.Status == ReservationStatus.Pending)
                r.Status = ReservationStatus.Cancelled;   // jamais confirmée avant le départ
        }

        await _context.SaveChangesAsync();
        return true;
    }

    // ================= DELETE =================

    public async Task<bool> DeleteTripAsync(int id, string driverId)
    {
        Trip trip;
        try
        {
            trip = await ChargerTrajetDuConducteur(id, driverId);
        }
        catch (KeyNotFoundException)
        {
            return false;
        }

        // Pas de perte de données : un trajet avec réservations s'annule, il ne se supprime pas
        if (trip.Reservations.Any())
            throw new BusinessRuleException("RM-07",
                "Ce trajet a des réservations : annulez-le au lieu de le supprimer.");

        _context.Trips.Remove(trip);
        await _context.SaveChangesAsync();
        return true;
    }

    // ================= PLACES =================

    public async Task<bool> HasAvailableSeatsAsync(int tripId, int requestedSeats)
    {
        var trip = await _context.Trips.FindAsync(tripId);

        if (trip == null || requestedSeats < 1)
            return false;

        var placesReservees = await PlacesReserveesAsync(tripId);
        return trip.AvailableSeats - placesReservees >= requestedSeats;
    }

    // ================= RECHERCHE =================

    public async Task<IEnumerable<Trip>> SearchTripsAsync(string? departure, string? destination, DateTime? date)
    {
        var query = _context.Trips
            .AsNoTracking()
            .Include(t => t.Driver)
            .Include(t => t.Reservations)
            .Where(t => t.Status == TripStatus.Active && t.DepartureTime > DateTime.Now);

        if (!string.IsNullOrWhiteSpace(departure))
            query = query.Where(t => t.Departure.Contains(departure));

        if (!string.IsNullOrWhiteSpace(destination))
            query = query.Where(t => t.Destination.Contains(destination));

        if (date.HasValue)
            query = query.Where(t => t.DepartureTime.Date == date.Value.Date);

        return await query.OrderBy(t => t.DepartureTime).ToListAsync();
    }
}