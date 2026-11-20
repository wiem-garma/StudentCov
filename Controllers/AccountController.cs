using CovStudent.Data;
using CovStudent.Models;
using CovStudent.Services;
using CovStudent.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CovStudent.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public AccountController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        ApplicationDbContext context,
        IAuditService auditService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _auditService = auditService;
    }

    // ================= LOGIN =================

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login() => View();

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);

        // RM-09 : un utilisateur bloqué ne peut pas se connecter
        if (user == null || user.IsBlocked)
        {
            await _auditService.LogAsync(AuditActions.LoginFailed, "Account", user?.Id,
                user?.IsBlocked == true ? "Compte bloqué" : "Utilisateur inconnu",
                userId: user?.Id, userName: model.Email);

            // Message volontairement générique pour un compte inconnu (pas d'énumération des comptes)
            ModelState.AddModelError("", user?.IsBlocked == true
                ? "Votre compte est bloqué. Contactez l'administration."
                : "Email ou mot de passe incorrect.");
            return View(model);
        }

        // lockoutOnFailure: true → verrouillage après 5 échecs (protection brute force)
        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            user.LastLoginDate = DateTime.Now;
            await _userManager.UpdateAsync(user);

            await _auditService.LogAsync(AuditActions.LoginSuccess, "Account", user.Id,
                userId: user.Id, userName: user.Email);

            return RedirectToAction("Index", "Dashboard");
        }

        if (result.IsLockedOut)
        {
            await _auditService.LogAsync(AuditActions.LoginFailed, "Account", user.Id,
                "Compte verrouillé (trop de tentatives)", userId: user.Id, userName: user.Email);

            ModelState.AddModelError("", "Trop de tentatives échouées. Réessayez dans quelques minutes.");
            return View(model);
        }

        await _auditService.LogAsync(AuditActions.LoginFailed, "Account", user.Id,
            "Mot de passe incorrect", userId: user.Id, userName: user.Email);

        ModelState.AddModelError("", "Email ou mot de passe incorrect.");
        return View(model);
    }

    // ================= INSCRIPTION =================

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register() => View();

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = new AppUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            University = model.University,
            City = model.City,
            Address = model.Address,
            PhoneNumber = model.PhoneNumber,
            StudentId = model.StudentId,
            RegistrationDate = DateTime.Now
        };

        // Sécurité : plus de "premier inscrit = Admin". Le compte admin est créé par le seed.
        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            await _auditService.LogAsync(AuditActions.Register, "Account", user.Id,
                userId: user.Id, userName: user.Email);

            await _signInManager.SignInAsync(user, false);
            return RedirectToAction("Index", "Dashboard");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);

        return View(model);
    }

    // ================= PROFIL =================

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile(string? id, int? tripId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return RedirectToAction(nameof(Login));

        AppUser? user = currentUser;
        var isOwnProfile = string.IsNullOrWhiteSpace(id) || id == currentUser.Id;

        if (!isOwnProfile && !string.IsNullOrWhiteSpace(id) && tripId.HasValue)
        {
            var trip = await _context.Trips
                .Include(t => t.Reservations)
                .FirstOrDefaultAsync(t => t.Id == tripId.Value);

            if (trip == null)
                return NotFound();

            // On ne voit le profil que d'une personne liée à un trajet commun (réservation non annulée/refusée)
            bool ReservationValide(Reservation r) =>
                r.Status != ReservationStatus.Cancelled && r.Status != ReservationStatus.Rejected;

            var canView =
                (trip.DriverId == currentUser.Id && trip.Reservations.Any(r => r.PassengerId == id && ReservationValide(r))) ||
                (trip.DriverId == id && trip.Reservations.Any(r => r.PassengerId == currentUser.Id && ReservationValide(r)));

            if (!canView)
                return Forbid();

            user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            ViewBag.RelationLabel = trip.DriverId == currentUser.Id ? "Passager du trajet" : "Conducteur du trajet";
        }

        ViewBag.IsOwnProfile = isOwnProfile;
        ViewBag.BackUrl = tripId.HasValue
            ? Url.Action("Details", "Trip", new { id = tripId.Value })
            : Url.Action("Index", "Dashboard");

        return View(user);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> EditProfile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction(nameof(Login));

        var model = new EditProfileViewModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            University = user.University,
            City = user.City,
            Address = user.Address,
            PhoneNumber = user.PhoneNumber
        };

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProfile(EditProfileViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction(nameof(Login));

        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.Email = model.Email;
        user.UserName = model.Email;
        user.University = model.University;
        user.City = model.City;
        user.Address = model.Address;
        user.PhoneNumber = model.PhoneNumber;

        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Profil mis à jour avec succès.";
            return RedirectToAction(nameof(Profile));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);

        return View(model);
    }

    // ================= DIVERS =================

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _auditService.LogAsync(AuditActions.Logout, "Account");
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
}