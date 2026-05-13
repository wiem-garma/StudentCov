using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CovStudent.Models;

public class Notification
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string RecipientId { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Body { get; set; } = string.Empty;

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [StringLength(300)]
    public string? Link { get; set; }

    [ForeignKey(nameof(RecipientId))]
    public virtual AppUser? Recipient { get; set; }
}