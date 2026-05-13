using CovStudent.Data;
using CovStudent.Models;
using CovStudent.Hubs;
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

    public MessageController(ApplicationDbContext context, IHubContext<NotificationsHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public async Task<IActionResult> Index(string? userId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (currentUserId == null)
            return Unauthorized();

        if (string.IsNullOrEmpty(userId))
            return View(new List<Message>());

        var messages = await _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .Where(m =>
                (m.SenderId == currentUserId && m.ReceiverId == userId) ||
                (m.SenderId == userId && m.ReceiverId == currentUserId))
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        // mark as read
        var unread = messages
            .Where(m => m.ReceiverId == currentUserId && !m.IsRead)
            .ToList();

        foreach (var m in unread)
            m.IsRead = true;

        await _context.SaveChangesAsync();

        ViewBag.WithUser = await _context.Users.FindAsync(userId);
        ViewBag.CurrentUserId = currentUserId;

        return View(messages);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string receiverId, string content, int? tripId)
    {
        var senderId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(senderId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(content))
        {
            TempData["Error"] = "Message vide.";
            return RedirectToAction(nameof(Index), new { userId = receiverId });
        }

        var message = new Message
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Content = content,
            SentAt = DateTime.Now,
            TripId = tripId
        };

        _context.Messages.Add(message);

        var senderUser = await _context.Users.FindAsync(senderId);

        var notif = new Notification
        {
            RecipientId = receiverId,
            Title = "Nouveau message",
            Body = content.Length > 150 ? content[..150] + "..." : content,
            CreatedAt = DateTime.Now,
            Link = Url.Action("Index", "Message", new { userId = senderId })
        };

        _context.Notifications.Add(notif);

        await _context.SaveChangesAsync();

        try
        {
            await _hubContext.Clients.User(receiverId)
                .SendAsync("ReceiveNotification", notif);
        }
        catch { }

        return RedirectToAction(nameof(Index), new { userId = receiverId });
    }

    public async Task<IActionResult> Conversations()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (currentUserId == null)
            return Unauthorized();

        var messages = await _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
            .OrderByDescending(m => m.SentAt)
            .ToListAsync();

        var conversations = messages
            .GroupBy(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
            .Select(g => new
            {
                UserId = g.Key,
                LastMessage = g.First(),
                UnreadCount = g.Count(m => m.ReceiverId == currentUserId && !m.IsRead)
            })
            .ToList();

        ViewBag.Conversations = conversations;
        ViewBag.CurrentUserId = currentUserId;

        return View();
    }
}