using CovStudent.Data;
using CovStudent.Hubs;
using CovStudent.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CovStudent.Controllers;

[Authorize]
public class MessageController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<NotificationsHub> _hubContext;
    private readonly ILogger<MessageController> _logger;

    public MessageController(
        ApplicationDbContext context,
        IHubContext<NotificationsHub> hubContext,
        ILogger<MessageController> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    // Une réservation "valide" pour discuter : ni annulée, ni refusée
    private static bool ReservationValide(Reservation r)
        => r.Status != ReservationStatus.Cancelled && r.Status != ReservationStatus.Rejected;

    [HttpGet]
    public async Task<IActionResult> Index(string? userId, int? tripId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (currentUserId == null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(userId) || tripId == null)
        {
            ViewBag.CurrentUserId = currentUserId;
            ViewBag.WithUser = null;
            ViewBag.TripId = null;
            ViewBag.Trip = null;
            TempData["Error"] = "Choisissez une conversation liée à un trajet.";
            return View(new List<Message>());
        }

        var trip = await _context.Trips
            .Include(t => t.Driver)
            .Include(t => t.Reservations)
                .ThenInclude(r => r.Passenger)
            .FirstOrDefaultAsync(t => t.Id == tripId.Value);

        if (trip == null)
        {
            TempData["Error"] = "Trajet introuvable.";
            return View(new List<Message>());
        }

        var isDriver = trip.DriverId == currentUserId;
        var isPassenger = trip.Reservations.Any(r => r.PassengerId == currentUserId && ReservationValide(r));

        if (!isDriver && !isPassenger)
        {
            TempData["Error"] = "Vous ne pouvez discuter que si vous êtes le conducteur ou un passager de ce trajet.";
            return View(new List<Message>());
        }

        var validReceiver = isDriver
            ? trip.Reservations.Any(r => r.PassengerId == userId && ReservationValide(r))
            : trip.DriverId == userId;

        if (!validReceiver)
        {
            TempData["Error"] = "Cette conversation ne correspond pas à un trajet autorisé.";
            return View(new List<Message>());
        }

        var messages = await _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .Include(m => m.Trip)
            .Where(m =>
                m.TripId == tripId.Value &&
                ((m.SenderId == currentUserId && m.ReceiverId == userId) ||
                 (m.SenderId == userId && m.ReceiverId == currentUserId)))
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        // Marquer comme lus
        foreach (var m in messages.Where(m => m.ReceiverId == currentUserId && !m.IsRead))
            m.IsRead = true;

        await _context.SaveChangesAsync();

        ViewBag.WithUser = await _context.Users.FindAsync(userId);
        ViewBag.CurrentUserId = currentUserId;
        ViewBag.TripId = tripId;
        ViewBag.Trip = trip;

        return View(messages);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string receiverId, string content, int? tripId)
    {
        var senderId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(senderId))
            return Unauthorized();

        if (tripId == null)
        {
            TempData["Error"] = "La conversation doit être liée à un trajet.";
            return RedirectToAction(nameof(Conversations));
        }

        if (string.IsNullOrWhiteSpace(receiverId))
        {
            TempData["Error"] = "Sélectionnez un interlocuteur avant d'envoyer un message.";
            return RedirectToAction(nameof(Conversations));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            TempData["Error"] = "Message vide.";
            return RedirectToAction(nameof(Index), new { userId = receiverId, tripId });
        }

        var trip = await _context.Trips
            .Include(t => t.Reservations)
            .FirstOrDefaultAsync(t => t.Id == tripId.Value);

        if (trip == null)
        {
            TempData["Error"] = "Trajet introuvable.";
            return RedirectToAction(nameof(Conversations));
        }

        var senderIsDriver = trip.DriverId == senderId;
        var senderIsPassenger = trip.Reservations.Any(r => r.PassengerId == senderId && ReservationValide(r));

        if (!senderIsDriver && !senderIsPassenger)
        {
            TempData["Error"] = "Vous ne pouvez discuter que si vous êtes lié à ce trajet.";
            return RedirectToAction(nameof(Conversations));
        }

        var receiverIsValid = senderIsDriver
            ? trip.Reservations.Any(r => r.PassengerId == receiverId && ReservationValide(r))
            : trip.DriverId == receiverId;

        if (!receiverIsValid)
        {
            TempData["Error"] = "Le destinataire n'est pas autorisé pour ce trajet.";
            return RedirectToAction(nameof(Index), new { userId = receiverId, tripId });
        }

        _context.Messages.Add(new Message
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Content = content,
            SentAt = DateTime.Now,
            TripId = tripId
        });

        var notif = new Notification
        {
            RecipientId = receiverId,
            Title = "Nouveau message",
            Body = content.Length > 150 ? content[..150] + "..." : content,
            CreatedAt = DateTime.Now,
            Link = Url.Action("Index", "Message", new { userId = senderId, tripId })
        };

        _context.Notifications.Add(notif);

        await _context.SaveChangesAsync();

        try
        {
            await _hubContext.Clients.User(receiverId)
                .SendAsync("ReceiveNotification", new { title = notif.Title, body = notif.Body, link = notif.Link });
        }
        catch (Exception ex)
        {
            // Fiabilité : le message est déjà enregistré, l'échec du temps réel n'est pas bloquant
            _logger.LogWarning(ex, "Notification temps réel non envoyée à {ReceiverId}", receiverId);
        }

        return RedirectToAction(nameof(Index), new { userId = receiverId, tripId });
    }

    [HttpGet]
    public async Task<IActionResult> Conversations()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (currentUserId == null)
            return Unauthorized();

        var messages = await _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .Include(m => m.Trip)
            .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
            .OrderByDescending(m => m.SentAt)
            .ToListAsync();

        var partnerIds = messages
            .Where(m => m.TripId != null)
            .Select(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
            .Distinct()
            .ToList();

        var partners = await _context.Users
            .Where(u => partnerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var tripIds = messages
            .Where(m => m.TripId != null)
            .Select(m => m.TripId!.Value)
            .Distinct()
            .ToList();

        var trips = await _context.Trips
            .Where(t => tripIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id);

        var conversations = messages
            .Where(m => m.TripId != null)
            .GroupBy(m => new
            {
                TripId = m.TripId!.Value,
                PartnerId = m.SenderId == currentUserId ? m.ReceiverId : m.SenderId
            })
            .Select(g => new
            {
                TripId = g.Key.TripId,
                UserId = g.Key.PartnerId,
                DisplayName = partners.TryGetValue(g.Key.PartnerId, out var partner)
                    ? partner.FullName
                    : g.Key.PartnerId,
                Initial = partners.TryGetValue(g.Key.PartnerId, out var foundPartner) && !string.IsNullOrWhiteSpace(foundPartner.FullName)
                    ? foundPartner.FullName.Substring(0, 1).ToUpper()
                    : "U",
                TripLabel = trips.TryGetValue(g.Key.TripId, out var trip)
                    ? $"{trip.Departure} → {trip.Destination}"
                    : "Trajet",
                LastMessage = g.First(),
                UnreadCount = g.Count(m => m.ReceiverId == currentUserId && !m.IsRead)
            })
            .OrderByDescending(x => x.LastMessage.SentAt)
            .ToList();

        ViewBag.Conversations = conversations;
        ViewBag.CurrentUserId = currentUserId;

        return View();
    }
}