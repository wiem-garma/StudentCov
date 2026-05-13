using System.ComponentModel.DataAnnotations;

namespace CovStudent.ViewModels;

public class EditProfileViewModel
{
    [Required(ErrorMessage = "Le nom est requis")]
    [Display(Name = "Nom")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Le nom doit contenir entre 2 et 50 caractères")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le prénom est requis")]
    [Display(Name = "Prénom")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Le prénom doit contenir entre 2 et 50 caractères")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "L'email est requis")]
    [EmailAddress(ErrorMessage = "Email invalide")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Université")]
    [StringLength(100, ErrorMessage = "L'université ne peut pas dépasser 100 caractères")]
    public string? University { get; set; }

    [Display(Name = "Téléphone")]
    [Phone(ErrorMessage = "Numéro de téléphone invalide")]
    [RegularExpression(@"^[0-9]{8}$", ErrorMessage = "Le numéro doit contenir 8 chiffres")]
    public string? PhoneNumber { get; set; }
}