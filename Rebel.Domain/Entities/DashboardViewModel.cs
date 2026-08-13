using Rebel.Domain.Entities;

namespace Rebel.Web.Models
{
    public class DashboardViewModel
    {
        public int CategoriesCount { get; set; }

        public int ProductsCount { get; set; }

        public int EventsCount { get; set; }

        public int ReservationsCount { get; set; }

        public int PendingReservationsCount { get; set; }

        public int TonightReservationsCount { get; set; }

        public int TonightGuestsCount { get; set; }

        public int TonightPendingReservationsCount { get; set; }

        public int TonightApprovedReservationsCount { get; set; }

        public int TonightArrivedReservationsCount { get; set; }

        public int TonightNoShowReservationsCount { get; set; }

        public int TonightCancelledReservationsCount { get; set; }

        public int TonightUnassignedTablesCount { get; set; }

        public int TonightLargePartiesCount { get; set; }

        public int EmailFailuresCount { get; set; }

        public int TodayEventBookingsCount { get; set; }

        public int UnavailableProductsCount { get; set; }

        public int ActiveTablesCount { get; set; }

        public int ActiveTableCapacity { get; set; }

        public string? SearchQuery { get; set; }

        public List<DashboardSearchResult> SearchResults { get; set; } = new();

        public List<Reservation> LatestPendingReservations { get; set; } = new();

        public List<Reservation> NextArrivals { get; set; } = new();

        public List<Reservation> LargePartyReservations { get; set; } = new();

        public List<Reservation> EmailFailureReservations { get; set; } = new();

        public List<Product> UnavailableProducts { get; set; } = new();

        public List<Event> TodayEvents { get; set; } = new();

        public List<Event> UpcomingEvents { get; set; } = new();

        public List<DashboardEventBookingSummary> TodayEventBookings { get; set; } = new();
    }

    public class DashboardEventBookingSummary
    {
        public Guid EventId { get; set; }

        public string Title { get; set; } = string.Empty;

        public TimeSpan? StartTime { get; set; }

        public int ReservationsCount { get; set; }

        public int GuestsCount { get; set; }
    }

    public class DashboardSearchResult
    {
        public string Type { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Meta { get; set; } = string.Empty;

        public string Controller { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public Guid? Id { get; set; }
    }
}
