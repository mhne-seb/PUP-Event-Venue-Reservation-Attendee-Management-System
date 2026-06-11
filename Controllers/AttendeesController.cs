using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PUPEventVenue.Data;
using PUPEventVenue.Models;
using PUPEventVenue.ViewModels;

namespace PUPEventVenue.Controllers
{
    public class AttendeesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public AttendeesController(ApplicationDbContext db, UserManager<ApplicationUser> um)
        {
            _db = db; _userManager = um;
        }

        // GET: /Attendees/Register?token=xxx  (public registration link)
        [AllowAnonymous]
        public async Task<IActionResult> Register(string token)
        {
            // Token encodes the reservationId (base64)
            if (string.IsNullOrEmpty(token)) return BadRequest();
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token));
            if (!int.TryParse(decoded, out int reservationId)) return BadRequest();

            var res = await _db.Reservations
                .Include(r => r.Venue)
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId
                                       && r.Status == ReservationStatus.Approved);
            if (res == null) return NotFound();

            var vm = new AttendeeRegisterViewModel
            {
                ReservationId = reservationId,
                Reservation = res
            };
            return View(vm);
        }

        // POST: /Attendees/Register
        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(AttendeeRegisterViewModel model)
        {
            var res = await _db.Reservations
                .Include(r => r.Venue)
                .Include(r => r.Attendees)
                .FirstOrDefaultAsync(r => r.ReservationId == model.ReservationId
                                       && r.Status == ReservationStatus.Approved);

            if (res == null) return NotFound();
            model.Reservation = res;

            // Check duplicate email
            bool duplicate = res.Attendees.Any(a =>
                a.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase));
            if (duplicate)
                ModelState.AddModelError("Email", "This email is already registered for this event.");

            // Check capacity
            if (res.Attendees.Count >= res.Venue!.Capacity)
                ModelState.AddModelError("", "Sorry, this event has reached its maximum capacity.");

            if (!ModelState.IsValid) return View(model);

            var attendee = new Attendee
            {
                ReservationId = model.ReservationId,
                FullName = model.FullName,
                Email = model.Email,
                StudentNumber = model.StudentNumber,
                Department = model.Department,
                PhoneNumber = model.PhoneNumber,
                RegistrationToken = Guid.NewGuid().ToString("N")
            };

            _db.Attendees.Add(attendee);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Successfully registered! You're confirmed for \"{res.EventName}\".";
            return RedirectToAction(nameof(Confirmation), new { id = attendee.AttendeeId });
        }

        // GET: /Attendees/Confirmation/5
        [AllowAnonymous]
        public async Task<IActionResult> Confirmation(int id)
        {
            var attendee = await _db.Attendees
                .Include(a => a.Reservation)
                    .ThenInclude(r => r!.Venue)
                .FirstOrDefaultAsync(a => a.AttendeeId == id);
            if (attendee == null) return NotFound();
            return View(attendee);
        }

        // GET: /Attendees/List/5  (organizer or admin)
        [Authorize]
        public async Task<IActionResult> List(int reservationId)
        {
            var user = await _userManager.GetUserAsync(User);
            var res = await _db.Reservations
                .Include(r => r.Venue)
                .Include(r => r.Attendees)
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId);

            if (res == null) return NotFound();
            if (!User.IsInRole("Admin") && res.OrganizerId != user!.Id)
                return Forbid();

            ViewBag.RegistrationLink = GenerateRegistrationLink(reservationId);
            return View(res);
        }

        // POST: /Attendees/MarkAttended
        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAttended(int attendeeId, int reservationId)
        {
            var attendee = await _db.Attendees.FindAsync(attendeeId);
            if (attendee == null) return NotFound();

            attendee.HasAttended = !attendee.HasAttended;
            attendee.AttendedAt = attendee.HasAttended ? DateTime.UtcNow : null;
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(List), new { reservationId });
        }

        // POST: /Attendees/Delete
        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int attendeeId, int reservationId)
        {
            var attendee = await _db.Attendees.FindAsync(attendeeId);
            if (attendee != null)
            {
                _db.Attendees.Remove(attendee);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Attendee removed.";
            }
            return RedirectToAction(nameof(List), new { reservationId });
        }

        // GET: /Attendees/ExportCsv/5
        [Authorize]
        public async Task<IActionResult> ExportCsv(int reservationId)
        {
            var res = await _db.Reservations
                .Include(r => r.Attendees)
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId);
            if (res == null) return NotFound();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Full Name,Email,Student Number,Department,Phone,Registered At,Attended");
            foreach (var a in res.Attendees)
                csv.AppendLine($"\"{a.FullName}\",{a.Email},{a.StudentNumber},{a.Department},{a.PhoneNumber},{a.RegisteredAt:yyyy-MM-dd HH:mm},{(a.HasAttended ? "Yes" : "No")}");

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"attendees_{reservationId}.csv");
        }

        private string GenerateRegistrationLink(int reservationId)
        {
            var token = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(reservationId.ToString()));
            return $"{Request.Scheme}://{Request.Host}/Attendees/Register?token={token}";
        }
    }
}
