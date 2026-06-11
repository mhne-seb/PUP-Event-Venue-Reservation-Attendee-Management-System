using Microsoft.EntityFrameworkCore;
using PUPEventVenue.Data;
using PUPEventVenue.Models;

namespace PUPEventVenue.Services
{
    public interface INotificationService
    {
        Task SendAsync(string userId, string title, string message, string? type = null, int? reservationId = null);
        Task<List<Notification>> GetUnreadAsync(string userId);
        Task MarkAllReadAsync(string userId);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _db;
        public NotificationService(ApplicationDbContext db) => _db = db;

        public async Task SendAsync(string userId, string title, string message,
            string? type = null, int? reservationId = null)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                NotificationType = type,
                RelatedReservationId = reservationId
            });
            await _db.SaveChangesAsync();
        }

        public async Task<List<Notification>> GetUnreadAsync(string userId) =>
            await _db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .Take(10)
                .ToListAsync();

        public async Task MarkAllReadAsync(string userId)
        {
            var notifs = await _db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();
            notifs.ForEach(n => n.IsRead = true);
            await _db.SaveChangesAsync();
        }
    }
}
