using Microsoft.AspNetCore.Http;

namespace Rebel.Web.Services
{
    public interface IEventImageStorage
    {
        Task<string> SaveAsync(
            IFormFile image,
            CancellationToken cancellationToken = default);
    }
}
