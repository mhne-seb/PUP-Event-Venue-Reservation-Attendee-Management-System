using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PUPEventVenue.Data;
using PUPEventVenue.Models;
using PUPEventVenue.ViewModels;

namespace PUPEventVenue.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ApplicationDbContext db, UserManager<ApplicationUser> um)
        {
            _db = db; _userManager = um;
        }

        // GET: /
        public async Task<IActionResult> Index()
        {
            var venues = await _db.Venues.Where(v => v.IsAvailable).Take(6).ToListAsync();
            return View(venues);
        }

        // GET: /Home/Dashboard
        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction("Dashboard", "Admin");

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var reservations = await _db.Reservations
                .Include(r => r.Venue)
                .Where(r => r.OrganizerId == user.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var vm = new OrganizerDashboardViewModel
            {
                TotalReservations = reservations.Count,
                PendingCount = reservations.Count(r => r.Status == ReservationStatus.Pending),
                ApprovedCount = reservations.Count(r => r.Status == ReservationStatus.Approved),
                RejectedCount = reservations.Count(r => r.Status == ReservationStatus.Rejected),
                UpcomingEvents = reservations
                    .Where(r => r.Status == ReservationStatus.Approved && r.StartDateTime >= DateTime.Now)
                    .OrderBy(r => r.StartDateTime)
                    .Take(5)
                    .ToList(),
                RecentNotifications = await _db.Notifications
                    .Where(n => n.UserId == user.Id)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(5)
                    .ToListAsync()
            };

            return View(vm);
        }

        // POST: /Home/MarkNotificationsRead
        [HttpPost, Authorize]
        public async Task<IActionResult> MarkNotificationsRead()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var notifs = await _db.Notifications
                    .Where(n => n.UserId == user.Id && !n.IsRead).ToListAsync();
                notifs.ForEach(n => n.IsRead = true);
                await _db.SaveChangesAsync();
            }
            return Ok();
        }

        // GET: /Home/GetNotifications
        [Authorize]
        public async Task<IActionResult> GetNotifications()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new List<object>());

            var notifs = await _db.Notifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(10)
                .Select(n => new
                {
                    n.NotificationId,
                    n.Title,
                    n.Message,
                    n.IsRead,
                    n.NotificationType,
                    n.RelatedReservationId,
                    CreatedAt = n.CreatedAt.ToString("MMM dd, yyyy hh:mm tt")
                })
                .ToListAsync();

            return Json(notifs);
        }
    }
}
