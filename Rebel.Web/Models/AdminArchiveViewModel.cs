using Rebel.Domain.Entities;

namespace Rebel.Web.Models
{
    public class AdminArchiveViewModel
    {
        public IReadOnlyList<Product> Products { get; set; } =
            Array.Empty<Product>();

        public IReadOnlyList<Event> Events { get; set; } =
            Array.Empty<Event>();

        public IReadOnlyList<Reservation> Reservations { get; set; } =
            Array.Empty<Reservation>();

        public IReadOnlyList<Category> Categories { get; set; } =
            Array.Empty<Category>();

        public int TotalItems =>
            Products.Count +
            Events.Count +
            Reservations.Count +
            Categories.Count;
    }
}
