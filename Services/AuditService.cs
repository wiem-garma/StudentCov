using System.Security.Claims;
using CovStudent.Data;
using CovStudent.Models;
using Microsoft.EntityFrameworkCore;

namespace CovStudent.Services;

public interface IAuditService
{
    Task LogAsync(string action, string entity, string? entityId = null,
                  string? details = null, string? userId = null, string? userName = null);
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(string action, string entity, string? entityId = null,
                               string? details = null, string? userId = null, string? userName = null)
    {
        var http = _httpContextAccessor.HttpContext;

        var log = new AuditLog
        {
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Details = details,
            // Par défaut : l'utilisateur connecté. On peut le forcer (ex. échec de login)
            UserId = userId ?? http?.User.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName = userName ?? http?.User.Identity?.Name,
            IpAddress = http?.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = DateTime.Now
        };

        try
        {
            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Fiabilité : un échec d'audit ne doit pas casser l'action de l'utilisateur,
            // mais il doit être visible dans les logs
            _context.Entry(log).State = EntityState.Detached;
            _logger.LogError(ex, "Échec d'écriture du journal d'audit pour l'action {Action}", action);
        }
    }
}