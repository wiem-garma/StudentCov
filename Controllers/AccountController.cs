using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CovStudent.Data;
using CovStudent.Models;
using CovStudent.ViewModels;

namespace CovStudent.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly ApplicationDbContext _context;

    public AccountController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
    }

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

        if (user == null || user.IsBlocked)
        {
            ModelState.AddModelError("", user?.IsBlocked == true
                ? "Votre compte est bloque. Contactez l'administration."
                : "Login incorrect");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            false);

        if (result.Succeeded)
        {
            user.LastLoginDate = DateTime.Now;
            await _userManager.UpdateAsync(user);
            return RedirectToAction("Index", "Dashboard");
        }

        ModelState.AddModelError("", "Login incorrect");
        return View(model);
    }

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

        var isFirstUser = !_userManager.Users.Any();

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

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            if (isFirstUser)
                await _userManager.AddToRoleAsync(user, "Admin");

            await _signInManager.SignInAsync(user, false);
            return RedirectToAction("Index", "Dashboard");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);

        return View(model);
    }

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

            var canView = trip != null && (
                (trip.DriverId == currentUser.Id && trip.Reservations.Any(r => r.PassengerId == id && r.Status != ReservationStatus.Cancelled)) ||
                (trip.Reservations.Any(r => r.PassengerId == currentUser.Id && r.Status != ReservationStatus.Cancelled) && trip.DriverId == id));

            if (!canView)
                return Forbid();

            user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            ViewBag.RelationLabel = trip.DriverId == currentUser.Id ? "Passager du trajet" : "Conducteur du trajet";
        }

        ViewBag.IsOwnProfile = isOwnProfile;
        ViewBag.BackUrl = tripId.HasValue ? Url.Action("Details", "Trip", new { id = tripId.Value }) : Url.Action("Index", "Dashboard");

        return View(user);
    }

    [Authorize]
    [HttpGet]
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

    [Authorize]
    [HttpPost]
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
            TempData["SuccessMessage"] = "Profil mis a jour avec succes.";
            return RedirectToAction(nameof(Profile));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);

        return View(model);
    }

    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
}
