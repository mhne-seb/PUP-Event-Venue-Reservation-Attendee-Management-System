using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PUPEventVenue.Models
{
    // ─── Identity User ───────────────────────────────────────────
    public class ApplicationUser : IdentityUser
    {
        [Required, StringLength(200)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(50)]
        public string? StudentNumber { get; set; }

        [StringLength(200)]
        public string? OrganizationName { get; set; }

        [StringLength(200)]
        public string? Department { get; set; }

        public string? ProfileImageUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }

    // ─── Venue ────────────────────────────────────────────────────
    public class Venue
    {
        public int VenueId { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [StringLength(500)]
        public string? Location { get; set; }

        [Required, Range(1, 100000)]
        public int Capacity { get; set; }

        public string? ImageUrl { get; set; }
        public string? Amenities { get; set; }
        public bool IsAvailable { get; set; } = true;

        [Column(TypeName = "decimal(10,2)")]
        public decimal PricePerHour { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

        [NotMapped]
        public List<string> AmenitiesList =>
            string.IsNullOrEmpty(Amenities)
                ? new()
                : Amenities.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(a => a.Trim()).ToList();
    }

    // ─── Reservation ──────────────────────────────────────────────
    public class Reservation
    {
        public int ReservationId { get; set; }

        [Required]
        public int VenueId { get; set; }

        [Required]
        public string OrganizerId { get; set; } = string.Empty;

        [Required, StringLength(300)]
        public string EventName { get; set; } = string.Empty;

        public string? EventDescription { get; set; }

        [StringLength(100)]
        public string? EventType { get; set; }

        [Required]
        public DateTime StartDateTime { get; set; }

        [Required]
        public DateTime EndDateTime { get; set; }

        [Range(1, 100000)]
        public int ExpectedAttendees { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = ReservationStatus.Pending;

        public string? AdminNotes { get; set; }
        public string? RejectionReason { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Venue? Venue { get; set; }
        public ApplicationUser? Organizer { get; set; }
        public ApplicationUser? ApproverAdmin { get; set; }
        public ICollection<Attendee> Attendees { get; set; } = new List<Attendee>();
        public Payment? Payment { get; set; }

        [NotMapped]
        public TimeSpan Duration => EndDateTime - StartDateTime;

        [NotMapped]
        public int AttendeeCount => Attendees?.Count ?? 0;
    }

public static class ReservationStatus
{
    public const string Pending = "Pending";
    public const string PendingPayment = "PendingPayment";
    public const string PaymentVerified = "PaymentVerified";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
    public const string InProgress = "InProgress";      // EVENT STARTED
    public const string Completed = "Completed";        // EVENT ENDED
    public const string NoShow = "NoShow";              // EVENT ENDED WITH NO ATTENDEES
}

// Add this new model after the Attendee class
public class EventLog
{
    public int EventLogId { get; set; }

    [Required]
    public int ReservationId { get; set; }

    [Required]
    public string EventType { get; set; } = string.Empty; // "EventStarted", "EventEnded", "AttendeeCheckedIn"

    public string? Description { get; set; }
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    public Reservation? Reservation { get; set; }
}

    // ─── Attendee ─────────────────────────────────────────────────
    public class Attendee
    {
        public int AttendeeId { get; set; }

        [Required]
        public int ReservationId { get; set; }

        [Required, StringLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [StringLength(50)]
        public string? StudentNumber { get; set; }

        [StringLength(200)]
        public string? Department { get; set; }

        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        public bool HasAttended { get; set; } = false;
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
        public DateTime? AttendedAt { get; set; }
        public string? RegistrationToken { get; set; }

        public Reservation? Reservation { get; set; }
    }

    // ─── Notification ─────────────────────────────────────────────
    public class Notification
    {
        public int NotificationId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required, StringLength(300)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;

        [StringLength(100)]
        public string? NotificationType { get; set; }

        public int? RelatedReservationId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser? User { get; set; }
        public Reservation? RelatedReservation { get; set; }
    }

    // ─── Payment ──────────────────────────────────────────────────
    public class Payment
    {
        public int PaymentId { get; set; }

        [Required]
        public int ReservationId { get; set; }

        [Required]
        public string OrganizerId { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [Required]
        public decimal HourlyRate { get; set; }

        [Required]
        public int DurationHours { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = PaymentStatus.Pending;

        [StringLength(300)]
        public string? ReceiptImageUrl { get; set; }

        public string? VerificationNotes { get; set; }
        public string? VerifiedBy { get; set; }
        public DateTime? VerifiedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Reservation? Reservation { get; set; }
        public ApplicationUser? Organizer { get; set; }
        public ApplicationUser? VerifierAdmin { get; set; }
    }

    public static class PaymentStatus
    {
        public const string Pending = "Pending";
        public const string Submitted = "Submitted";
        public const string Verified = "Verified";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }
}
