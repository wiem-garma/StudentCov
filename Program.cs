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

// 🔥 IMPORTANT (dev only)
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ================= MVC =================

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// ================= IDENTITY =================

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    // 🔐 Password (dev config)
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequiredLength = 3;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;

    // 🔐 Lockout
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // 👤 User config
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    // 🔑 SignIn
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
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
});

// ================= SERVICES =================

builder.Services.AddScoped<ITripService, TripService>();
builder.Services.AddScoped<IReservationService, ReservationService>();

// ================= SIGNALR =================

builder.Services.AddSignalR();

// ================= APP =================

var app = builder.Build();

// ================= PIPELINE =================

try
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    if (!await roleManager.RoleExistsAsync("Admin"))
        await roleManager.CreateAsync(new IdentityRole("Admin"));
}
catch
{
    // Database may not be available yet during first local startup.
}

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

// 🔐 IMPORTANT ORDER
app.UseAuthentication();
app.UseAuthorization();

// ================= ROUTES =================

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.MapRazorPages();

// ================= SIGNALR =================

app.MapHub<NotificationsHub>("/notificationsHub");

// ================= START =================

app.Run();
