using System.ComponentModel.DataAnnotations;
using PUPEventVenue.Models;

namespace PUPEventVenue.ViewModels
{
    // ─── Account ─────────────────────────────────────────────────
    public class RegisterViewModel
    {
        [Required, StringLength(200)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Student Number")]
        public string? StudentNumber { get; set; }

        [StringLength(200)]
        [Display(Name = "Organization Name")]
        public string? OrganizationName { get; set; }

        [StringLength(200)]
        [Display(Name = "Department / College")]
        public string? Department { get; set; }

        [Required, StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class LoginViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; }
    }

    // ─── Venue ───────────────────────────────────────────────────
    public class VenueViewModel
    {
        public int VenueId { get; set; }

        [Required, StringLength(200)]
        [Display(Name = "Venue Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [StringLength(500)]
        [Display(Name = "Location / Room")]
        public string? Location { get; set; }

        [Required, Range(1, 100000)]
        [Display(Name = "Capacity")]
        public int Capacity { get; set; }

        [Display(Name = "Amenities (comma-separated)")]
        public string? Amenities { get; set; }

        [Display(Name = "Is Available")]
        public bool IsAvailable { get; set; } = true;

        [Range(0, 999999.99)]
        [Display(Name = "Price Per Hour (₱)")]
        public decimal PricePerHour { get; set; } = 0;

        [Display(Name = "Current Image")]
        public string? ExistingImageUrl { get; set; }

        [Display(Name = "Upload New Image")]
        public IFormFile? ImageFile { get; set; }
    }

    // ─── Reservation ─────────────────────────────────────────────
    public class ReservationCreateViewModel
    {
        public int VenueId { get; set; }
        public Venue? Venue { get; set; }

        [Required, StringLength(300)]
        [Display(Name = "Event Name")]
        public string EventName { get; set; } = string.Empty;

        [Display(Name = "Event Description")]
        public string? EventDescription { get; set; }

        [Display(Name = "Event Type")]
        public string? EventType { get; set; }

        [Required]
        [Display(Name = "Start Date & Time")]
        public DateTime StartDateTime { get; set; } = DateTime.Now.AddDays(1);

        [Required]
        [Display(Name = "End Date & Time")]
        public DateTime EndDateTime { get; set; } = DateTime.Now.AddDays(1).AddHours(2);

        [Required, Range(1, 100000)]
        [Display(Name = "Expected Number of Attendees")]
        public int ExpectedAttendees { get; set; }
    }

    public class ReservationActionViewModel
    {
        public int ReservationId { get; set; }
        public string Action { get; set; } = string.Empty; // Approve / Reject
        public string? Notes { get; set; }
        public string? RejectionReason { get; set; }
    }

    // ─── Attendee ────────────────────────────────────────────────
    public class AttendeeRegisterViewModel
    {
        [Required]
        public int ReservationId { get; set; }
        public Reservation? Reservation { get; set; }

        [Required, StringLength(200)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Student Number")]
        public string? StudentNumber { get; set; }

        [StringLength(200)]
        [Display(Name = "Department / College")]
        public string? Department { get; set; }

        [StringLength(50)]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }
    }

    // ─── Dashboard / Analytics ───────────────────────────────────
    public class AdminDashboardViewModel
    {
        public int TotalVenues { get; set; }
        public int TotalReservations { get; set; }
        public int PendingReservations { get; set; }
        public int ApprovedReservations { get; set; }
        public int RejectedReservations { get; set; }
        public int TotalAttendees { get; set; }
        public int TotalUsers { get; set; }
        public List<Reservation> RecentReservations { get; set; } = new();
        public List<VenueUsageStat> VenueUsageStats { get; set; } = new();
        public List<MonthlyStatItem> MonthlyStats { get; set; } = new();
    }

    public class VenueUsageStat
    {
        public string VenueName { get; set; } = string.Empty;
        public int ReservationCount { get; set; }
        public int TotalAttendees { get; set; }
    }

    public class MonthlyStatItem
    {
        public string Month { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class OrganizerDashboardViewModel
    {
        public int TotalReservations { get; set; }
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public List<Reservation> UpcomingEvents { get; set; } = new();
        public List<Notification> RecentNotifications { get; set; } = new();
    }
}
