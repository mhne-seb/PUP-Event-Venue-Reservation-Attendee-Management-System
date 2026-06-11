using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PUPEventVenue.Data;
using PUPEventVenue.Models;
using PUPEventVenue.ViewModels;

namespace PUPEventVenue.Controllers
{
    public class VenuesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<ApplicationUser> _userManager;

        public VenuesController(ApplicationDbContext db, IWebHostEnvironment env,
            UserManager<ApplicationUser> um)
        {
            _db = db; _env = env; _userManager = um;
        }

        // GET: /Venues
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? search, bool? available)
        {
            var query = _db.Venues.AsQueryable();
            if (!string.IsNullOrEmpty(search))
                query = query.Where(v => v.Name.Contains(search) || (v.Location != null && v.Location.Contains(search)));
            if (available.HasValue)
                query = query.Where(v => v.IsAvailable == available.Value);

            ViewBag.Search = search;
            ViewBag.Available = available;
            return View(await query.OrderBy(v => v.Name).ToListAsync());
        }

        // GET: /Venues/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var venue = await _db.Venues
                .Include(v => v.Reservations.Where(r => r.Status == ReservationStatus.Approved))
                .FirstOrDefaultAsync(v => v.VenueId == id);
            if (venue == null) return NotFound();
            return View(venue);
        }

        // GET: /Venues/Create  [Admin only]
        [Authorize(Roles = "Admin")]
        public IActionResult Create() => View(new VenueViewModel());

        // POST: /Venues/Create
        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VenueViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var venue = new Venue
            {
                Name = model.Name,
                Description = model.Description,
                Location = model.Location,
                Capacity = model.Capacity,
                Amenities = model.Amenities,
                IsAvailable = model.IsAvailable,
                PricePerHour = model.PricePerHour
            };

            if (model.ImageFile != null)
                venue.ImageUrl = await SaveImageAsync(model.ImageFile);

            _db.Venues.Add(venue);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Venue created successfully!";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Venues/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var venue = await _db.Venues.FindAsync(id);
            if (venue == null) return NotFound();
            return View(new VenueViewModel
            {
                VenueId = venue.VenueId,
                Name = venue.Name,
                Description = venue.Description,
                Location = venue.Location,
                Capacity = venue.Capacity,
                Amenities = venue.Amenities,
                IsAvailable = venue.IsAvailable,
                PricePerHour = venue.PricePerHour,
                ExistingImageUrl = venue.ImageUrl
            });
        }

        // POST: /Venues/Edit/5
        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, VenueViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var venue = await _db.Venues.FindAsync(id);
            if (venue == null) return NotFound();

            venue.Name = model.Name;
            venue.Description = model.Description;
            venue.Location = model.Location;
            venue.Capacity = model.Capacity;
            venue.Amenities = model.Amenities;
            venue.IsAvailable = model.IsAvailable;
            venue.PricePerHour = model.PricePerHour;
            venue.UpdatedAt = DateTime.UtcNow;

            if (model.ImageFile != null)
                venue.ImageUrl = await SaveImageAsync(model.ImageFile);

            await _db.SaveChangesAsync();
            TempData["Success"] = "Venue updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Venues/Delete/5
        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var venue = await _db.Venues.FindAsync(id);
            if (venue == null) return NotFound();
            _db.Venues.Remove(venue);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Venue deleted.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Venues/CheckAvailability
        [AllowAnonymous]
        public async Task<IActionResult> CheckAvailability(int venueId, DateTime start, DateTime end)
        {
            var conflicts = await _db.Reservations
                .Where(r => r.VenueId == venueId
                    && r.Status == ReservationStatus.Approved
                    && r.StartDateTime < end
                    && r.EndDateTime > start)
                .Select(r => new { r.StartDateTime, r.EndDateTime, r.EventName })
                .ToListAsync();

            return Json(new { available = !conflicts.Any(), conflicts });
        }

        private async Task<string> SaveImageAsync(IFormFile file)
        {
            var uploadsDir = Path.Combine(_env.WebRootPath, "images", "venues");
            Directory.CreateDirectory(uploadsDir);
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsDir, fileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);
            return $"/images/venues/{fileName}";
        }
    }
}
