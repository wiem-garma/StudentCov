using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CovStudent.Models;

public class Review
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string ReviewerId { get; set; } = string.Empty;

    [Required]
    public string ReviewedUserId { get; set; } = string.Empty;

    [Required]
    public int TripId { get; set; }

    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(500)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey("ReviewerId")]
    public virtual AppUser? Reviewer { get; set; }  // ← AppUser

    [ForeignKey("ReviewedUserId")]
    public virtual AppUser? ReviewedUser { get; set; }  // ← AppUser

    [ForeignKey("TripId")]
    public virtual Trip? Trip { get; set; }
}