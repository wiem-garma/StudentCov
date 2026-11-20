using CovStudent.Data;
using CovStudent.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CovStudent.Controllers;

[Authorize]
public class ReviewController : Controller
{
    private readonly ApplicationDbContext _context;

    public ReviewController(ApplicationDbContext context)
    {
        _context = context;
    }

    // RM-08 : avis uniquement après une réservation confirmée/terminée, une seule fois, note de 1 à 5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int tripId, string reviewedUserId, int rating, string? comment)
    {
        var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (reviewerId == null)
            return RedirectToAction("Login", "Account");

        if (rating is < 1 or > 5)
        {
            TempData["Error"] = "La note doit être comprise entre 1 et 5.";
            return RedirectToAction("Details", "Trip", new { id = tripId });
        }

        var trip = await _context.Trips
            .Include(t => t.Reservations)
            .FirstOrDefaultAsync(t => t.Id == tripId);

        if (trip == null || reviewedUserId == reviewerId)
            return NotFound();

        var canReview = trip.Reservations.Any(r =>
            r.PassengerId == reviewerId &&
            r.Status is ReservationStatus.Confirmed or ReservationStatus.Completed);

        if (!canReview)
        {
            TempData["Error"] = "Vous pouvez noter uniquement un conducteur avec une réservation confirmée.";
            return RedirectToAction("Details", "Trip", new { id = tripId });
        }

        var alreadyReviewed = await _context.Reviews.AnyAsync(r =>
            r.TripId == tripId &&
            r.ReviewerId == reviewerId &&
            r.ReviewedUserId == reviewedUserId);

        if (alreadyReviewed)
        {
            TempData["Error"] = "Vous avez déjà noté ce trajet.";
            return RedirectToAction("Details", "Trip", new { id = tripId });
        }

        _context.Reviews.Add(new Review
        {
            TripId = tripId,
            ReviewerId = reviewerId,
            ReviewedUserId = reviewedUserId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTime.Now
        });

        await _context.SaveChangesAsync();

        var reviewedUser = await _context.Users.FindAsync(reviewedUserId);
        if (reviewedUser != null)
        {
            reviewedUser.Rating = await _context.Reviews
                .Where(r => r.ReviewedUserId == reviewedUserId)
                .AverageAsync(r => r.Rating);

            _context.Notifications.Add(new Notification
            {
                RecipientId = reviewedUserId,
                Title = "Nouvelle note",
                Body = "Un passager a laissé une note sur votre trajet.",
                Link = Url.Action("Details", "Trip", new { id = tripId }),
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
        }

        TempData["Success"] = "Merci, votre avis a été publié.";
        return RedirectToAction("Details", "Trip", new { id = tripId });
    }
}