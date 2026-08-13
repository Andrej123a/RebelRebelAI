using Rebel.Domain.Entities;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public interface IBeerNoMatchRecoveryService
{
    BeerNoMatchRecovery Build(
        string query,
        IReadOnlyCollection<Product> availableBeers);
}
