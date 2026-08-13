using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rebel.Infrastructure.Data;

namespace Rebel.Web.Controllers
{
    public class EventsController : Controller
    {
        private readonly AppDbContext _context;

        public EventsController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Skopje");

            var skopjeNow = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                timeZone
            );

            var startOfTodayInSkopje = DateTime.SpecifyKind(
                skopjeNow.Date,
                DateTimeKind.Unspecified
            );

            var startOfTodayUtc = TimeZoneInfo.ConvertTimeToUtc(
                startOfTodayInSkopje,
                timeZone
            );

            var events = await _context.Events
                .AsNoTracking()
                .Where(e => e.IsActive && e.Date >= startOfTodayUtc)
                .OrderBy(e => e.Date)
                .ThenBy(e => e.StartTime)
                .ToListAsync();

            return View(events);
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var ev = await _context.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id && e.IsActive);

            if (ev == null)
            {
                return NotFound();
            }

            return View(ev);
        }
    }
}