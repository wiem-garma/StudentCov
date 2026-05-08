using Microsoft.AspNetCore.Mvc;
using CovStudent.Data;
using CovStudent.Models;
using Microsoft.EntityFrameworkCore;

namespace CovStudent.Controllers;

public class TripController : Controller
{
    private readonly ApplicationDbContext _context;

    public TripController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Afficher tous les trajets
    public async Task<IActionResult> Index()
    {
        var trips = await _context.Trips
            .Include(t => t.Driver)
            .OrderBy(t => t.DepartureTime)
            .ToListAsync();
        return View(trips);
    }

    // Formulaire de création
    public IActionResult Create()
    {
        return View();
    }

    // Sauvegarder un nouveau trajet
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Trip trip)
    {
        if (ModelState.IsValid)
        {
            trip.CreatedAt = DateTime.Now;
            trip.Status = "Active";
            // Pour l'instant, on utilise un ID temporaire
            trip.DriverId = "temp-user-id";

            _context.Add(trip);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(trip);
    }

    // Détails d'un trajet
    public async Task<IActionResult> Details(int id)
    {
        var trip = await _context.Trips
            .Include(t => t.Driver)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (trip == null)
        {
            return NotFound();
        }
        return View(trip);
    }

    // Formulaire d'édition
    public async Task<IActionResult> Edit(int id)
    {
        var trip = await _context.Trips.FindAsync(id);
        if (trip == null)
        {
            return NotFound();
        }
        return View(trip);
    }

    // Sauvegarder les modifications
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Trip trip)
    {
        if (id != trip.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(trip);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TripExists(id))
                {
                    return NotFound();
                }
                throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(trip);
    }

    // Formulaire de suppression
    public async Task<IActionResult> Delete(int id)
    {
        var trip = await _context.Trips
            .FirstOrDefaultAsync(t => t.Id == id);
        if (trip == null)
        {
            return NotFound();
        }
        return View(trip);
    }

    // Confirmer la suppression
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var trip = await _context.Trips.FindAsync(id);
        if (trip != null)
        {
            _context.Trips.Remove(trip);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private bool TripExists(int id)
    {
        return _context.Trips.Any(e => e.Id == id);
    }
}