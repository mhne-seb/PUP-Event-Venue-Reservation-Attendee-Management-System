using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PUPEventVenue.Data;
using PUPEventVenue.Models;

namespace PUPEventVenue.Services
{
    public interface IPaymentService
    {
        Task<Payment> CreatePaymentAsync(int reservationId, string organizerId);
        Task<Payment?> GetPaymentByReservationAsync(int reservationId);
        Task<Payment?> GetPaymentByIdAsync(int paymentId);
        Task<bool> SubmitReceiptAsync(int paymentId, string receiptImageUrl);
        Task<bool> VerifyPaymentAsync(int paymentId, string verifierId, string notes);
        Task<bool> RejectPaymentAsync(int paymentId, string verifierId, string reason);
        Task<List<Payment>> GetPendingPaymentsAsync();
        Task<List<Payment>> GetPaymentsByOrganizerAsync(string organizerId);
        Task<int> GetPendingPaymentCountAsync();
    }

    public class PaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notif;
        private readonly UserManager<ApplicationUser> _userManager;

        public PaymentService(ApplicationDbContext db, INotificationService notif,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _notif = notif;
            _userManager = userManager;
        }

        public async Task<Payment> CreatePaymentAsync(int reservationId, string organizerId)
        {
            var reservation = await _db.Reservations
                .Include(r => r.Venue)
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId);

            if (reservation == null)
                throw new InvalidOperationException("Reservation not found.");
            if (reservation.Venue == null)
                throw new InvalidOperationException("Venue not found.");

            var durationHours = (int)Math.Ceiling(
                (reservation.EndDateTime - reservation.StartDateTime).TotalHours);
            var amount = durationHours * reservation.Venue.PricePerHour;

            var payment = new Payment
            {
                ReservationId = reservationId,
                OrganizerId = organizerId,
                Amount = amount,
                HourlyRate = reservation.Venue.PricePerHour,
                DurationHours = durationHours,
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();
            return payment;
        }

        public async Task<Payment?> GetPaymentByReservationAsync(int reservationId) =>
            await _db.Payments
                .Include(p => p.Organizer)
                .Include(p => p.VerifierAdmin)
                .FirstOrDefaultAsync(p => p.ReservationId == reservationId);

        public async Task<Payment?> GetPaymentByIdAsync(int paymentId) =>
            await _db.Payments
                .Include(p => p.Reservation).ThenInclude(r => r!.Venue)
                .Include(p => p.Organizer)
                .Include(p => p.VerifierAdmin)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

        public async Task<bool> SubmitReceiptAsync(int paymentId, string receiptImageUrl)
        {
            var payment = await _db.Payments
                .Include(p => p.Reservation)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);
            if (payment == null) return false;

            payment.ReceiptImageUrl = receiptImageUrl;
            payment.Status = PaymentStatus.Submitted;
            payment.UpdatedAt = DateTime.UtcNow;

            if (payment.Reservation != null)
            {
                payment.Reservation.Status = ReservationStatus.PendingPayment;
                payment.Reservation.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            // Notify all admins — using UserManager to get role members correctly
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            foreach (var admin in admins)
            {
                await _notif.SendAsync(admin.Id,
                    "💳 Payment Receipt Submitted",
                    $"A payment receipt has been submitted for verification. " +
                    $"Event: \"{payment.Reservation?.EventName}\" — Amount: ₱{payment.Amount:F2}",
                    "PaymentSubmitted", payment.ReservationId);
            }

            return true;
        }

        public async Task<bool> VerifyPaymentAsync(int paymentId, string verifierId, string notes)
        {
            var payment = await _db.Payments
                .Include(p => p.Reservation).ThenInclude(r => r!.Venue)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);
            if (payment == null) return false;

            payment.Status = PaymentStatus.Verified;
            payment.VerificationNotes = notes;
            payment.VerifiedBy = verifierId;
            payment.VerifiedAt = DateTime.UtcNow;
            payment.UpdatedAt = DateTime.UtcNow;

            // Payment verified → reservation becomes Approved automatically
            if (payment.Reservation != null)
            {
                payment.Reservation.Status = ReservationStatus.Approved;
                payment.Reservation.ApprovedBy = verifierId;
                payment.Reservation.ApprovedAt = DateTime.UtcNow;
                payment.Reservation.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            // Notify organizer of approval
            await _notif.SendAsync(payment.OrganizerId,
                "✅ Payment Approved — Reservation Confirmed!",
                $"Your payment of ₱{payment.Amount:F2} for " +
                $"\"{payment.Reservation?.EventName}\" at {payment.Reservation?.Venue?.Name} " +
                $"has been verified and approved. Your reservation is now CONFIRMED!",
                "PaymentApproved", payment.ReservationId);

            return true;
        }

        public async Task<bool> RejectPaymentAsync(int paymentId, string verifierId, string reason)
        {
            var payment = await _db.Payments
                .Include(p => p.Reservation)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);
            if (payment == null) return false;

            payment.Status = PaymentStatus.Rejected;
            payment.VerificationNotes = reason;
            payment.VerifiedBy = verifierId;
            payment.VerifiedAt = DateTime.UtcNow;
            payment.UpdatedAt = DateTime.UtcNow;

            // Reservation goes back to pending payment so organizer can resubmit
            if (payment.Reservation != null)
            {
                payment.Reservation.Status = ReservationStatus.Pending;
                payment.Reservation.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            await _notif.SendAsync(payment.OrganizerId,
                "❌ Payment Receipt Rejected",
                $"Your payment receipt for \"{payment.Reservation?.EventName}\" was rejected. " +
                $"Reason: {reason}. Please submit a new receipt.",
                "PaymentRejected", payment.ReservationId);

            return true;
        }

        public async Task<List<Payment>> GetPendingPaymentsAsync() =>
            await _db.Payments
                .Where(p => p.Status == PaymentStatus.Submitted)
                .Include(p => p.Reservation).ThenInclude(r => r!.Venue)
                .Include(p => p.Organizer)
                .OrderByDescending(p => p.UpdatedAt)
                .ToListAsync();

        public async Task<List<Payment>> GetPaymentsByOrganizerAsync(string organizerId) =>
            await _db.Payments
                .Where(p => p.OrganizerId == organizerId)
                .Include(p => p.Reservation).ThenInclude(r => r!.Venue)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

        public async Task<int> GetPendingPaymentCountAsync() =>
            await _db.Payments.CountAsync(p => p.Status == PaymentStatus.Submitted);
    }
}
