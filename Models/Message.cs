using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CovStudent.Models;

public class Message
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string SenderId { get; set; } = string.Empty;

    [Required]
    public string ReceiverId { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.Now;
    public bool IsRead { get; set; } = false;
    public int? TripId { get; set; }

    [ForeignKey("SenderId")]
    public virtual AppUser? Sender { get; set; }  // ← AppUser

    [ForeignKey("ReceiverId")]
    public virtual AppUser? Receiver { get; set; }  // ← AppUser

    [ForeignKey("TripId")]
    public virtual Trip? Trip { get; set; }
}