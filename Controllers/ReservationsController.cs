using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PUPEventVenue.Data;
using PUPEventVenue.Models;
using PUPEventVenue.Services;
using PUPEventVenue.ViewModels;

namespace PUPEventVenue.Controllers
{
    [Authorize]
    public class ReservationsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notif;

        public ReservationsController(ApplicationDbContext db,
            UserManager<ApplicationUser> um, INotificationService notif)
        {
            _db = db; _userManager = um; _notif = notif;
        }

        // GET: /Reservations  (organizer sees own; admin sees all)
        public async Task<IActionResult> Index(string? status, string? search)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var query = _db.Reservations
                .Include(r => r.Venue)
                .Include(r => r.Organizer)
                .AsQueryable();

            if (!User.IsInRole("Admin"))
                query = query.Where(r => r.OrganizerId == user.Id);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Status == status);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(r => r.EventName.Contains(search)
                    || (r.Venue != null && r.Venue.Name.Contains(search)));

            ViewBag.Status = status;
            ViewBag.Search = search;
            return View(await query.OrderByDescending(r => r.CreatedAt).ToListAsync());
        }

        // GET: /Reservations/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var res = await _db.Reservations
                .Include(r => r.Venue)
                .Include(r => r.Organizer)
                .Include(r => r.Attendees)
                .Include(r => r.Payment)
                .FirstOrDefaultAsync(r => r.ReservationId == id);

            if (res == null) return NotFound();
            if (!User.IsInRole("Admin") && res.OrganizerId != user!.Id)
                return Forbid();

            return View(res);
        }

        // GET: /Reservations/Create?venueId=1
        public async Task<IActionResult> Create(int venueId)
        {
            var venue = await _db.Venues.FindAsync(venueId);
            if (venue == null || !venue.IsAvailable)
            {
                TempData["Error"] = "This venue is not available for reservations.";
                return RedirectToAction("Index", "Venues");
            }
            return View(new ReservationCreateViewModel
            {
                VenueId = venueId,
                Venue = venue,
                StartDateTime = DateTime.Now.AddDays(1).Date.AddHours(8),
                EndDateTime = DateTime.Now.AddDays(1).Date.AddHours(10)
            });
        }

        // POST: /Reservations/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReservationCreateViewModel model)
        {
            if (model.EndDateTime <= model.StartDateTime)
                ModelState.AddModelError("EndDateTime", "End time must be after start time.");

            if (model.StartDateTime < DateTime.Now)
                ModelState.AddModelError("StartDateTime", "Start date must be in the future.");

            // Check for conflicts
            var conflict = await _db.Reservations.AnyAsync(r =>
                r.VenueId == model.VenueId
                && r.Status == ReservationStatus.Approved
                && r.StartDateTime < model.EndDateTime
                && r.EndDateTime > model.StartDateTime);

            if (conflict)
                ModelState.AddModelError("", "The venue is already booked for the selected time slot.");

            if (!ModelState.IsValid)
            {
                model.Venue = await _db.Venues.FindAsync(model.VenueId);
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            var reservation = new Reservation
            {
                VenueId = model.VenueId,
                OrganizerId = user!.Id,
                EventName = model.EventName,
                EventDescription = model.EventDescription,
                EventType = model.EventType,
                StartDateTime = model.StartDateTime,
                EndDateTime = model.EndDateTime,
                ExpectedAttendees = model.ExpectedAttendees,
                Status = ReservationStatus.Pending
            };

            _db.Reservations.Add(reservation);
            await _db.SaveChangesAsync();

            // Notify admins
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            foreach (var admin in admins)
                await _notif.SendAsync(admin.Id,
                    "New Reservation Request",
                    $"{user.FullName} submitted a reservation for {reservation.EventName}.",
                    "NewRequest", reservation.ReservationId);

            // If venue has a fee, create payment record and send organizer to pay
            var venue = await _db.Venues.FindAsync(model.VenueId);
            if (venue != null && venue.PricePerHour > 0)
            {
                TempData["Success"] = "✅ Reservation submitted! Please complete your payment to confirm.";
                return RedirectToAction("SubmitReceipt", "Payment",
                    new { reservationId = reservation.ReservationId });
            }

            TempData["Success"] = "Reservation submitted successfully! Waiting for admin approval.";
            return RedirectToAction(nameof(Details), new { id = reservation.ReservationId });
        }

        // POST: /Reservations/Cancel/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var res = await _db.Reservations.FindAsync(id);
            if (res == null) return NotFound();
            if (res.OrganizerId != user!.Id && !User.IsInRole("Admin")) return Forbid();

            res.Status = ReservationStatus.Cancelled;
            res.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Reservation cancelled.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Reservations/Approve  [Admin only]
        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(ReservationActionViewModel model)
        {
            var admin = await _userManager.GetUserAsync(User);
            var res = await _db.Reservations
                .Include(r => r.Venue)
                .FirstOrDefaultAsync(r => r.ReservationId == model.ReservationId);
            if (res == null) return NotFound();

            res.Status = ReservationStatus.Approved;
            res.ApprovedBy = admin!.Id;
            res.ApprovedAt = DateTime.UtcNow;
            res.AdminNotes = model.Notes;
            res.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _notif.SendAsync(res.OrganizerId,
                "Reservation Approved! 🎉",
                $"Your reservation for \"{res.EventName}\" at {res.Venue?.Name} has been approved.",
                "Approved", res.ReservationId);

            TempData["Success"] = "Reservation approved.";
            return RedirectToAction(nameof(Details), new { id = res.ReservationId });
        }

        // POST: /Reservations/Reject  [Admin only]
        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(ReservationActionViewModel model)
        {
            var admin = await _userManager.GetUserAsync(User);
            var res = await _db.Reservations
                .Include(r => r.Venue)
                .FirstOrDefaultAsync(r => r.ReservationId == model.ReservationId);
            if (res == null) return NotFound();

            res.Status = ReservationStatus.Rejected;
            res.RejectionReason = model.RejectionReason;
            res.AdminNotes = model.Notes;
            res.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _notif.SendAsync(res.OrganizerId,
                "Reservation Rejected",
                $"Your reservation for \"{res.EventName}\" was rejected. Reason: {model.RejectionReason}",
                "Rejected", res.ReservationId);

            TempData["Warning"] = "Reservation rejected.";
            return RedirectToAction(nameof(Details), new { id = res.ReservationId });
        }
    }
}
