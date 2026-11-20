using CovStudent.Data;
using CovStudent.Models;
using CovStudent.Services;
using CovStudent.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ================= DATABASE =================

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Page d'erreur détaillée EF (développement uniquement)
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ================= HEALTH CHECK =================

// /health : "Healthy" si la base répond, sinon 503 (fiabilité + Docker)
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("base-de-donnees");

// ================= MVC =================

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// ================= IDENTITY =================

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    // 🔐 Politique de mot de passe (OWASP A07)
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // 🔐 Verrouillage après 5 échecs
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // 👤 Utilisateur
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    // 🔑 Connexion
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ================= AUTH COOKIE =================

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;

    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// ================= SERVICES =================

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITripService, TripService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// ================= SIGNALR =================

builder.Services.AddSignalR();

// ================= APP =================

var app = builder.Build();

// ================= DONNÉES DE DÉMONSTRATION =================

// dotnet run -- --seed
if (args.Contains("--seed"))
{
    await DataSeeder.SeedAsync(app.Services, app.Configuration, app.Logger);
    return;
}

// ================= MIGRATIONS AUTOMATIQUES (Docker) =================

// Activé par la variable Database__AppliquerMigrations=true (docker-compose)
if (app.Configuration.GetValue<bool>("Database:AppliquerMigrations"))
{
    const int maxTentatives = 10;

    for (var tentative = 1; ; tentative++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();
            app.Logger.LogInformation("Migrations appliquées");
            break;
        }
        catch (Exception ex) when (tentative < maxTentatives)
        {
            // Fiabilité : la base peut démarrer plus lentement que l'application
            app.Logger.LogWarning(ex, "Base indisponible (tentative {Tentative}/{Max}), nouvel essai dans 5 s",
                tentative, maxTentatives);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}

// ================= INITIALISATION DES RÔLES =================

try
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    if (!await roleManager.RoleExistsAsync("Admin"))
        await roleManager.CreateAsync(new IdentityRole("Admin"));
}
catch (Exception ex)
{
    // Fiabilité : l'application démarre quand même, mais l'erreur est tracée
    app.Logger.LogWarning(ex, "Initialisation des rôles impossible au démarrage (base indisponible ?)");
}

// ================= PIPELINE =================

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 🔐 L'ordre compte : d'abord qui es-tu, ensuite as-tu le droit
app.UseAuthentication();
app.UseAuthorization();

// ================= ROUTES =================

app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.MapRazorPages();

app.MapHub<NotificationsHub>("/notificationsHub");

app.Run();

// Rend la classe Program visible pour les tests d'intégration (WebApplicationFactory)
public partial class Program { }