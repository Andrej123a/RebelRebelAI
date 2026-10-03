using Rebel.Domain.Entities;

namespace Rebel.Web.Models
{
    public class HomeViewModel
    {
        public Event? FeaturedEvent { get; set; }

        /// <summary>What Life on Mars? orders today: a popular dish from the menu, a different one each day.</summary>
        public Product? DishOfTheDay { get; set; }

        /// <summary>The beers the spacemen drink, by look key ("ziggy", "tom"), when they are on the menu.</summary>
        public IReadOnlyDictionary<string, Product> Picks { get; set; } = new Dictionary<string, Product>();
    }
}
