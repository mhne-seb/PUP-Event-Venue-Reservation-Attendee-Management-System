using System.ComponentModel.DataAnnotations;

namespace PUPEventVenue.ViewModels
{
    public class PaymentSubmitViewModel
    {
        public int PaymentId { get; set; }
        public int ReservationId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string? VenueName { get; set; }
        public decimal Amount { get; set; }
        public decimal HourlyRate { get; set; }
        public int DurationHours { get; set; }
        public IFormFile? ReceiptFile { get; set; }
    }
}
