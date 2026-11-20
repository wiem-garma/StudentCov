using CovStudent.Data;
using CovStudent.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CovStudent.Controllers;

[Authorize(Roles = "Admin")]
public class AuditController : Controller
{
    private const int TaillePage = 50;
    private readonly ApplicationDbContext _context;

    public AuditController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? type, string? utilisateur, DateTime? du, DateTime? au, int page = 1)
    {
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(a => a.Action == type);

        if (!string.IsNullOrWhiteSpace(utilisateur))
            query = query.Where(a => a.UserName != null && a.UserName.Contains(utilisateur));

        if (du.HasValue)
            query = query.Where(a => a.CreatedAt >= du.Value.Date);

        if (au.HasValue)
            query = query.Where(a => a.CreatedAt < au.Value.Date.AddDays(1));

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)TaillePage));
        page = Math.Clamp(page, 1, totalPages);

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * TaillePage)
            .Take(TaillePage)
            .ToListAsync();

        ViewBag.Types = typeof(AuditActions).GetFields()
            .Select(f => (string)f.GetValue(null)!)
            .ToList();
        ViewBag.Type = type;
        ViewBag.Utilisateur = utilisateur;
        ViewBag.Du = du?.ToString("yyyy-MM-dd");
        ViewBag.Au = au?.ToString("yyyy-MM-dd");
        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.Total = total;

        return View(logs);
    }
}