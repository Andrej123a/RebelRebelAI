using Rebel.Domain.Entities;

namespace Rebel.Web.Models
{
    public class HomeViewModel
    {
        public Event? FeaturedEvent { get; set; }

        public List<Event> UpcomingEvents { get; set; } = new();
    }
}