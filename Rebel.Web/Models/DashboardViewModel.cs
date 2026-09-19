using Rebel.Domain.Entities;

namespace Rebel.Web.Models
{
    public class DashboardViewModel
    {
        public int TonightGuestsCount { get; set; }

        public int TonightPendingReservationsCount { get; set; }

        public int TonightUnassignedTablesCount { get; set; }

        public string? SearchQuery { get; set; }

        public List<DashboardSearchResult> SearchResults { get; set; } = new();

        public List<Reservation> NextArrivals { get; set; } = new();
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
