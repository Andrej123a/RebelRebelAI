using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Rebel.Domain.Entities;
using Rebel.Domain.Enums;
using Rebel.Infrastructure.Data;
using Rebel.Web.Authorization;

namespace Rebel.Web.Controllers
{
    [Authorize(Policy = AdminPolicies.Backstage)]
    public class AdminEventsController : Controller
    {
        private readonly AppDbContext _context;

        public AdminEventsController(AppDbContext context)
        {
            _context = context;
        }

        // INDEX
        [HttpGet]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> Index()
        {
            var events = await _context.Events
                .AsNoTracking()
                .OrderByDescending(e => e.Date)
                .ThenBy(e => e.StartTime)
                .ToListAsync();

            var eventIds = events
                .Select(e => e.Id)
                .ToList();

            var reservationStatsRows = await _context.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.EventId.HasValue &&
                    eventIds.Contains(reservation.EventId.Value))
                .GroupBy(reservation => reservation.EventId!.Value)
                .Select(group => new
                {
                    EventId = group.Key,
                    ReservationCount = group.Count(),
                    GuestCount = group.Sum(reservation =>
                        reservation.NumberOfGuests),
                    PendingCount = group.Count(reservation =>
                        reservation.Status == ReservationStatus.Pending),
                    ApprovedCount = group.Count(reservation =>
                        reservation.Status == ReservationStatus.Approved),
                    ArrivedCount = group.Count(reservation =>
                        reservation.Status == ReservationStatus.Arrived),
                    NoShowCount = group.Count(reservation =>
                        reservation.Status == ReservationStatus.NoShow),
                    CancelledCount = group.Count(reservation =>
                        reservation.Status == ReservationStatus.Cancelled)
                })
                .ToListAsync();

            var reservationStats = reservationStatsRows
                .ToDictionary(
                    eventStats => eventStats.EventId,
                    eventStats => new Dictionary<string, int>
                    {
                        ["Reservations"] = eventStats.ReservationCount,
                        ["Guests"] = eventStats.GuestCount,
                        ["Pending"] = eventStats.PendingCount,
                        ["Approved"] = eventStats.ApprovedCount,
                        ["Arrived"] = eventStats.ArrivedCount,
                        ["NoShow"] = eventStats.NoShowCount,
                        ["Cancelled"] = eventStats.CancelledCount
                    });

            ViewBag.ReservationStats = reservationStats;

            return View(events);
        }

        [HttpGet]
        public async Task<IActionResult> Bookings(
            Guid id,
            CancellationToken cancellationToken)
        {
            var ev = await _context.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    eventItem => eventItem.Id == id,
                    cancellationToken);

            if (ev == null)
            {
                return NotFound();
            }

            var reservations = await _context.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.EventId == id)
                .OrderBy(reservation =>
                    reservation.Status == ReservationStatus.Pending ? 0 :
                    reservation.Status == ReservationStatus.Approved ? 1 :
                    reservation.Status == ReservationStatus.Arrived ? 2 :
                    reservation.Status == ReservationStatus.NoShow ? 3 :
                    reservation.Status == ReservationStatus.Cancelled ? 4 : 5)
                .ThenBy(reservation => reservation.ReservationTime)
                .ThenBy(reservation => reservation.FullName)
                .ToListAsync(cancellationToken);

            ViewBag.Event = ev;

            return View(reservations);
        }

        // CREATE GET
        [HttpGet]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public IActionResult Create()
        {
            var model = new Event
            {
                Date = DateTime.UtcNow.Date,
                IsActive = true
            };

            return View(model);
        }

        // CREATE POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> Create(Event model)
        {
            ValidateEventTimes(model);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.Id = Guid.NewGuid();
            model.Title = model.Title.Trim();
            model.Description = model.Description.Trim();

            model.ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl)
                ? null
                : model.ImageUrl.Trim();

            model.Date = DateTime.SpecifyKind(
                model.Date.Date,
                DateTimeKind.Utc
            );

            _context.Events.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"{model.Title} was added to the gig calendar.";

            return RedirectToAction(nameof(Index));
        }

        // EDIT GET
        [HttpGet]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> Edit(Guid id)
        {
            var ev = await _context.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (ev == null)
            {
                return NotFound();
            }

            return View(ev);
        }

        // EDIT POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> Edit(Guid id, Event model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            ValidateEventTimes(model);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var existingEvent = await _context.Events.FindAsync(id);

            if (existingEvent == null)
            {
                return NotFound();
            }

            existingEvent.Title = model.Title.Trim();
            existingEvent.Description = model.Description.Trim();

            existingEvent.Date = DateTime.SpecifyKind(
                model.Date.Date,
                DateTimeKind.Utc
            );

            existingEvent.StartTime = model.StartTime;
            existingEvent.EndTime = model.EndTime;
            existingEvent.IsActive = model.IsActive;
            existingEvent.MaxReservations = model.MaxReservations;
            existingEvent.MaxGuests = model.MaxGuests;

            existingEvent.ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl)
                ? null
                : model.ImageUrl.Trim();

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"{existingEvent.Title} was updated.";

            return RedirectToAction(nameof(Index));
        }

        // DELETE GET
        [HttpGet]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var ev = await _context.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (ev == null)
            {
                return NotFound();
            }

            return View(ev);
        }

        // DELETE POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var ev = await _context.Events.FindAsync(id);

            if (ev == null)
            {
                return NotFound();
            }

            ev.IsDeleted = true;
            ev.DeletedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"{ev.Title} was archived from the gig calendar.";

            return RedirectToAction(nameof(Index));
        }

        private void ValidateEventTimes(Event model)
        {
            if (model.MaxReservations.HasValue &&
                model.MaxReservations.Value < 1)
            {
                ModelState.AddModelError(
                    nameof(model.MaxReservations),
                    "Max reservations must be at least 1."
                );
            }

            if (model.MaxGuests.HasValue &&
                model.MaxGuests.Value < 1)
            {
                ModelState.AddModelError(
                    nameof(model.MaxGuests),
                    "Max guests must be at least 1."
                );
            }

            if (!model.StartTime.HasValue || !model.EndTime.HasValue)
            {
                return;
            }

            // Пример 22:00–02:00 е дозволен:
            // завршува следниот ден.
            if (model.StartTime.Value == model.EndTime.Value)
            {
                ModelState.AddModelError(
                    nameof(model.EndTime),
                    "Start time and end time cannot be the same."
                );
            }
        }

    }
}
