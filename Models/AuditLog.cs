using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CovStudent.Models;

[Index(nameof(CreatedAt))]
[Index(nameof(UserId))]
[Index(nameof(Action))]
public class AuditLog
{
    public int Id { get; set; }

    // Pas de clé étrangère volontairement : le journal doit survivre
    // même si l'utilisateur est supprimé (traçabilité)
    [StringLength(450)]
    public string? UserId { get; set; }

    [StringLength(256)]
    public string? UserName { get; set; }

    [Required, StringLength(50)]
    public string Action { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Entity { get; set; } = string.Empty;

    [StringLength(50)]
    public string? EntityId { get; set; }

    [StringLength(500)]
    public string? Details { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}