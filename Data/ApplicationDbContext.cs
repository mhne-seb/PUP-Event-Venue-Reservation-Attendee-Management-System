using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PUPEventVenue.Models;

namespace PUPEventVenue.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Venue> Venues { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<Attendee> Attendees { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Payment> Payments { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Venue
            builder.Entity<Venue>(e =>
            {
                e.HasKey(v => v.VenueId);
                e.Property(v => v.Name).IsRequired().HasMaxLength(200);
                e.Property(v => v.PricePerHour).HasColumnType("decimal(10,2)");
            });

            // Reservation
            builder.Entity<Reservation>(e =>
            {
                e.HasKey(r => r.ReservationId);

                e.HasOne(r => r.Venue)
                 .WithMany(v => v.Reservations)
                 .HasForeignKey(r => r.VenueId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(r => r.Organizer)
                 .WithMany(u => u.Reservations)
                 .HasForeignKey(r => r.OrganizerId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(r => r.ApproverAdmin)
                 .WithMany()
                 .HasForeignKey(r => r.ApprovedBy)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // Attendee
            builder.Entity<Attendee>(e =>
            {
                e.HasKey(a => a.AttendeeId);
                e.HasIndex(a => a.RegistrationToken).IsUnique();

                e.HasOne(a => a.Reservation)
                 .WithMany(r => r.Attendees)
                 .HasForeignKey(a => a.ReservationId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // Notification
            builder.Entity<Notification>(e =>
            {
                e.HasKey(n => n.NotificationId);

                e.HasOne(n => n.User)
                 .WithMany(u => u.Notifications)
                 .HasForeignKey(n => n.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(n => n.RelatedReservation)
                 .WithMany()
                 .HasForeignKey(n => n.RelatedReservationId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // Payment
            builder.Entity<Payment>(e =>
            {
                e.HasKey(p => p.PaymentId);

                e.HasOne(p => p.Reservation)
                 .WithOne(r => r.Payment)
                 .HasForeignKey<Payment>(p => p.ReservationId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(p => p.Organizer)
                 .WithMany()
                 .HasForeignKey(p => p.OrganizerId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(p => p.VerifierAdmin)
                 .WithMany()
                 .HasForeignKey(p => p.VerifiedBy)
                 .OnDelete(DeleteBehavior.SetNull);

                e.Property(p => p.Amount).HasColumnType("decimal(10,2)");
                e.Property(p => p.HourlyRate).HasColumnType("decimal(10,2)");
            });
        }
    }
}
