using CovStudent.Data;
using CovStudent.Hubs;
using CovStudent.Models;
using Microsoft.AspNetCore.SignalR;

namespace CovStudent.Services;

public interface INotificationService
{
    Task SendAsync(string userId, string title, string body, string? link);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<NotificationsHub> _hubContext;

    public NotificationService(ApplicationDbContext context, IHubContext<NotificationsHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public async Task SendAsync(string userId, string title, string body, string? link)
    {
        _context.Notifications.Add(new Notification
        {
            RecipientId = userId,
            Title = title,
            Body = body,
            Link = link,
            CreatedAt = DateTime.Now,
            IsRead = false
        });

        await _context.SaveChangesAsync();

        await _hubContext.Clients.User(userId)
            .SendAsync("ReceiveNotification", new { title, body, link });
    }
}