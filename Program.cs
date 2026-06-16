using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PUPEventVenue.Data;
using PUPEventVenue.Models;
using PUPEventVenue.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
});

builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<PUPEventVenue.Filters.PendingPaymentBadgeFilter>();
builder.Services.AddControllersWithViews(options =>
    options.Filters.AddService<PUPEventVenue.Filters.PendingPaymentBadgeFilter>());

var app = builder.Build();

// Ensure receipt uploads directory exists
var webRoot = app.Environment.WebRootPath;
Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "receipts"));

// ── Seed Database ─────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();
        await SeedData.InitializeAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error seeding the database.");
    }
}

// ── Middleware ────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");

// ── Seed Data ─────────────────────────────────────────────────
public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var db = services.GetRequiredService<ApplicationDbContext>();

        // Ensure roles
        foreach (var role in new[] { "Admin", "Organizer" })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        // Seed Admin account
        const string adminEmail = "admin@pup.edu.ph";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "PUP System Administrator",
                Department = "Office of Student Affairs",
                EmailConfirmed = true,
                IsActive = true
            };
            var result = await userManager.CreateAsync(admin, "Admin@12345");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, "Admin");
        }

        // Seed demo organizer
        const string orgEmail = "organizer@pup.edu.ph";
        if (await userManager.FindByEmailAsync(orgEmail) == null)
        {
            var org = new ApplicationUser
            {
                UserName = orgEmail,
                Email = orgEmail,
                FullName = "Juan dela Cruz",
                StudentNumber = "2021-00001",
                OrganizationName = "PUP Supreme Student Council",
                Department = "BSIT",
                EmailConfirmed = true,
                IsActive = true
            };
            var result = await userManager.CreateAsync(org, "Organizer@12345");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(org, "Organizer");
        }

        // Seed venues if none exist
        if (!db.Venues.Any())
        {
            db.Venues.AddRange(
                new PUPEventVenue.Models.Venue
                {
                    Name = "Main Auditorium",
                    Description = "The largest venue in PUP, perfect for major university events, graduation ceremonies, and large-scale gatherings.",
                    Location = "Main Building, Ground Floor",
                    Capacity = 1500,
                    ImageUrl = "/images/venues/default-venue.jpg",
                    Amenities = "Stage,Projector,Sound System,Air Conditioning,Parking",
                    IsAvailable = true,
                    PricePerHour = 5000
                },
                new PUPEventVenue.Models.Venue
                {
                    Name = "Audio Visual Room 1",
                    Description = "A well-equipped AV room suitable for seminars, workshops, and academic presentations.",
                    Location = "Main Building, 2nd Floor, Room 201",
                    Capacity = 80,
                    ImageUrl = "/images/venues/default-venue.jpg",
                    Amenities = "Projector,Whiteboard,Air Conditioning,Microphone",
                    IsAvailable = true,
                    PricePerHour = 1500
                },
                new PUPEventVenue.Models.Venue
                {
                    Name = "University Gymnasium",
                    Description = "Spacious gymnasium used for sports events, major assemblies, and large gatherings.",
                    Location = "Sports Complex, East Wing",
                    Capacity = 3000,
                    ImageUrl = "/images/venues/default-venue.jpg",
                    Amenities = "Stage,Sound System,Bleachers,Parking",
                    IsAvailable = true,
                    PricePerHour = 4000
                },
                new PUPEventVenue.Models.Venue
                {
                    Name = "Open Grounds / Oval",
                    Description = "The PUP oval open grounds, suitable for large outdoor events, fairs, and university-wide activities.",
                    Location = "PUP Campus Grounds",
                    Capacity = 5000,
                    ImageUrl = "/images/venues/default-venue.jpg",
                    Amenities = "Open Space,Electrical Outlets,Nearby Parking",
                    IsAvailable = true,
                    PricePerHour = 3000
                }
            );
            await db.SaveChangesAsync();
        }
    }
}

// ── Startup Folder Service ─────────────────────────────────────
public class StartupFolderService : IHostedService
{
    private readonly IWebHostEnvironment _env;
    public StartupFolderService(IWebHostEnvironment env) => _env = env;
    public Task StartAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(Path.Combine(_env.WebRootPath, "uploads", "receipts"));
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}