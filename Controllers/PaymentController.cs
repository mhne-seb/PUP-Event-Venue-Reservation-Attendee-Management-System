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
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPaymentService _paymentService;
        private readonly INotificationService _notif;
        private readonly IWebHostEnvironment _env;

        public PaymentController(ApplicationDbContext db,
            UserManager<ApplicationUser> um, IPaymentService ps,
            INotificationService notif, IWebHostEnvironment env)
        {
            _db = db; _userManager = um; _paymentService = ps; _notif = notif; _env = env;
        }

        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var payment = await _paymentService.GetPaymentByIdAsync(id);
            if (payment == null) return NotFound();
            if (!User.IsInRole("Admin") && payment.OrganizerId != user!.Id)
                return Forbid();
            return View(payment);
        }

        public async Task<IActionResult> SubmitReceipt(int reservationId)
        {
            var user = await _userManager.GetUserAsync(User);
            var reservation = await _db.Reservations
                .Include(r => r.Venue)
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId);

            if (reservation == null) return NotFound();
            if (reservation.OrganizerId != user!.Id && !User.IsInRole("Admin"))
                return Forbid();

            var existingPayment = await _paymentService.GetPaymentByReservationAsync(reservationId);
            if (existingPayment != null &&
                (existingPayment.Status == PaymentStatus.Verified ||
                 existingPayment.Status == PaymentStatus.Approved))
            {
                TempData["Info"] = "This payment has already been verified.";
                return RedirectToAction("Details", new { id = existingPayment.PaymentId });
            }

            var payment = existingPayment
                ?? await _paymentService.CreatePaymentAsync(reservationId, reservation.OrganizerId);

            var model = new PaymentSubmitViewModel
            {
                PaymentId = payment.PaymentId,
                ReservationId = reservationId,
                EventName = reservation.EventName,
                VenueName = reservation.Venue?.Name,
                Amount = payment.Amount,
                HourlyRate = payment.HourlyRate,
                DurationHours = payment.DurationHours
            };

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReceipt(PaymentSubmitViewModel model, IFormFile? receiptFile)
        {
            var user = await _userManager.GetUserAsync(User);
            var payment = await _paymentService.GetPaymentByIdAsync(model.PaymentId);
            if (payment == null) return NotFound();
            if (payment.OrganizerId != user!.Id && !User.IsInRole("Admin"))
                return Forbid();

            if (receiptFile == null || receiptFile.Length == 0)
            {
                ModelState.AddModelError("ReceiptFile", "Please upload a receipt image.");
                return View(model);
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            var ext = Path.GetExtension(receiptFile.FileName).ToLower();
            if (!allowedExtensions.Contains(ext))
            {
                ModelState.AddModelError("ReceiptFile", "Only JPG, PNG, and PDF files are allowed.");
                return View(model);
            }

            const long maxSize = 5 * 1024 * 1024;
            if (receiptFile.Length > maxSize)
            {
                ModelState.AddModelError("ReceiptFile", "File size must not exceed 5 MB.");
                return View(model);
            }

            try
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "receipts");
                Directory.CreateDirectory(uploadsFolder);
                var uniqueFileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await receiptFile.CopyToAsync(stream);

                var receiptUrl = $"/uploads/receipts/{uniqueFileName}";
                var success = await _paymentService.SubmitReceiptAsync(model.PaymentId, receiptUrl);

                if (!success)
                {
                    TempData["Error"] = "Failed to submit receipt. Please try again.";
                    return View(model);
                }

                TempData["Success"] = "Receipt submitted successfully! Admin will verify within 24 hours.";
                return RedirectToAction("Details", new { id = model.PaymentId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error uploading file: {ex.Message}";
                return View(model);
            }
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminVerify(string? status)
        {
            List<Payment> payments;
            if (status == "rejected")
            {
                payments = await _db.Payments
                    .Where(p => p.Status == PaymentStatus.Rejected)
                    .Include(p => p.Reservation).ThenInclude(r => r!.Venue)
                    .Include(p => p.Organizer)
                    .OrderByDescending(p => p.UpdatedAt)
                    .ToListAsync();
            }
            else if (status == "verified")
            {
                payments = await _db.Payments
                    .Where(p => p.Status == PaymentStatus.Verified || p.Status == PaymentStatus.Approved)
                    .Include(p => p.Reservation).ThenInclude(r => r!.Venue)
                    .Include(p => p.Organizer)
                    .OrderByDescending(p => p.VerifiedAt)
                    .ToListAsync();
            }
            else
            {
                payments = await _paymentService.GetPendingPaymentsAsync();
            }

            ViewBag.Status = status;
            ViewBag.PendingCount = await _paymentService.GetPendingPaymentCountAsync();
            return View(payments);
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(int paymentId, string? notes)
        {
            var admin = await _userManager.GetUserAsync(User);
            var success = await _paymentService.VerifyPaymentAsync(paymentId, admin!.Id, notes ?? "");
            TempData[success ? "Success" : "Error"] =
                success ? "Payment approved and reservation confirmed. Organizer has been notified."
                        : "Failed to approve payment.";
            return RedirectToAction(nameof(AdminVerify));
        }

        [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int paymentId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "A rejection reason is required.";
                return RedirectToAction(nameof(AdminVerify));
            }

            var admin = await _userManager.GetUserAsync(User);
            var success = await _paymentService.RejectPaymentAsync(paymentId, admin!.Id, reason);
            TempData[success ? "Warning" : "Error"] =
                success ? "Payment rejected. Organizer has been notified."
                        : "Failed to reject payment.";
            return RedirectToAction(nameof(AdminVerify));
        }

        public async Task<IActionResult> MyPayments()
        {
            var user = await _userManager.GetUserAsync(User);
            var payments = await _paymentService.GetPaymentsByOrganizerAsync(user!.Id);
            return View(payments);
        }
    }
}