using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rebel.Domain.Enums;
using Rebel.Infrastructure.Data;
using Rebel.Web.Models;
using Rebel.Web.Authorization;

namespace Rebel.Web.Controllers
{
    [Authorize(Policy = AdminPolicies.Backstage)]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        private static readonly TimeZoneInfo SkopjeTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Skopje");

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? q,
            CancellationToken cancellationToken)
        {
            var skopjeNow = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                SkopjeTimeZone
            );

            var startOfTodayInSkopje = DateTime.SpecifyKind(
                skopjeNow.Date,
                DateTimeKind.Unspecified
            );

            var startOfTodayUtc = TimeZoneInfo.ConvertTimeToUtc(
                startOfTodayInSkopje,
                SkopjeTimeZone
            );

            var startOfTomorrowUtc =
                startOfTodayUtc.AddDays(1);

            var today = skopjeNow.Date;
            var tomorrow = today.AddDays(1);

            var tonightReservations = await _context.Reservations
                .AsNoTracking()
                .Include(reservation => reservation.Event)
                .Where(reservation =>
                    reservation.ReservationDate >= today &&
                    reservation.ReservationDate < tomorrow &&
                    reservation.Status != ReservationStatus.Rejected &&
                    reservation.Status != ReservationStatus.Cancelled)
                .OrderBy(reservation => reservation.ReservationTime)
                .ToListAsync(cancellationToken);

            var activeTables = await _context.PubTables
                .AsNoTracking()
                .Where(table => table.IsActive)
                .ToListAsync(cancellationToken);

            var searchQuery = string.IsNullOrWhiteSpace(q)
                ? null
                : q.Trim();

            var activeTonightReservations = tonightReservations
                .Where(reservation =>
                    reservation.Status != ReservationStatus.NoShow &&
                    reservation.Status != ReservationStatus.Cancelled &&
                    reservation.Status != ReservationStatus.Rejected)
                .ToList();

            var nextArrivals = activeTonightReservations
                .Where(reservation =>
                    reservation.Status == ReservationStatus.Approved &&
                    reservation.ReservationTime >= skopjeNow.TimeOfDay)
                .OrderBy(reservation => reservation.ReservationTime)
                .ThenBy(reservation => reservation.FullName)
                .Take(4)
                .ToList();

            if (nextArrivals.Count == 0)
            {
                nextArrivals = activeTonightReservations
                    .Where(reservation =>
                        reservation.Status == ReservationStatus.Approved)
                    .OrderBy(reservation => reservation.ReservationTime)
                    .ThenBy(reservation => reservation.FullName)
                    .Take(4)
                    .ToList();
            }

            var todayEventBookings = tonightReservations
                .Where(reservation =>
                    reservation.EventId.HasValue &&
                    reservation.Event != null &&
                    reservation.Status != ReservationStatus.Cancelled &&
                    reservation.Status != ReservationStatus.Rejected &&
                    reservation.Status != ReservationStatus.NoShow)
                .GroupBy(reservation => new
                {
                    reservation.EventId,
                    reservation.Event!.Title,
                    reservation.Event.StartTime
                })
                .Select(group => new DashboardEventBookingSummary
                {
                    EventId = group.Key.EventId!.Value,
                    Title = group.Key.Title,
                    StartTime = group.Key.StartTime,
                    ReservationsCount = group.Count(),
                    GuestsCount = group.Sum(reservation =>
                        reservation.NumberOfGuests)
                })
                .OrderBy(summary => summary.StartTime ?? TimeSpan.MaxValue)
                .ThenBy(summary => summary.Title)
                .ToList();

            var model = new DashboardViewModel
            {
                CategoriesCount = await _context.Categories
                    .CountAsync(cancellationToken),

                ProductsCount = await _context.Products
                    .CountAsync(cancellationToken),

                EventsCount = await _context.Events
                    .CountAsync(cancellationToken),

                ReservationsCount = await _context.Reservations
                    .CountAsync(cancellationToken),

                PendingReservationsCount = await _context.Reservations
                    .CountAsync(
                        reservation =>
                            reservation.Status == ReservationStatus.Pending,
                        cancellationToken
                    ),

                TonightReservationsCount =
                    tonightReservations.Count,

                TonightGuestsCount =
                    tonightReservations.Sum(reservation =>
                        reservation.NumberOfGuests),

                TonightPendingReservationsCount =
                    tonightReservations.Count(reservation =>
                        reservation.Status == ReservationStatus.Pending),

                TonightApprovedReservationsCount =
                    tonightReservations.Count(reservation =>
                        reservation.Status == ReservationStatus.Approved),

                TonightArrivedReservationsCount =
                    tonightReservations.Count(reservation =>
                        reservation.Status == ReservationStatus.Arrived),

                TonightNoShowReservationsCount =
                    tonightReservations.Count(reservation =>
                        reservation.Status == ReservationStatus.NoShow),

                TonightCancelledReservationsCount =
                    await _context.Reservations
                        .CountAsync(
                            reservation =>
                                reservation.ReservationDate >= today &&
                                reservation.ReservationDate < tomorrow &&
                                reservation.Status == ReservationStatus.Cancelled,
                            cancellationToken),

                TonightUnassignedTablesCount =
                    tonightReservations.Count(reservation =>
                        reservation.Status != ReservationStatus.Pending &&
                        reservation.Status != ReservationStatus.NoShow &&
                        reservation.Status != ReservationStatus.Cancelled &&
                        string.IsNullOrWhiteSpace(
                            reservation.TableLabel)),

                TonightLargePartiesCount =
                    activeTonightReservations.Count(reservation =>
                        reservation.NumberOfGuests >= 9),

                EmailFailuresCount = await _context.Reservations
                    .CountAsync(
                        reservation =>
                            reservation.EmailStatus.Contains("Failed"),
                        cancellationToken),

                TodayEventBookingsCount =
                    todayEventBookings.Sum(summary =>
                        summary.ReservationsCount),

                UnavailableProductsCount = await _context.Products
                    .CountAsync(
                        product => !product.IsAvailable,
                        cancellationToken),

                ActiveTablesCount = activeTables.Count,

                ActiveTableCapacity = activeTables.Sum(table =>
                    table.Capacity),

                SearchQuery = searchQuery,

                SearchResults = await BuildSearchResults(
                    searchQuery,
                    cancellationToken),

                LatestPendingReservations = await _context.Reservations
                    .AsNoTracking()
                    .Include(reservation => reservation.Event)
                    .Where(reservation =>
                        reservation.Status == ReservationStatus.Pending)
                    .OrderBy(reservation =>
                        reservation.ReservationDate)
                    .ThenBy(reservation =>
                        reservation.ReservationTime)
                    .Take(5)
                    .ToListAsync(cancellationToken),

                NextArrivals = nextArrivals,

                LargePartyReservations = activeTonightReservations
                    .Where(reservation =>
                        reservation.NumberOfGuests >= 9)
                    .OrderBy(reservation =>
                        reservation.ReservationTime)
                    .ThenByDescending(reservation =>
                        reservation.NumberOfGuests)
                    .Take(4)
                    .ToList(),

                EmailFailureReservations = await _context.Reservations
                    .AsNoTracking()
                    .Where(reservation =>
                        reservation.EmailStatus.Contains("Failed"))
                    .OrderByDescending(reservation =>
                        reservation.LastEmailSentAtUtc ??
                        reservation.CreatedAtUtc)
                    .Take(4)
                    .ToListAsync(cancellationToken),

                UnavailableProducts = await _context.Products
                    .AsNoTracking()
                    .Include(product => product.Category)
                    .Where(product => !product.IsAvailable)
                    .OrderBy(product => product.Category!.Name)
                    .ThenBy(product => product.Name)
                    .Take(5)
                    .ToListAsync(cancellationToken),

                TodayEvents = await _context.Events
                    .AsNoTracking()
                    .Where(eventItem =>
                        eventItem.IsActive &&
                        eventItem.Date >= startOfTodayUtc &&
                        eventItem.Date < startOfTomorrowUtc)
                    .OrderBy(eventItem => eventItem.StartTime)
                    .ToListAsync(cancellationToken),

                UpcomingEvents = await _context.Events
                    .AsNoTracking()
                    .Where(eventItem =>
                        eventItem.IsActive &&
                        eventItem.Date >= startOfTodayUtc
                    )
                    .OrderBy(eventItem => eventItem.Date)
                    .ThenBy(eventItem => eventItem.StartTime)
                    .Take(3)
                    .ToListAsync(cancellationToken),

                TodayEventBookings = todayEventBookings
            };

            return View(model);
        }

        private async Task<List<DashboardSearchResult>> BuildSearchResults(
            string? searchQuery,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                return new List<DashboardSearchResult>();
            }

            var normalizedQuery = searchQuery.Trim().ToLower();

            var reservations = await _context.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.FullName.ToLower().Contains(normalizedQuery) ||
                    reservation.PhoneNumber.ToLower().Contains(normalizedQuery) ||
                    reservation.Email.ToLower().Contains(normalizedQuery) ||
                    reservation.ReservationCode.ToLower().Contains(normalizedQuery))
                .OrderByDescending(reservation =>
                    reservation.CreatedAtUtc)
                .Take(5)
                .ToListAsync(cancellationToken);

            var events = await _context.Events
                .AsNoTracking()
                .Where(eventItem =>
                    eventItem.Title.ToLower().Contains(normalizedQuery) ||
                    eventItem.Description.ToLower().Contains(normalizedQuery))
                .OrderBy(eventItem => eventItem.Date)
                .ThenBy(eventItem => eventItem.StartTime)
                .Take(4)
                .ToListAsync(cancellationToken);

            var products = await _context.Products
                .AsNoTracking()
                .Include(product => product.Category)
                .Where(product =>
                    product.Name.ToLower().Contains(normalizedQuery) ||
                    (product.Description ?? string.Empty).ToLower().Contains(normalizedQuery) ||
                    (product.Category != null &&
                     product.Category.Name.ToLower().Contains(normalizedQuery)))
                .OrderBy(product => product.Name)
                .Take(5)
                .ToListAsync(cancellationToken);

            return reservations
                .Select(reservation => new DashboardSearchResult
                {
                    Type = "Reservation",
                    Title = reservation.FullName,
                    Meta =
                        reservation.ReservationCode + " / " +
                        reservation.ReservationDate.ToString("dd MMM") + " " +
                        reservation.ReservationTime.ToString(@"hh\:mm") + " / " +
                        reservation.Status,
                    Controller = "AdminReservations",
                    Action = "Details",
                    Id = reservation.Id
                })
                .Concat(events.Select(eventItem => new DashboardSearchResult
                {
                    Type = "Event",
                    Title = eventItem.Title,
                    Meta =
                        eventItem.Date.ToString("dd MMM yyyy") + " / " +
                        (eventItem.StartTime.HasValue
                            ? eventItem.StartTime.Value.ToString(@"hh\:mm")
                            : "time not set"),
                    Controller = "AdminEvents",
                    Action = "Edit",
                    Id = eventItem.Id
                }))
                .Concat(products.Select(product => new DashboardSearchResult
                {
                    Type = "Menu item",
                    Title = product.Name,
                    Meta =
                        (product.Category != null
                            ? product.Category.Name
                            : "Uncategorized") +
                        " / " +
                        (product.IsAvailable ? "available" : "unavailable"),
                    Controller = "Product",
                    Action = "Edit",
                    Id = product.Id
                }))
                .Take(12)
                .ToList();
        }
    }
}
