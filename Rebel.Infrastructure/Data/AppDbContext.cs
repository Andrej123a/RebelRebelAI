using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Rebel.Domain.Entities;

namespace Rebel.Infrastructure.Data
{
    public class AppDbContext : IdentityDbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<ContactInfo> ContactInfos { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<ReservationActivity> ReservationActivities { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<PubTable> PubTables { get; set; }
        public DbSet<StaffMember> StaffMembers { get; set; }
        public DbSet<StaffShift> StaffShifts { get; set; }
        public DbSet<BeerGuideFeedback> BeerGuideFeedbacks { get; set; }
        public DbSet<BeerGuideResponseContext> BeerGuideResponseContexts { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<PubTable>(entity =>
            {
                entity.Property(table => table.Label)
                    .IsRequired()
                    .HasMaxLength(40);

                entity.Property(table => table.Area)
                    .HasMaxLength(80);

                entity.HasIndex(table => table.Label)
                    .IsUnique();
            });

            builder.Entity<Category>(entity =>
            {
                entity.Property(c => c.DeletedAtUtc)
                    .HasColumnType("timestamp with time zone");

                entity.HasQueryFilter(c => !c.IsDeleted);
            });

            builder.Entity<Event>(entity =>
            {
                entity.Property(e => e.MaxReservations);

                entity.Property(e => e.MaxGuests);

                entity.Property(e => e.DeletedAtUtc)
                    .HasColumnType("timestamp with time zone");

                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            builder.Entity<Product>(entity =>
            {
                entity.Property(p => p.DeletedAtUtc)
                    .HasColumnType("timestamp with time zone");

                entity.Property(p => p.AlcoholByVolume)
                    .HasPrecision(4, 1);

                entity.Property(p => p.OriginCountry)
                    .HasMaxLength(60);

                entity.Property(p => p.BeerStyle)
                    .HasMaxLength(80);

                entity.Property(p => p.FlavorNotes)
                    .HasMaxLength(300);

                entity.Property(p => p.PairingTags)
                    .HasMaxLength(300);

                entity.HasQueryFilter(p => !p.IsDeleted);
            });

            builder.Entity<BeerGuideFeedback>(entity =>
            {
                entity.Property(feedback => feedback.AnonymousSessionHash)
                    .IsRequired()
                    .HasMaxLength(64);

                entity.Property(feedback => feedback.Reason)
                    .HasConversion<string>()
                    .HasMaxLength(30);

                entity.Property(feedback => feedback.CreatedAtUtc)
                    .HasColumnType("timestamp with time zone");

                entity.Property(feedback => feedback.RequestedStyle).HasMaxLength(40);
                entity.Property(feedback => feedback.FlavourTags).HasMaxLength(160);
                entity.Property(feedback => feedback.RequestedOrigin).HasMaxLength(40);
                entity.Property(feedback => feedback.StrengthPreference).HasMaxLength(20);
                entity.Property(feedback => feedback.BitternessPreference).HasMaxLength(20);
                entity.Property(feedback => feedback.SweetnessPreference).HasMaxLength(20);
                entity.Property(feedback => feedback.FoodPairing).HasMaxLength(40);

                entity.HasIndex(feedback => new
                {
                    feedback.ResponseId,
                    feedback.AnonymousSessionHash,
                    feedback.ProductId
                }).IsUnique();

                entity.HasIndex(feedback => new
                {
                    feedback.ProductId,
                    feedback.CreatedAtUtc
                });

                entity.HasOne(feedback => feedback.Product)
                    .WithMany()
                    .HasForeignKey(feedback => feedback.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<BeerGuideResponseContext>(entity =>
            {
                entity.Property(context => context.CreatedAtUtc)
                    .HasColumnType("timestamp with time zone");
                entity.Property(context => context.RecommendedProductIds)
                    .IsRequired()
                    .HasMaxLength(500);
                entity.Property(context => context.RequestedStyle).HasMaxLength(40);
                entity.Property(context => context.FlavourTags).HasMaxLength(160);
                entity.Property(context => context.RequestedOrigin).HasMaxLength(40);
                entity.Property(context => context.StrengthPreference).HasMaxLength(20);
                entity.Property(context => context.BitternessPreference).HasMaxLength(20);
                entity.Property(context => context.SweetnessPreference).HasMaxLength(20);
                entity.Property(context => context.FoodPairing).HasMaxLength(40);
                entity.HasIndex(context => context.CreatedAtUtc);
            });

            builder.Entity<Reservation>(entity =>
            {
                /*
                 * ReservationDate претставува само календарски датум,
                 * па во PostgreSQL го чуваме како date, а не timestamptz.
                 */
                entity.Property(r => r.ReservationDate)
                    .HasColumnType("date");

                entity.Property(r => r.ReservationTime)
                    .HasColumnType("time without time zone");

                entity.Property(r => r.ReservationCode)
                    .IsRequired()
                    .HasMaxLength(16);

                entity.Property(r => r.EmailStatus)
                    .IsRequired()
                    .HasMaxLength(30);

                entity.Property(r => r.CreatedAtUtc)
                    .HasColumnType("timestamp with time zone");

                entity.Property(r => r.RespondedAtUtc)
                    .HasColumnType("timestamp with time zone");

                entity.Property(r => r.LastEmailSentAtUtc)
                    .HasColumnType("timestamp with time zone");

                entity.Property(r => r.LastEmailError)
                    .HasMaxLength(500);

                entity.Property(r => r.DeletedAtUtc)
                    .HasColumnType("timestamp with time zone");

                entity.Property(r => r.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(r => r.TableLabel)
                    .HasMaxLength(40);

                entity.Property(r => r.InternalNote)
                    .HasMaxLength(500);

                entity.HasIndex(r => new
                {
                    r.Status,
                    r.ReservationDate
                });

                entity.HasIndex(r => r.ReservationCode)
                    .IsUnique();

                entity.HasIndex(r => new
                {
                    r.ReservationDate,
                    r.ReservationTime,
                    r.TableLabel
                })
                .IsUnique()
                .HasFilter(
                    "\"TableLabel\" IS NOT NULL " +
                    "AND \"Status\" IN ('Approved', 'Arrived')");

                entity.HasQueryFilter(r => !r.IsDeleted);
            });

            builder.Entity<ReservationActivity>(entity =>
            {
                entity.Property(activity => activity.Title)
                    .IsRequired()
                    .HasMaxLength(80);

                entity.Property(activity => activity.Description)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(activity => activity.Actor)
                    .IsRequired()
                    .HasMaxLength(30);

                entity.Property(activity => activity.CreatedAtUtc)
                    .HasColumnType("timestamp with time zone");

                entity.HasIndex(activity => new
                {
                    activity.ReservationId,
                    activity.CreatedAtUtc
                });
            });

            builder.Entity<Notification>(entity =>
            {
                entity.Property(n => n.Title)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(n => n.Message)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(n => n.Link)
                    .HasMaxLength(300);

                entity.Property(n => n.CreatedAt)
                    .HasColumnType("timestamp with time zone");

                entity.HasIndex(n => new
                {
                    n.IsRead,
                    n.CreatedAt
                });

                entity.HasOne(n => n.Reservation)
                    .WithMany()
                    .HasForeignKey(n => n.ReservationId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<StaffMember>(entity =>
            {
                entity.Property(staff => staff.FullName)
                    .IsRequired()
                    .HasMaxLength(80);

                entity.Property(staff => staff.Role)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(staff => staff.PhoneNumber)
                    .HasMaxLength(40);

                entity.HasIndex(staff => new
                {
                    staff.IsActive,
                    staff.Role
                });
            });

            builder.Entity<StaffShift>(entity =>
            {
                entity.Property(shift => shift.Role)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(shift => shift.ShiftDate)
                    .HasColumnType("date");

                entity.Property(shift => shift.StartsAt)
                    .HasColumnType("time without time zone");

                entity.Property(shift => shift.EndsAt)
                    .HasColumnType("time without time zone");

                entity.Property(shift => shift.Note)
                    .HasMaxLength(160);

                entity.Property(shift => shift.CreatedAtUtc)
                    .HasColumnType("timestamp with time zone");

                entity.HasIndex(shift => new
                {
                    shift.ShiftDate,
                    shift.Role
                });

                entity.HasOne(shift => shift.StaffMember)
                    .WithMany(staff => staff.Shifts)
                    .HasForeignKey(shift => shift.StaffMemberId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
