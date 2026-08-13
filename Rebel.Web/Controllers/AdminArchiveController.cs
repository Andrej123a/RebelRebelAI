using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rebel.Infrastructure.Data;
using Rebel.Web.Models;
using Rebel.Web.Authorization;

namespace Rebel.Web.Controllers
{
    [Authorize(Policy = AdminPolicies.ManagerOnly)]
    public class AdminArchiveController : Controller
    {
        private readonly AppDbContext _context;

        public AdminArchiveController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            CancellationToken cancellationToken)
        {
            var model = new AdminArchiveViewModel
            {
                Products = await _context.Products
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Include(product => product.Category)
                    .Where(product => product.IsDeleted)
                    .OrderByDescending(product => product.DeletedAtUtc)
                    .ThenBy(product => product.Name)
                    .ToListAsync(cancellationToken),

                Events = await _context.Events
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(eventItem => eventItem.IsDeleted)
                    .OrderByDescending(eventItem => eventItem.DeletedAtUtc)
                    .ThenBy(eventItem => eventItem.Date)
                    .ToListAsync(cancellationToken),

                Reservations = await _context.Reservations
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Include(reservation => reservation.Event)
                    .Where(reservation => reservation.IsDeleted)
                    .OrderByDescending(reservation => reservation.DeletedAtUtc)
                    .ThenBy(reservation => reservation.ReservationDate)
                    .ThenBy(reservation => reservation.ReservationTime)
                    .ToListAsync(cancellationToken),

                Categories = await _context.Categories
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(category => category.IsDeleted)
                    .OrderByDescending(category => category.DeletedAtUtc)
                    .ThenBy(category => category.Name)
                    .ToListAsync(cancellationToken)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreProduct(
            Guid id,
            CancellationToken cancellationToken)
        {
            var product = await _context.Products
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    product => product.Id == id,
                    cancellationToken);

            if (product is null)
            {
                return NotFound();
            }

            product.IsDeleted = false;
            product.DeletedAtUtc = null;

            await _context.SaveChangesAsync(cancellationToken);
            TempData["SuccessMessage"] = $"{product.Name} is back on the menu board.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreEvent(
            Guid id,
            CancellationToken cancellationToken)
        {
            var eventItem = await _context.Events
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    eventItem => eventItem.Id == id,
                    cancellationToken);

            if (eventItem is null)
            {
                return NotFound();
            }

            eventItem.IsDeleted = false;
            eventItem.DeletedAtUtc = null;

            await _context.SaveChangesAsync(cancellationToken);
            TempData["SuccessMessage"] = $"{eventItem.Title} is back on the gig calendar.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreReservation(
            Guid id,
            CancellationToken cancellationToken)
        {
            var reservation = await _context.Reservations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    reservation => reservation.Id == id,
                    cancellationToken);

            if (reservation is null)
            {
                return NotFound();
            }

            reservation.IsDeleted = false;
            reservation.DeletedAtUtc = null;

            await _context.SaveChangesAsync(cancellationToken);
            TempData["SuccessMessage"] = $"{reservation.FullName}'s reservation has been restored.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreCategory(
            Guid id,
            CancellationToken cancellationToken)
        {
            var category = await _context.Categories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    category => category.Id == id,
                    cancellationToken);

            if (category is null)
            {
                return NotFound();
            }

            category.IsDeleted = false;
            category.DeletedAtUtc = null;

            await _context.SaveChangesAsync(cancellationToken);
            TempData["SuccessMessage"] = $"{category.Name} is back in menu sections.";

            return RedirectToAction(nameof(Index));
        }
    }
}
