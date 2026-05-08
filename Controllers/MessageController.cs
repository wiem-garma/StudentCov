using CovStudent.Data;
using CovStudent.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace CovStudent.Controllers
{
    [Authorize]
    public class MessageController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MessageController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(string? userId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var messages = _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m => (m.SenderId == currentUserId && m.ReceiverId == userId) ||
                           (m.SenderId == userId && m.ReceiverId == currentUserId))
                .OrderBy(m => m.SentAt)
                .ToList();

            ViewBag.WithUser = _context.Users.Find(userId);
            ViewBag.CurrentUserId = currentUserId;

            return View(messages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(string receiverId, string content, int? tripId)
        {
            var message = new Message
            {
                SenderId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
                ReceiverId = receiverId,
                Content = content,
                SentAt = DateTime.Now,
                TripId = tripId
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { userId = receiverId });
        }

        public IActionResult Conversations()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var conversations = _context.Messages
                .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .OrderByDescending(m => m.SentAt)
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
}
