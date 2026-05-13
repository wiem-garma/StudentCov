using System.ComponentModel.DataAnnotations;

namespace CovStudent.ViewModels;

public class RegisterViewModel
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

    [Required(ErrorMessage = "Le mot de passe est requis")]
    [StringLength(100, ErrorMessage = "Le mot de passe doit contenir au moins {2} caractères.", MinimumLength = 3)]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirmer le mot de passe")]
    [Compare("Password", ErrorMessage = "Les mots de passe ne correspondent pas")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "Université")]
    [StringLength(100, ErrorMessage = "L'université ne peut pas dépasser 100 caractères")]
    public string? University { get; set; }

    [Display(Name = "Ville")]
    [StringLength(100, ErrorMessage = "La ville ne peut pas dépasser 100 caractères")]
    public string? City { get; set; }

    [Display(Name = "Adresse")]
    [StringLength(150, ErrorMessage = "L'adresse ne peut pas dépasser 150 caractères")]
    public string? Address { get; set; }

    [Display(Name = "Numéro étudiant")]
    [StringLength(20, ErrorMessage = "Le numéro étudiant ne peut pas dépasser 20 caractères")]
    public string? StudentId { get; set; }

    [Display(Name = "Téléphone")]
    [Phone(ErrorMessage = "Numéro de téléphone invalide")]
    [RegularExpression(@"^[0-9]{8}$", ErrorMessage = "Le numéro doit contenir 8 chiffres")]
    public string? PhoneNumber { get; set; }
}