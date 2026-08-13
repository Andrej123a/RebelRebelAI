using Rebel.Domain.Entities;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public interface IBeerProfileSuggestionService
{
    IReadOnlyList<BeerProfileSuggestionViewModel> Suggest(Product beer);
}
