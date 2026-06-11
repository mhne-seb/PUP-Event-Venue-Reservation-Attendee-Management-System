using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PUPEventVenue.Data;
using PUPEventVenue.Models;
using PUPEventVenue.ViewModels;

namespace PUPEventVenue.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> um)
        {
            _db = db; _userManager = um;
        }

        // GET: /Admin/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var now = DateTime.UtcNow;
            var reservations = await _db.Reservations
                .Include(r => r.Venue)
                .Include(r => r.Organizer)
                .Include(r => r.Attendees)
                .ToListAsync();

            var venueStats = await _db.Venues
                .Select(v => new VenueUsageStat
                {
                    VenueName = v.Name,
                    ReservationCount = v.Reservations.Count(r => r.Status == ReservationStatus.Approved),
                    TotalAttendees = v.Reservations
                        .Where(r => r.Status == ReservationStatus.Approved)
                        .SelectMany(r => r.Attendees).Count()
                }).ToListAsync();

            // Last 6 months stats
            var monthly = Enumerable.Range(0, 6)
                .Select(i => now.AddMonths(-i))
                .Select(m => new MonthlyStatItem
                {
                    Month = m.ToString("MMM yyyy"),
                    Count = reservations.Count(r =>
                        r.CreatedAt.Year == m.Year && r.CreatedAt.Month == m.Month)
                })
                .Reverse()
                .ToList();

            var vm = new AdminDashboardViewModel
            {
                TotalVenues = await _db.Venues.CountAsync(),
                TotalReservations = reservations.Count,
                PendingReservations = reservations.Count(r => r.Status == ReservationStatus.Pending),
                ApprovedReservations = reservations.Count(r => r.Status == ReservationStatus.Approved),
                RejectedReservations = reservations.Count(r => r.Status == ReservationStatus.Rejected),
                TotalAttendees = await _db.Attendees.CountAsync(),
                TotalUsers = await _userManager.Users.CountAsync(),
                RecentReservations = reservations.OrderByDescending(r => r.CreatedAt).Take(10).ToList(),
                VenueUsageStats = venueStats,
                MonthlyStats = monthly
            };

            return View(vm);
        }

        // GET: /Admin/Users
        public async Task<IActionResult> Users(string? search)
        {
            var query = _userManager.Users.AsQueryable();
            if (!string.IsNullOrEmpty(search))
                query = query.Where(u => u.FullName.Contains(search)
                    || (u.Email != null && u.Email.Contains(search))
                    || (u.OrganizationName != null && u.OrganizationName.Contains(search)));

            var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
            var usersWithRoles = new List<(ApplicationUser User, IList<string> Roles)>();

            foreach (var u in users)
                usersWithRoles.Add((u, await _userManager.GetRolesAsync(u)));

            ViewBag.Search = search;
            return View(usersWithRoles);
        }

        // POST: /Admin/ToggleUserStatus
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();
            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);
            TempData["Success"] = $"User {user.FullName} status updated.";
            return RedirectToAction(nameof(Users));
        }

        // GET: /Admin/Reports
        public async Task<IActionResult> Reports(DateTime? from, DateTime? to, string? venueId)
        {
            from ??= DateTime.UtcNow.AddMonths(-1);
            to ??= DateTime.UtcNow;

            var query = _db.Reservations
                .Include(r => r.Venue)
                .Include(r => r.Organizer)
                .Include(r => r.Attendees)
                .Where(r => r.CreatedAt >= from && r.CreatedAt <= to.Value.AddDays(1));

            if (!string.IsNullOrEmpty(venueId) && int.TryParse(venueId, out int vid))
                query = query.Where(r => r.VenueId == vid);

            var reservations = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
            var venues = await _db.Venues.ToListAsync();

            ViewBag.From = from.Value.ToString("yyyy-MM-dd");
            ViewBag.To = to.Value.ToString("yyyy-MM-dd");
            ViewBag.VenueId = venueId;
            ViewBag.Venues = venues;

            return View(reservations);
        }

        // GET: /Admin/VenueUsage
        public async Task<IActionResult> VenueUsage()
        {
            var stats = await _db.Venues
                .Include(v => v.Reservations)
                    .ThenInclude(r => r.Attendees)
                .Select(v => new
                {
                    v.VenueId,
                    v.Name,
                    v.Capacity,
                    v.IsAvailable,
                    TotalReservations = v.Reservations.Count,
                    ApprovedReservations = v.Reservations.Count(r => r.Status == ReservationStatus.Approved),
                    TotalAttendees = v.Reservations.SelectMany(r => r.Attendees).Count(),
                    LastUsed = v.Reservations
                        .Where(r => r.Status == ReservationStatus.Approved)
                        .OrderByDescending(r => r.StartDateTime)
                        .Select(r => (DateTime?)r.StartDateTime)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return View(stats);
        }
    }
}
